using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Interface for the report service that fetches report metadata
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Gets a list of all available reports
    /// </summary>
    /// <returns>A list of reports</returns>
    Task<List<Report>> GetReportsAsync();
    
    /// <summary>
    /// Searches for reports matching the given query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <returns>A list of matching reports</returns>
    Task<List<Report>> SearchReportsAsync(string query);
    
    /// <summary>
    /// Gets a report by its ID
    /// </summary>
    /// <param name="id">The report ID</param>
    /// <returns>The report, if found</returns>
    Task<Report?> GetReportByIdAsync(string id);
}
