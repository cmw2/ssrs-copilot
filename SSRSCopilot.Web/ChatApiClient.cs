using SSRSCopilot.Web.Models;
using System.Net.Http.Json;

namespace SSRSCopilot.Web;

/// <summary>
/// Client for communicating with the Chat API
/// </summary>
public class ChatApiClient
{
    private readonly HttpClient _httpClient;

    public ChatApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
    }
    
    /// <summary>
    /// Sends a chat message to the API
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <param name="sessionId">The session ID, if any</param>
    /// <returns>The chat response</returns>
    public async Task<ChatResponse> SendMessageAsync(string message, string? sessionId = null)
    {
        var request = new ChatRequest
        {
            Message = message,
            SessionId = sessionId
        };
        
        var response = await _httpClient.PostAsJsonAsync("api/chat", request);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<ChatResponse>() 
            ?? throw new Exception("Failed to deserialize chat response");
    }
}
