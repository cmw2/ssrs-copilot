using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Orchestrator for managing the flow between agents
/// </summary>
public class AgentOrchestrator
{
    private readonly ReportSelectorAgent _reportSelectorAgent;
    private readonly ParameterFillerAgent _parameterFillerAgent;
    private readonly ReportUrlCreatorAgent _reportUrlCreatorAgent;
    private readonly ILogger<AgentOrchestrator> _logger;
    
    public AgentOrchestrator(
        ReportSelectorAgent reportSelectorAgent,
        ParameterFillerAgent parameterFillerAgent,
        ReportUrlCreatorAgent reportUrlCreatorAgent,
        ILogger<AgentOrchestrator> logger)
    {
        _reportSelectorAgent = reportSelectorAgent;
        _parameterFillerAgent = parameterFillerAgent;
        _reportUrlCreatorAgent = reportUrlCreatorAgent;
        _logger = logger;
    }
    
    /// <summary>
    /// Processes a user message through the appropriate agent based on the current state
    /// </summary>
    /// <param name="userMessage">The message from the user</param>
    /// <param name="sessionId">The session ID, which is used to retrieve the conversation context</param>
    /// <returns>A chat response from the appropriate agent</returns>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, string sessionId)
    {
        // Get or create a chat context for this session
        var context = GetOrCreateContext(sessionId);
        
        try
        {
            // Route the message to the appropriate agent based on the current state
            ChatResponse response = context.State switch
            {
                AgentState.ReportSelection => await _reportSelectorAgent.ProcessMessageAsync(userMessage, context),
                AgentState.ParameterFilling => await _parameterFillerAgent.ProcessMessageAsync(userMessage, context),
                AgentState.ReportUrlCreation => await _reportUrlCreatorAgent.ProcessMessageAsync(userMessage, context),
                AgentState.Completed => await HandleCompletedStateAsync(userMessage, context),
                _ => throw new InvalidOperationException($"Unexpected agent state: {context.State}")
            };
            
            // Update the context in the session store
            SaveContext(sessionId, context);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AgentOrchestrator.ProcessMessageAsync");
            
            return new ChatResponse
            {
                Message = "I'm sorry, but something went wrong. Let's start over. What kind of report are you looking for?",
                State = AgentState.ReportSelection
            };
        }
    }
    
    /// <summary>
    /// Handles messages when the conversation is in the Completed state
    /// </summary>
    private async Task<ChatResponse> HandleCompletedStateAsync(string userMessage, ChatContext context)
    {
        // Check if the user wants to start over or modify the report
        string normalizedMessage = userMessage.ToLowerInvariant();
        
        if (normalizedMessage.Contains("start over") || 
            normalizedMessage.Contains("new report") || 
            normalizedMessage.Contains("different report"))
        {
            // Reset the context and start over with report selection
            context.SelectedReport = null;
            context.ParameterValues.Clear();
            context.ReportUrl = null;
            context.State = AgentState.ReportSelection;
            
            return await _reportSelectorAgent.ProcessMessageAsync("I need a new report", context);
        }
        
        if (normalizedMessage.Contains("change parameter") || 
            normalizedMessage.Contains("update parameter") || 
            normalizedMessage.Contains("different parameter"))
        {
            // Go back to parameter filling
            context.State = AgentState.ParameterFilling;
            return await _parameterFillerAgent.ProcessMessageAsync(userMessage, context);
        }
        
        // Default: return the already generated report URL
        return new ChatResponse
        {
            Message = "The report is already generated and displayed below. If you'd like to start over with a new report or change parameters, just let me know.",
            ReportUrl = context.ReportUrl,
            State = AgentState.Completed
        };
    }
    
    // In a real application, these would use a distributed cache, database, or state store
    // For simplicity, we're using an in-memory dictionary here
    private static readonly Dictionary<string, ChatContext> _sessionStore = new();
    
    /// <summary>
    /// Gets or creates a chat context for the given session ID
    /// </summary>
    private static ChatContext GetOrCreateContext(string sessionId)
    {
        if (!_sessionStore.TryGetValue(sessionId, out var context))
        {
            context = new ChatContext();
            _sessionStore[sessionId] = context;
        }
        
        return context;
    }
    
    /// <summary>
    /// Saves the chat context for the given session ID
    /// </summary>
    private static void SaveContext(string sessionId, ChatContext context)
    {
        _sessionStore[sessionId] = context;
    }
}
