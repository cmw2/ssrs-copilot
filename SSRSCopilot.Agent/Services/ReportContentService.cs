namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Service for retrieving report content from SSRS
/// </summary>
public class ReportContentService : IReportContentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportContentService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportContentService"/> class.
    /// </summary>
    /// <param name="ssrsReportClient">The typed SSRS report client</param>
    /// <param name="logger">The logger</param>
    public ReportContentService(
        SsrsReportClient ssrsReportClient,
        ILogger<ReportContentService> logger)
    {
        _logger = logger;
        _httpClient = ssrsReportClient.Client;
    }

    /// <inheritdoc/>
    public async Task<(byte[] Content, string ContentType)> GetReportContentAsync(string reportUrl)
    {
        try
        {
            _logger.LogInformation("Retrieving report content from URL: {ReportUrl}", reportUrl);
            
            // Create a URI from the report URL
            var uri = new Uri(reportUrl);
            
            // If the URI is relative, combine it with the base address
            if (!uri.IsAbsoluteUri && _httpClient.BaseAddress != null)
            {
                uri = new Uri(_httpClient.BaseAddress, reportUrl);
            }
            
            // Use GetAsync to ensure NTLM authentication is properly applied
            var response = await _httpClient.GetAsync(uri);
            
            // Ensure success
            response.EnsureSuccessStatusCode();
            
            // Get the content
            var content = await response.Content.ReadAsByteArrayAsync();
            
            // Get the content type from the response or default to application/pdf
            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/pdf";
            
            // Force PDF content type if it's likely a PDF
            if (reportUrl.ToLower().Contains("pdf") || 
                reportUrl.ToLower().Contains("format=pdf") ||
                contentType.Contains("application/octet-stream"))
            {
                contentType = "application/pdf";
            }
            
            _logger.LogInformation("Successfully retrieved report content: {ContentLength} bytes with content type: {ContentType}", 
                content.Length, contentType);
            
            return (content, contentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving report content from URL: {ReportUrl}", reportUrl);
            throw;
        }
    }
}
