using Microsoft.AspNetCore.Mvc;
using SSRSCopilot.ApiService.Agents;
using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly FunctionCallingAgentOrchestrator _orchestrator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        FunctionCallingAgentOrchestrator orchestrator,
        ILogger<ChatController> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> PostChatMessage([FromBody] ChatRequest request)
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

            // Process the message
            var response = await _orchestrator.ProcessMessageAsync(request.Message, sessionId);
            
            // Return the response with the session ID
            return Ok(new 
            {
                response.Message,
                response.ReportUrl,
                response.State,
                SessionId = sessionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, "An error occurred while processing your request");
        }
    }
}
