using Microsoft.AspNetCore.Mvc;
using SSRSCopilot.Agent.Services;
using System.Net;

namespace SSRSCopilot.Agent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportController : ControllerBase
    {
        private readonly ISsrsService _ssrsService;
        private readonly IReportContentService _reportContentService;
        private readonly ILogger<ReportController> _logger;

        public ReportController(
            ISsrsService ssrsService, 
            IReportContentService reportContentService,
            ILogger<ReportController> logger)
        {
            _ssrsService = ssrsService;
            _reportContentService = reportContentService;
            _logger = logger;
        }        [HttpGet("content")]
        public async Task<IActionResult> GetReportContent([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("URL parameter is required");
            }

            try
            {
                // Validate and decode the URL
                string decodedUrl = WebUtility.UrlDecode(url);
                
                _logger.LogInformation("Retrieving report content from URL: {ReportUrl}", decodedUrl);
                
                // Use the report content service to get the report content
                var (content, contentType) = await _reportContentService.GetReportContentAsync(decodedUrl);
                
                // Log successful retrieval
                _logger.LogInformation("Successfully retrieved report content: {ContentLength} bytes with content type: {ContentType}", 
                    content.Length, contentType);
                
                // Create a response with an explicit header to force inline display
                var result = File(content, contentType);
                
                // Force inline display by setting Content-Disposition header
                Response.Headers.Append("Content-Disposition", "inline; filename=\"report.pdf\"");
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving report content from URL: {ReportUrl}", url);
                return StatusCode(500, $"Error retrieving report: {ex.Message}");
            }
        }
    }
}
