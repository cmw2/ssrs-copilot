namespace SSRSCopilot.Web.Models;

/// <summary>
/// Represents a response from the chat API
/// </summary>
public class ChatResponse
{
    /// <summary>
    /// The message from the agent
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The URL to display in the iframe, if available
    /// </summary>
    public string? ReportUrl { get; set; }
    
    /// <summary>
    /// The current state of the agent workflow
    /// </summary>
    public AgentState State { get; set; }
    
    /// <summary>
    /// The session ID for the conversation
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}
