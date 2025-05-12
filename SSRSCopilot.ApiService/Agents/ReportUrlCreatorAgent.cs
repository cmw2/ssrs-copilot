using SSRSCopilot.ApiService.Models;
using SSRSCopilot.ApiService.Services;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Agent responsible for creating a URL to run a report
/// </summary>
public class ReportUrlCreatorAgent : IAgent
{
    private readonly IReportUrlService _reportUrlService;
    private readonly ILogger<ReportUrlCreatorAgent> _logger;
    
    public ReportUrlCreatorAgent(
        IReportUrlService reportUrlService,
        ILogger<ReportUrlCreatorAgent> logger)
    {
        _reportUrlService = reportUrlService;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        // Add the user message to the history
        context.History.Add(new ChatMessage { Role = "user", Content = userMessage });
        
        // Adding a minimal await operation to make the method properly async
        await Task.CompletedTask;
        
        try
        {
            // Validate that we have a selected report and all required parameters
            if (context.SelectedReport == null)
            {
                context.State = AgentState.ReportSelection;
                return new ChatResponse
                {
                    Message = "Let's first select a report. What kind of report are you looking for?",
                    State = AgentState.ReportSelection
                };
            }
            
            // Check for missing required parameters
            var missingParameters = context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !context.ParameterValues.ContainsKey(p.Name))
                .ToList();
            
            if (missingParameters.Count > 0)
            {
                context.State = AgentState.ParameterFilling;
                
                var missingParamsMessage = "I need more information before generating the report:";
                missingParamsMessage += " " + string.Join(", ", missingParameters.Select(p => p.Name));
                
                context.History.Add(new ChatMessage { Role = "assistant", Content = missingParamsMessage });
                
                return new ChatResponse
                {
                    Message = missingParamsMessage,
                    State = AgentState.ParameterFilling
                };
            }
            
            // Generate the report URL
            string reportUrl = _reportUrlService.GenerateReportUrl(context.SelectedReport, context.ParameterValues);
            
            // Save the URL in the context
            context.ReportUrl = reportUrl;
            context.State = AgentState.Completed;
            
            // Build a response message that summarizes the report parameters
            var responseMessage = $"I've generated the {context.SelectedReport.Name} report with the following parameters:";
            
            foreach (var param in context.ParameterValues)
            {
                var paramDef = context.SelectedReport.Parameters.FirstOrDefault(p => p.Name == param.Key);
                if (paramDef != null)
                {
                    responseMessage += $"\n- {param.Key}: {param.Value} ({paramDef.Description})";
                }
                else
                {
                    responseMessage += $"\n- {param.Key}: {param.Value}";
                }
            }
            
            responseMessage += "\n\nThe report is now available. You can view it in the panel below.";
            
            context.History.Add(new ChatMessage { Role = "assistant", Content = responseMessage });
            
            // Return the response with the report URL
            return new ChatResponse
            {
                Message = responseMessage,
                ReportUrl = reportUrl,
                State = AgentState.Completed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReportUrlCreatorAgent");
            return new ChatResponse
            {
                Message = "I'm sorry, but I encountered an error while generating the report URL. Please try again.",
                State = AgentState.ReportUrlCreation
            };
        }
    }
}
