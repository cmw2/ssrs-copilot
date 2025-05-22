using Microsoft.AspNetCore.Mvc;
using System;
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
                // Remove any cache-busting parameters (_cb, _ts) before forwarding the request
                // as SSRS doesn't understand these parameters
                string cleanUrl = RemoveCacheBustingParameters(url);
                
                // Forward the request to the Agent service's Report controller
                var encodedUrl = WebUtility.UrlEncode(cleanUrl);
                var response = await _httpClient.GetAsync($"api/report/content?url={encodedUrl}");
                
                // Ensure success
                response.EnsureSuccessStatusCode();
                
                // Get the content
                var content = await response.Content.ReadAsByteArrayAsync();
                
                // Get the content type from the response or default to application/pdf
                var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/pdf";
                
                // Log successful proxy
                _logger.LogInformation("Successfully proxied report: {Url} with content type: {ContentType}", 
                    cleanUrl, contentType);
                    // Create a response with the content and content type
                var result = File(content, contentType);
                
                // Copy all relevant headers from the original response
                if (response.Headers.TryGetValues("Content-Disposition", out var contentDisposition))
                {
                    Response.Headers.Append("Content-Disposition", contentDisposition.First());
                }
                else
                {
                    // Fallback if no Content-Disposition header is provided
                    Response.Headers.Append("Content-Disposition", "inline; filename=\"report.pdf\"");
                }
                
                // Copy cache control headers
                if (response.Headers.TryGetValues("Cache-Control", out var cacheControl))
                {
                    Response.Headers.Append("Cache-Control", cacheControl.First());
                }
                else
                {
                    // Fallback cache control
                    Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0");
                }
                
                if (response.Headers.TryGetValues("Pragma", out var pragma))
                {
                    Response.Headers.Append("Pragma", pragma.First());
                }
                else
                {
                    Response.Headers.Append("Pragma", "no-cache");
                }
                
                if (response.Headers.TryGetValues("Expires", out var expires))
                {
                    Response.Headers.Append("Expires", expires.First());
                }
                else
                {
                    Response.Headers.Append("Expires", "0");
                }
                
                if (response.Headers.TryGetValues("ETag", out var etag))
                {
                    Response.Headers.Append("ETag", etag.First());
                }
                
                if (response.Headers.TryGetValues("Vary", out var vary))
                {
                    Response.Headers.Append("Vary", vary.First());
                }
                
                // Add headers to help with PDF rendering in iframes
                if (contentType.Contains("pdf"))
                {
                    Response.Headers.Append("X-Content-Type-Options", "nosniff");
                    Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error proxying report: {Url}", url);
                return StatusCode(500, $"Error proxying report: {ex.Message}");
            }
        }
        
        private string RemoveCacheBustingParameters(string url)
        {
            try
            {
                // If there are no query parameters, return the URL as is
                if (!url.Contains("?"))
                    return url;
                
                // Split the URL into base and query parts
                var parts = url.Split('?');
                if (parts.Length != 2)
                    return url; // Malformed URL, return as is
                
                var baseUrl = parts[0];
                var queryString = parts[1];
                  // Split the query string into individual parameters
                var parameters = queryString.Split('&');
                
                // Filter out our cache-busting parameters
                var filteredParams = parameters.Where(p => 
                    !p.StartsWith("_cb=") && 
                    !p.StartsWith("_ts=") && 
                    !p.StartsWith("_proxyts="));
                
                // Reconstruct the URL
                var newQueryString = string.Join("&", filteredParams);
                
                if (string.IsNullOrEmpty(newQueryString))
                    return baseUrl;
                
                return $"{baseUrl}?{newQueryString}";
            }
            catch
            {
                // If any error occurs during parsing, return the original URL
                return url;
            }
        }
    }
}
