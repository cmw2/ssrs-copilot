using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Interface for agent implementations
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Processes a chat message and updates the chat context
    /// </summary>
    /// <param name="userMessage">The message from the user</param>
    /// <param name="context">The chat context</param>
    /// <returns>A response from the agent</returns>
    Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context);
}
