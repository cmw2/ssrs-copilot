using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace SSRSCopilot.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportProxyController : ControllerBase
    {        private readonly HttpClient _httpClient;
        private readonly ILogger<ReportProxyController> _logger;

        public ReportProxyController(IHttpClientFactory httpClientFactory, ILogger<ReportProxyController> logger)
        {
            // Create an HttpClient using the same factory
            _httpClient = httpClientFactory.CreateClient("AgentService");
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> GetReport([FromQuery] string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return BadRequest("URL parameter is required");
            }

            try
            {
                // Forward the request to the Agent service's Report controller
                var encodedUrl = WebUtility.UrlEncode(url);
                var response = await _httpClient.GetAsync($"api/report/content?url={encodedUrl}");
                
                // Ensure success
                response.EnsureSuccessStatusCode();
                
                // Get the content
                var content = await response.Content.ReadAsByteArrayAsync();
                
                // Get the content type from the response or default to application/pdf
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/pdf";
                
                // Log successful proxy
                _logger.LogInformation("Successfully proxied report: {Url} with content type: {ContentType}", 
                    url, contentType);
                  // Create a response with an explicit header to force inline display
                var result = File(content, contentType);
                
                // Force inline display by setting Content-Disposition header
                Response.Headers.Append("Content-Disposition", "inline; filename=\"report.pdf\"");
                
                // Add headers to help with PDF rendering
                if (contentType.Contains("pdf"))
                {
                    // These headers can help with PDF display in iframes
                    Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
                    Response.Headers.Append("Cache-Control", "public, max-age=300");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying report: {Url}", url);
                return StatusCode(500, $"Error proxying report: {ex.Message}");
            }
        }
    }
}
