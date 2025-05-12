using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Interface for the service that generates SSRS report URLs
/// </summary>
public interface IReportUrlService
{
    /// <summary>
    /// Generates a URL for viewing an SSRS report with the specified parameters
    /// </summary>
    /// <param name="report">The report to generate a URL for</param>
    /// <param name="parameters">The parameter values for the report</param>
    /// <param name="format">The format of the report (default: PDF)</param>
    /// <returns>The URL to view the report</returns>
    string GenerateReportUrl(Report report, Dictionary<string, string> parameters, string format = "PDF");
}
