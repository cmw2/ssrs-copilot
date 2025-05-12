namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents a chat message in the conversation
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// The role of the message sender (e.g., user, assistant)
    /// </summary>
    public string Role { get; set; } = string.Empty;
    
    /// <summary>
    /// The content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;
}
