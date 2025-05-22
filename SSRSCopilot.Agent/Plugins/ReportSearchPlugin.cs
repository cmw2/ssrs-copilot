using SSRSCopilot.Agent.Services;
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Azure.Search.Documents.Models;

namespace SSRSCopilot.Agent.Plugins;

/// <summary>
/// Plugin for searching reports in Azure Search using hybrid semantic and vector search capabilities
/// </summary>
public class ReportSearchPlugin
{
    private readonly IAzureSearchService _azureSearchService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportSearchPlugin"/> class.
    /// </summary>
    /// <param name="azureSearchService">The Azure Search service</param>
    public ReportSearchPlugin(IAzureSearchService azureSearchService)
    {
        _azureSearchService = azureSearchService;
    }

    /// <summary>
    /// Searches for reports based on the user's description using semantic search capabilities
    /// </summary>
    /// <param name="query">The search query describing what the user is looking for</param>
    /// <returns>A list of search documents matching the query</returns>
    [KernelFunction]
    [Description("Search for reports based on the user's description using advanced semantic search. Note that the titles in these search results may differ from the actual SSRS report names - you will need to perform semantic matching later.")]
    public async Task<List<SearchDocument>> SearchReportsAsync(string query)
    {
        return await _azureSearchService.SearchReportsAsync(query);
    }

    /// <summary>
    /// Gets detailed documentation about a specific report
    /// </summary>
    /// <param name="reportName">The name of the report</param>
    /// <returns>Detailed documentation text about the report</returns>
    [KernelFunction]
    [Description("Get detailed documentation about a specific report by its title as it appears in the documentation. This title may differ from the actual SSRS report name - you will need to perform semantic matching between this documentation title and the actual SSRS report names.")]
    public async Task<string> GetReportDocumentationAsync(string reportName)
    {
        return await _azureSearchService.GetReportDocumentationAsync(reportName);
    }
}
