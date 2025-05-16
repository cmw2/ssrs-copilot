using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Main orchestrator for the new function-calling based agent system
/// </summary>
public class FunctionCallingAgentOrchestrator
{
    private readonly FunctionCallingAgent _functionCallingAgent;
    private readonly ChitchatAgent _chitchatAgent;
    private readonly ILogger<FunctionCallingAgentOrchestrator> _logger;
    
    public FunctionCallingAgentOrchestrator(
        FunctionCallingAgent functionCallingAgent,
        ChitchatAgent chitchatAgent,
        ILogger<FunctionCallingAgentOrchestrator> logger)
    {
        _functionCallingAgent = functionCallingAgent;
        _chitchatAgent = chitchatAgent;
        _logger = logger;
    }
    
    /// <summary>
    /// Processes a user message and generates a response
    /// </summary>
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
            
            // Process the message through the function calling agent
            var response = await _functionCallingAgent.ProcessMessageAsync(userMessage, context);
            
            // Save the updated context
            SaveContext(sessionId, context);
            
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FunctionCallingAgentOrchestrator.ProcessMessageAsync");
            
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
        return chitchatPatterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(normalizedMessage, pattern));
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
