using Azure.Search.Documents.Models;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Interface for searching reports in Azure Search
/// </summary>
public interface IAzureSearchService
{
    /// <summary>
    /// Searches for reports based on user query
    /// </summary>
    /// <param name="query">The search query</param>
    /// <returns>A list of search documents with relevant information</returns>
    Task<List<SearchDocument>> SearchReportsAsync(string query);
    
    /// <summary>
    /// Gets detailed documentation about a specific report
    /// </summary>
    /// <param name="reportName">The name of the report</param>
    /// <returns>Detailed documentation text about the report</returns>
    Task<string> GetReportDocumentationAsync(string reportName);
}
