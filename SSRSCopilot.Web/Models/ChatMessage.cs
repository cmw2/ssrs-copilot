namespace SSRSCopilot.Web.Models;

/// <summary>
/// Represents a chat message in the UI
/// </summary>
public class ChatMessage
{
    /// <summary>
    /// Indicates whether the message is from the user or the system
    /// </summary>
    public bool IsUser { get; set; }
    
    /// <summary>
    /// The content of the message
    /// </summary>
    public string Content { get; set; } = string.Empty;
    
    /// <summary>
    /// The timestamp of the message
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
