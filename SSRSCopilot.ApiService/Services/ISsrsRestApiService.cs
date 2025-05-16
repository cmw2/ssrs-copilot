using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Interface for the service that interacts with SQL Reporting Services REST API
/// </summary>
public interface ISsrsRestApiService
{
    /// <summary>
    /// Gets all reports from the SSRS server
    /// </summary>
    /// <returns>A list of reports</returns>
    Task<List<Report>> GetReportsAsync();
    
    /// <summary>
    /// Gets a specific report by ID
    /// </summary>
    /// <param name="reportId">The ID of the report</param>
    /// <returns>The report details</returns>
    Task<Report?> GetReportAsync(string reportId);
    
    /// <summary>
    /// Gets a report by name (performs name matching)
    /// </summary>
    /// <param name="reportName">The name of the report to find</param>
    /// <returns>The report if found, null otherwise</returns>
    Task<Report?> GetReportByNameAsync(string reportName);
    
    /// <summary>
    /// Gets the parameter definitions for a specific report
    /// </summary>
    /// <param name="reportId">The ID of the report</param>
    /// <returns>A list of report parameters</returns>
    Task<List<ReportParameter>> GetReportParameterDefinitionsAsync(string reportId);
}
