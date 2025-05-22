using SSRSCopilot.Agent.Models;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Interface for interacting with the SSRS REST API
/// </summary>
public interface ISsrsService
{
    /// <summary>
    /// Gets a list of all available reports from the SSRS server
    /// </summary>
    /// <returns>A list of available reports</returns>
    Task<List<SsrsReportModel>> GetReportsAsync();
    
    /// <summary>
    /// Gets a specific report by its ID
    /// </summary>
    /// <param name="id">The ID of the report</param>
    /// <returns>The requested report or null if not found</returns>
    Task<SsrsReportModel?> GetReportByIdAsync(string id);
    
    /// <summary>
    /// Gets the parameters for a specific report
    /// </summary>
    /// <param name="reportId">The id of the report to get parameters for</param>
    /// <returns>A list of parameters for the report</returns>
    Task<List<SsrsReportParameterModel>> GetReportParametersAsync(string reportId);
    
    /// <summary>
    /// Generates a URL for running a report with the specified parameters
    /// </summary>
    /// <param name="report">The report to run</param>
    /// <param name="parameters">The parameter values to use</param>
    /// <returns>A URL that can be used to view the report</returns>
    Task<string> GenerateReportUrlAsync(SsrsReportModel report, Dictionary<string, string> parameters);
}
