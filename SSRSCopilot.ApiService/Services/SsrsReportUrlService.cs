using System.Text;
using System.Web;
using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Implementation of IReportUrlService for generating SSRS report URLs
/// </summary>
public class SsrsReportUrlService : IReportUrlService
{
    private readonly IConfiguration _configuration;
    
    public SsrsReportUrlService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    /// <inheritdoc/>
    public string GenerateReportUrl(Report report, Dictionary<string, string> parameters, string format = "PDF")
    {
        string baseUrl = _configuration["SSRS:ServerUrl"] ?? "https://ssrs.example.com/ReportServer";
        
        // Ensure baseUrl doesn't have a trailing slash
        baseUrl = baseUrl.TrimEnd('/');
        
        // Start building the URL
        StringBuilder urlBuilder = new StringBuilder($"{baseUrl}");
        
        // Add the report path
        string reportPath = report.Path.StartsWith("/") ? report.Path : "/" + report.Path;
        urlBuilder.Append(HttpUtility.UrlEncode(reportPath));
        
        // Add the format
        urlBuilder.Append($"?rs:Format={format}");
        
        // Add the parameters
        foreach (var param in parameters)
        {
            urlBuilder.Append($"&{HttpUtility.UrlEncode(param.Key)}={HttpUtility.UrlEncode(param.Value)}");
        }
        
        return urlBuilder.ToString();
    }
}
