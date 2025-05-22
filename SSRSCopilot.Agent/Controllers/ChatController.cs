using Microsoft.AspNetCore.Mvc;
using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Services;

namespace SSRSCopilot.Agent.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> ProcessMessage([FromBody] ChatRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty");
            }

            // Generate a session ID if not provided
            string sessionId = string.IsNullOrEmpty(request.SessionId) 
                ? Guid.NewGuid().ToString() 
                : request.SessionId;

            // Set the session ID on the request
            request.SessionId = sessionId;

            // Process the message
            var response = await _chatService.ProcessMessageAsync(request);
            
            // Return the response with the session ID
            var chatResponse = new ChatResponse
            {
                Message = response.Message,
                ReportUrl = response.ReportUrl,
                SessionId = sessionId
            };
            
            return Ok(chatResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
