using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SSRSCopilot.ApiService.Models;
using SSRSCopilot.ApiService.Services;
using System.Text;
using System.Text.Json;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Agent responsible for interfacing with SQL Reporting Services REST API to retrieve report details and parameters
/// </summary>
public class SsrsApiAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly ISsrsRestApiService _ssrsRestApiService;
    private readonly ILogger<SsrsApiAgent> _logger;
    
    public SsrsApiAgent(
        Kernel kernel,
        ISsrsRestApiService ssrsRestApiService,
        ILogger<SsrsApiAgent> logger)
    {
        _kernel = kernel;
        _ssrsRestApiService = ssrsRestApiService;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        try
        {
            // Add the user message to the history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });
            
            // Ensure we have a selected report name from the previous agent
            if (context.SelectedReport == null || string.IsNullOrEmpty(context.SelectedReport.Name))
            {
                context.State = AgentState.ReportSelection;
                return new ChatResponse
                {
                    Message = "Let's first select a report. What type of report are you looking for?",
                    State = AgentState.ReportSelection
                };
            }
            
            // Try to find the report in the SSRS REST API using the report name
            var report = await _ssrsRestApiService.GetReportByNameAsync(context.SelectedReport.Name);
            
            if (report == null)
            {
                // If the report wasn't found using basic matching, try LLM-based matching
                _logger.LogInformation("Attempting LLM-based report matching because exact match not found");
                report = await TryLlmBasedReportMatchingAsync(context.SelectedReport.Name, context.History);
                
                if (report != null)
                {
                    _logger.LogInformation("LLM successfully matched report: {ReportName}", report.Name);
                }
                else
                {
                    // If the report still wasn't found, ask for more information
                    var systemMessage = new StringBuilder();
                    systemMessage.AppendLine("You are an AI assistant that helps users find reports in the SQL Server Reporting Services (SSRS) system.");
                    systemMessage.AppendLine("The user is looking for a report but we couldn't find it by name in the system.");
                    systemMessage.AppendLine("Ask for more details about the report they're looking for.");
                    systemMessage.AppendLine("Be apologetic but helpful, and suggest they try a different name or provide more details.");
                    
                    // Create a prompt for the LLM including context from chat history
                    var prompt = new StringBuilder();
                    prompt.AppendLine(systemMessage.ToString());
                    prompt.AppendLine("\nRecent conversation context:");
                    
                    // Add up to 5 most recent messages from chat history to provide context
                    var recentMessages = context.History.Skip(Math.Max(0, context.History.Count - 5)).ToList();
                    foreach (var msg in recentMessages)
                    {
                        string role = msg.Role == "user" ? "User" : "Assistant";
                        prompt.AppendLine($"{role}: {msg.Content}");
                    }
                    
                    prompt.AppendLine("\nGenerate a helpful response:");
                    
                    // Create and invoke the function using the kernel
                    var responseFunction = _kernel.CreateFunctionFromPrompt(
                        prompt.ToString(),
                        new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
                        {
                            Temperature = 0.7,
                            MaxTokens = 500
                        });

                    var result = await _kernel.InvokeAsync(responseFunction);
                    
                    // Extract the response content
                    var responseContent = result.ToString();
                    
                    // Add the assistant's message to the history
                    context.History.Add(new ChatMessage { Role = "assistant", Content = responseContent });
                    
                    // Return the response
                    return new ChatResponse
                    {
                        Message = responseContent,
                        State = context.State
                    };
                }
            }
            
            // Update the selected report with the full details from the REST API
            context.SelectedReport = report;
            
            // If we found the report and have the parameters, proceed to parameter filling
            if (report.Parameters.Any())
            {
                // Generate a response letting the user know we found the report and its parameters
                var responseBuilder = new StringBuilder();
                
                responseBuilder.AppendLine($"I found the report \"{report.Name}\".");
                
                if (report.Parameters.Any())
                {
                    responseBuilder.AppendLine("This report requires the following parameters:");
                    
                    foreach (var param in report.Parameters)
                    {
                        responseBuilder.Append($"- {param.Name}");
                        
                        if (param.IsRequired)
                        {
                            responseBuilder.Append(" (required)");
                        }
                        
                        if (!string.IsNullOrEmpty(param.Description))
                        {
                            responseBuilder.Append($": {param.Description}");
                        }
                        
                        responseBuilder.AppendLine();
                        
                        // If the parameter has allowed values, list them
                        if (param.AllowedValues != null && param.AllowedValues.Any())
                        {
                            responseBuilder.AppendLine($"  Allowed values: {string.Join(", ", param.AllowedValues)}");
                        }
                        
                        // If the parameter has a default value, show it
                        if (!string.IsNullOrEmpty(param.DefaultValue))
                        {
                            responseBuilder.AppendLine($"  Default value: {param.DefaultValue}");
                        }
                    }
                    
                    responseBuilder.AppendLine("\nLet's fill in these parameters. What value would you like to use for each one?");
                }
                else
                {
                    responseBuilder.AppendLine("This report doesn't require any parameters.");
                }
                
                // Add the assistant's message to the history
                string assistantMessage = responseBuilder.ToString();
                context.History.Add(new ChatMessage { Role = "assistant", Content = assistantMessage });
                
                // Set state to parameter filling
                context.State = AgentState.ParameterFilling;
                
                return new ChatResponse
                {
                    Message = assistantMessage,
                    State = AgentState.ParameterFilling
                };
            }
            else
            {
                // No parameters needed, move directly to URL creation
                context.State = AgentState.ReportUrlCreation;
                
                // Add a message to history
                string assistantMessage = $"I found the report \"{report.Name}\". This report doesn't require any parameters, so I'll generate the URL for you.";
                context.History.Add(new ChatMessage { Role = "assistant", Content = assistantMessage });
                
                return new ChatResponse
                {
                    Message = assistantMessage,
                    State = AgentState.ReportUrlCreation
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SsrsApiAgent.ProcessMessageAsync");
            
            // In case of error, provide a generic message and stay in the current state
            return new ChatResponse
            {
                Message = "I'm having trouble accessing the report information from the SSRS server. Let me try again. Could you provide more details about the report you're looking for?",
                State = context.State
            };
        }
    }
    
    /// <summary>
    /// Tries to match the report using LLM-based semantic matching
    /// </summary>
    /// <remarks>
    /// This method uses the LLM to find a semantically similar report when exact or contains matching fails.
    /// It provides the LLM with:
    /// 1. The list of all available reports in the system
    /// 2. Recent conversation history for context
    /// 3. The report name the user is looking for
    /// 
    /// The LLM then identifies the most semantically similar report from the list.
    /// </remarks>
    /// <param name="reportName">The report name to match</param>
    /// <param name="history">The conversation history for context</param>
    /// <returns>The matched report if found, null otherwise</returns>
    private async Task<Report?> TryLlmBasedReportMatchingAsync(string reportName, List<ChatMessage> history)
    {
        try
        {
            _logger.LogInformation("Attempting LLM-based report matching for: {ReportName}", reportName);
            
            // Get all available reports
            var allReports = await _ssrsRestApiService.GetReportsAsync();
            
            if (allReports == null || !allReports.Any())
            {
                _logger.LogWarning("No reports found in the system");
                return null;
            }
            
            // Create a list of report names to provide to the LLM
            var reportNames = allReports.Select(r => r.Name).ToList();
            
            // Format the list for the LLM prompt
            var formattedReportList = string.Join("\n", reportNames.Select((name, index) => $"{index + 1}. {name}"));
            
            // Build a prompt for the LLM
            var systemMessage = new StringBuilder();
            systemMessage.AppendLine("You are an AI assistant helping to find reports in the SQL Server Reporting Services (SSRS).");
            systemMessage.AppendLine("A user is looking for a report, but we couldn't find an exact match.");
            systemMessage.AppendLine("Below are the available reports in the system. Please identify which report from the list is the best match for what the user is looking for.");
            systemMessage.AppendLine("Consider semantic similarity, not just string similarity. The names might have different formats or abbreviations.");
            systemMessage.AppendLine("\nAvailable reports:");
            systemMessage.AppendLine(formattedReportList);
            systemMessage.AppendLine("\nRespond ONLY with the name of the best matching report (exactly as listed), or 'NO MATCH' if none of the reports seem to match.");
            
            // Create a prompt including context from chat history
            var prompt = new StringBuilder();
            prompt.AppendLine(systemMessage.ToString());
            prompt.AppendLine("\nConversation context:");
            
            // Add up to 5 most recent messages from chat history to provide context
            var recentMessages = history.Skip(Math.Max(0, history.Count - 5)).ToList();
            foreach (var msg in recentMessages)
            {
                string role = msg.Role == "user" ? "User" : "Assistant";
                prompt.AppendLine($"{role}: {msg.Content}");
            }
            
            prompt.AppendLine($"\nBased on the conversation above, which report from the list best matches '{reportName}'? Respond only with the exact name from the list.");
            
            // Create and invoke the function using the kernel
            var matchFunction = _kernel.CreateFunctionFromPrompt(
                prompt.ToString(),
                new Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings
                {
                    Temperature = 0.0,
                    MaxTokens = 100
                });

            var result = await _kernel.InvokeAsync(matchFunction);
            
            // Process the LLM's response to extract the matched report name
            string suggestedReportName = result.ToString().Trim();
            
            // Remove any common prefixes the LLM might add
            var prefixesToRemove = new[] { "The best match is ", "Best match: ", "I recommend ", "You should use ", "The report " };
            foreach (var prefix in prefixesToRemove)
            {
                if (suggestedReportName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    suggestedReportName = suggestedReportName.Substring(prefix.Length);
                    break;
                }
            }
            
            // Remove quotation marks if present
            suggestedReportName = suggestedReportName.Trim('"', '\'', '`');
            
            // Check if LLM couldn't find a match
            if (suggestedReportName.Equals("NO MATCH", StringComparison.OrdinalIgnoreCase) ||
                suggestedReportName.Contains("no match", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("LLM couldn't find a matching report");
                return null;
            }
            
            _logger.LogInformation("LLM suggested report name: {SuggestedReportName}", suggestedReportName);
            
            // Look for the suggested report in the list
            var matchingReport = allReports.FirstOrDefault(r => 
                r.Name.Equals(suggestedReportName, StringComparison.OrdinalIgnoreCase));
                
            if (matchingReport == null)
            {
                // If not found by exact match, try closest match by name
                var bestMatchReport = allReports
                    .OrderByDescending(r => GetSimilarity(r.Name, suggestedReportName))
                    .FirstOrDefault();
                    
                if (bestMatchReport != null)
                {
                    _logger.LogInformation("Found closest match: {ReportName}", bestMatchReport.Name);
                    return await _ssrsRestApiService.GetReportAsync(bestMatchReport.Id);
                }
            }
            else
            {
                _logger.LogInformation("Found exact match: {ReportName}", matchingReport.Name);
                return await _ssrsRestApiService.GetReportAsync(matchingReport.Id);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in TryLlmBasedReportMatchingAsync");
            return null;
        }
    }
    
    /// <summary>
    /// Calculates a simple similarity score between two strings
    /// </summary>
    private double GetSimilarity(string str1, string str2)
    {
        // Simple case-insensitive comparison for now
        str1 = str1.ToLowerInvariant();
        str2 = str2.ToLowerInvariant();
        
        // Check for contains
        if (str1.Contains(str2)) return 0.8;
        if (str2.Contains(str1)) return 0.8;
        
        // Check for word overlap
        var words1 = str1.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
        var words2 = str2.Split(new[] { ' ', '-', '_', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
        
        int matches = words1.Count(w1 => words2.Any(w2 => w2.Equals(w1, StringComparison.OrdinalIgnoreCase)));
        
        if (matches > 0)
        {
            return (double)matches / Math.Max(words1.Length, words2.Length);
        }
        
        return 0.0;
    }
}
