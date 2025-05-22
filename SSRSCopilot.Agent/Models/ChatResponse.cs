namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents a response from the agent
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
    /// The session ID for the conversation
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
}
