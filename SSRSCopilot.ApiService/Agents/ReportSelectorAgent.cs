using Microsoft.SemanticKernel;
using SSRSCopilot.ApiService.Models;
using SSRSCopilot.ApiService.Services;
using System.Text;
using System.Text.Json;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Agent responsible for helping users select a report
/// </summary>
public class ReportSelectorAgent : IAgent
{
    private readonly IReportService _reportService;
    private readonly Kernel _kernel;
    private readonly ILogger<ReportSelectorAgent> _logger;
    
    public ReportSelectorAgent(
        IReportService reportService,
        Kernel kernel,
        ILogger<ReportSelectorAgent> logger)
    {
        _reportService = reportService;
        _kernel = kernel;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        try
        {
            // Add the user message to the history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });
            
            // If we already have a selected report, move to the next agent
            if (context.SelectedReport != null)
            {
                context.State = AgentState.ParameterFilling;
                return new ChatResponse
                {
                    Message = $"I see you've already selected the '{context.SelectedReport.Name}' report. Let's fill in the required parameters.",
                    State = AgentState.ParameterFilling
                };
            }
            
            // Search for reports based on the user's query
            var reports = await _reportService.SearchReportsAsync(userMessage);
            
            if (reports.Count == 0)
            {
                // No reports found
                var noReportsMessage = "I couldn't find any reports matching your request. Could you provide more details about the type of report you're looking for?";
                context.History.Add(new ChatMessage { Role = "assistant", Content = noReportsMessage });
                
                return new ChatResponse
                {
                    Message = noReportsMessage,
                    State = AgentState.ReportSelection
                };
            }
            
            if (reports.Count == 1)
            {
                // Only one report found, select it automatically
                context.SelectedReport = reports[0];
                context.State = AgentState.ParameterFilling;
                
                var singleReportMessage = $"I found the '{reports[0].Name}' report which matches your request. This report {reports[0].Description}. Shall we use this report?";
                context.History.Add(new ChatMessage { Role = "assistant", Content = singleReportMessage });
                
                return new ChatResponse
                {
                    Message = singleReportMessage,
                    State = AgentState.ParameterFilling
                };
            }
            
            // Multiple reports found, ask user to select one
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("I found several reports that might match what you're looking for:");
            promptBuilder.AppendLine();
            
            for (int i = 0; i < reports.Count; i++)
            {
                promptBuilder.AppendLine($"{i + 1}. {reports[i].Name}: {reports[i].Description}");
            }
            
            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Which one would you like to use? You can select by number or name, or ask for more details about any of them.");
            
            var promptMessage = promptBuilder.ToString();
            context.History.Add(new ChatMessage { Role = "assistant", Content = promptMessage });
            
            // Use Semantic Kernel to process the response and try to select a report
            string systemPrompt = @"
You are a helpful assistant helping a user select a report from a list.
The user may respond in various ways:
1. They might select by number (e.g., ""1"" or ""number 2"")
2. They might select by name (e.g., ""Sales Report"")
3. They might ask for more details about a specific report
4. They might provide more information about what they're looking for

Extract the user's intent and determine if they've selected a specific report.
If they have, return the index (0-based) of the selected report.
If they haven't made a clear selection, return -1.

Reports:
";
            for (int i = 0; i < reports.Count; i++)
            {
                systemPrompt += $"{i}: {reports[i].Name}\n";
            }
            
            // Add the reports to the context for later reference
            context.History.Add(new ChatMessage { Role = "system", Content = JsonSerializer.Serialize(reports) });
            
            return new ChatResponse
            {
                Message = promptMessage,
                State = AgentState.ReportSelection
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReportSelectorAgent");
            return new ChatResponse
            {
                Message = "I'm sorry, but I encountered an error while searching for reports. Please try again or provide more details about what you're looking for.",
                State = AgentState.ReportSelection
            };
        }
    }
}
