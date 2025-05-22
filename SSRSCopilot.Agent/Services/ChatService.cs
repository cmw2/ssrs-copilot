using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Plugins;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Service for handling chat interactions with auto function calling.
/// Uses the LLM to orchestrate the conversation flow without explicit state tracking.
/// </summary>
public class ChatService
{    private readonly Kernel _kernel;
    private readonly Dictionary<string, ChatHistory> _sessionChats = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger instance</param>
    public ChatService(Kernel kernel, IConfiguration configuration, ILogger<ChatService> logger)
    {
        _kernel = kernel;
        _configuration = configuration;
        _logger = logger;
    }    /// <summary>
    /// Loads the system message from the file specified in configuration
    /// </summary>
    /// <returns>The system message content</returns>
    private string LoadSystemMessage()
    {
        try
        {
            // Get the system message file path from configuration
            string? systemMessagePath = _configuration["AzureOpenAI:SystemMessagePath"];
            _logger.LogInformation("Loading system message, path from config: {SystemMessagePath}", systemMessagePath ?? "(not specified)");
            
            // If the path is specified, load the system message from the file
            if (!string.IsNullOrEmpty(systemMessagePath))
            {
                string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, systemMessagePath);
                _logger.LogDebug("Full system message path: {FullPath}", fullPath);
                
                if (File.Exists(fullPath))
                {
                    _logger.LogInformation("System message file found at: {FullPath}", fullPath);
                    string content = File.ReadAllText(fullPath);
                    _logger.LogDebug("System message loaded, length: {Length} characters", content.Length);
                    return content;
                }
                else
                {
                    _logger.LogWarning("System message file not found at: {FullPath}", fullPath);
                }
            }
            else
            {
                _logger.LogInformation("No system message path specified in configuration, using default message");
            }
            
            // Fall back to default system message if file not found or path not specified
            _logger.LogInformation("Using default system message");
            return @"
# General Instructions
- You are a helpful assistant that helps users find and run SSRS (SQL Server Reporting Services) reports.
- Your task is to guide users through the complete process of selecting a report, filling in required parameters, and generating a URL to view the report.  Once the report URL is generated, it will be displayed to the user by the front end.
- Be conversational and helpful. Explain what parameters are needed and why.

# Workflow
1. Help the user select a report based on their description
2. Get the report details and required parameters 
3. Ask the user for values for each required parameter and potentially optional parameters
4. When all parameters are provided, generate the report URL.  Be sure to do this step as it is the entire point of the process.

# Find the right report

IMPORTANT - SEMANTIC MATCHING REQUIRED: Report titles in the documentation (stored in Azure Search) often differ from the actual report names in SSRS. You need to perform semantic matching to find the best SSRS report that matches the documentation title:

1. First search for reports in the documentation using SearchReportsAsync from ReportSearchPlugin
2. When the user selects a report from documentation, call GetAllReportsAsync from SsrsPlugin to get all SSRS reports
3. Semantically compare the documentation title with all SSRS report names:
   - Look for similarities in keywords, business terms, and concepts
   - Compare report paths, descriptions, and names
   - Consider abbreviations, different word orders, and synonyms
   - Select the SSRS report with the highest semantic similarity
4. Use the matched SSRS report id when calling other SSRS functions
5. If you're unsure about the match, ask the user to pick one.

For example, if the documentation has a report titled 'Monthly Sales by Region' but the actual SSRS report is named 'RegionalSalesMonthly', you should semantically recognize they're referring to the same report.

# Report URL Generation
- When generating the report URL, include all required parameters and any optional parameters that the user has provided.
- The report URL will be displayed to the user by the front end.
- Prefix the report url with 'REPORT_URL:' in the response and place it on its own line at the end.
- Ensure that whenever you intend to display the report, you ALWAYS include the report url in the response with the prefix.  They won't see the report without this.
- Let the user know that they should see the report open to the side.
- You don't need to tell them to click a link.
- Give them a little summary of the report and the parameters you used to generate it.

Example:
   I'm showing your Product Report now, filtered by Model='ABC123' and Category='Electronics'.
   REPORT_URL:https://reportsserver/reports/report1?param1=value1&param2=value2'

# Error Handling
- If you encounter an error, provide a friendly message to the user.
";        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading system message: {ErrorMessage}", ex.Message);
            // Return default system message if there's an error loading from file
            return "You are a helpful assistant that helps users find and run SSRS reports.";
        }
    }

    /// <summary>
    /// Processes a chat message and generates a response using auto function calling.
    /// The LLM intelligently decides when to call functions from registered plugins.
    /// </summary>
    /// <param name="request">The chat request containing the user message and session ID</param>
    /// <returns>The chat response with optional report URL</returns>
    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        string sessionId = request.SessionId ?? string.Empty;        // Get or create chat history for this session
        if (!_sessionChats.TryGetValue(sessionId, out var chatHistory))
        {
            chatHistory = new ChatHistory();
            
            // Set initial system message to guide the conversation from configuration
            string systemMessage = LoadSystemMessage();
            
            chatHistory.AddSystemMessage(systemMessage);
            
            _sessionChats[sessionId] = chatHistory;
        }

        // Add the user message to the chat history
        chatHistory.AddUserMessage(request.Message);        // Create execution settings with auto function calling enabled
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = _configuration.GetValue<double?>("AzureOpenAI:Temperature") ?? 0.0,
            //TopP = 0.95,
        };

        // Get the chat completion service
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        try
        {
            // Get the response from the model with auto function calling
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel
            );

            // Extract the report URL if it's in the message
            string? reportUrl = null;
            var message = result.Content ?? string.Empty;

            // Look for report URL in the message - it will be prefixed with "REPORT_URL:"
            var reportUrlMarker = "REPORT_URL:";
            var reportUrlIndex = message.IndexOf(reportUrlMarker);
            if (reportUrlIndex >= 0)
            {
                var urlStart = reportUrlIndex + reportUrlMarker.Length;
                var urlEnd = message.IndexOf('\n', urlStart);
                if (urlEnd < 0)
                {
                    urlEnd = message.Length;
                }

                reportUrl = message.Substring(urlStart, urlEnd - urlStart).Trim();
                
                // Remove the URL marker from the message
                message = message.Remove(reportUrlIndex, urlEnd - reportUrlIndex);
                message = message.Trim();
            }

            // Add the assistant message to the history
            chatHistory.AddAssistantMessage(message);

            // Return the chat response
            return new ChatResponse
            {
                Message = message,
                ReportUrl = reportUrl
            };
        }
        catch (Exception ex)
        {
            // Add the error message to the chat history
            var errorMessage = $"I'm sorry, but I encountered an error: {ex.Message}. This might be due to difficulties finding the right report or matching the documentation title to the actual SSRS report name. Let's try again with more details. What kind of report are you looking for?";
            chatHistory.AddAssistantMessage(errorMessage);

            // Return the error response
            return new ChatResponse
            {
                Message = errorMessage
            };
        }
    }
}
