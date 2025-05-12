using SSRSCopilot.ApiService.Models;
using System.Text.RegularExpressions;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Orchestrator for managing the flow between agents
/// </summary>
public class AgentOrchestrator
{
    private readonly ReportSelectorAgent _reportSelectorAgent;
    private readonly ParameterFillerAgent _parameterFillerAgent;
    private readonly ReportUrlCreatorAgent _reportUrlCreatorAgent;
    private readonly ChitchatAgent _chitchatAgent;
    private readonly ILogger<AgentOrchestrator> _logger;
    
    public AgentOrchestrator(
        ReportSelectorAgent reportSelectorAgent,
        ParameterFillerAgent parameterFillerAgent,
        ReportUrlCreatorAgent reportUrlCreatorAgent,
        ChitchatAgent chitchatAgent,
        ILogger<AgentOrchestrator> logger)
    {
        _reportSelectorAgent = reportSelectorAgent;
        _parameterFillerAgent = parameterFillerAgent;
        _reportUrlCreatorAgent = reportUrlCreatorAgent;
        _chitchatAgent = chitchatAgent;
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
            // Check if this is a chitchat message
            if (IsChitchatMessage(userMessage))
            {
                // Process through the chitchat agent but don't change state
                return await _chitchatAgent.ProcessMessageAsync(userMessage, context);
            }
            
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
    /// Determines if a message is likely to be chitchat (greeting, etc.)
    /// </summary>
    private bool IsChitchatMessage(string message)
    {
        // Normalize the message for case-insensitive comparison
        string normalizedMessage = message.ToLowerInvariant().Trim();
        
        // Check for common greetings and chitchat patterns
        string[] chitchatPatterns = 
        {
            "^hi$", "^hello$", "^hey$", "^hi there$", "^hello there$", "^hey there$",
            "^good morning$", "^good afternoon$", "^good evening$", "^howdy$",
            "^how are you$", "^how are you doing$", "^how's it going$", "^what's up$",
            "^greetings$", "^yo$", "^hiya$", "^sup$", "^test$", "^testing$",
            "^are you there$", "^you there$", "^anybody home$", "^anyone there$"
        };
        
        // Check if the message matches any of the chitchat patterns
        return chitchatPatterns.Any(pattern => Regex.IsMatch(normalizedMessage, pattern));
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
