namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents a chat request from the client
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The message from the user
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// The session ID to maintain conversation context
    /// </summary>
    public string? SessionId { get; set; }
}
