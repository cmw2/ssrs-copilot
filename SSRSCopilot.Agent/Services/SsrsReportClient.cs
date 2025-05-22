using System.Net.Http.Headers;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Typed HttpClient for SSRS Report Rendering
/// </summary>
public class SsrsReportClient
{
    public HttpClient Client { get; }
    private readonly ILogger<SsrsReportClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsrsReportClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HttpClient provided by DI</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger</param>
    public SsrsReportClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SsrsReportClient> logger)
    {
        _logger = logger;
        
        // Get base URL from configuration - this should be the report viewer URL, not the API
        var reportViewerUrl = configuration["Ssrs:ReportViewerUrl"] 
            ?? throw new InvalidOperationException("Ssrs:ReportViewerUrl configuration is required");
        
        // Configure the base address
        httpClient.BaseAddress = new Uri(reportViewerUrl);
        
        // Configure timeout
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        
        // Configure headers specifically for PDF report retrieval
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/pdf"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));
        
        // Set User-Agent 
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 SSRS-Copilot-Agent");
        
        Client = httpClient;
        
        logger.LogInformation("SsrsReportClient initialized with base URL: {BaseUrl}", httpClient.BaseAddress);
    }
}
