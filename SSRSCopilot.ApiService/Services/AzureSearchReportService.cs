using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using SSRSCopilot.ApiService.Models;
using System.Text.Json;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Implementation of IReportService using Azure AI Search
/// </summary>
public class AzureSearchReportService : IReportService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<AzureSearchReportService> _logger;
    
    public AzureSearchReportService(SearchClient searchClient, ILogger<AzureSearchReportService> logger)
    {
        _searchClient = searchClient;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<List<Report>> GetReportsAsync()
    {
        try
        {
            SearchResults<Report> response = await _searchClient.SearchAsync<Report>("*");
            return response.GetResults().Select(r => r.Document).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reports from Azure AI Search");
            
            // For development purposes, return some sample reports
            // In production, this would be replaced with actual data from SSRS
            return GetSampleReports();
        }
    }
    
    /// <inheritdoc/>
    public async Task<Report?> GetReportByIdAsync(string id)
    {
        try
        {
            Response<Report> response = await _searchClient.GetDocumentAsync<Report>(id);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching report with ID {ReportId} from Azure AI Search", id);
            
            // For development purposes, return a sample report if ID matches
            return GetSampleReports().FirstOrDefault(r => r.Id == id);
        }
    }
    
    /// <inheritdoc/>
    public async Task<List<Report>> SearchReportsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await GetReportsAsync();
        }
        
        try
        {
            SearchOptions options = new()
            {
                SemanticSearch = new()
                {
                    SemanticConfigurationName = "default",
                    QueryCaption = new(QueryCaptionType.Extractive)
                },
                Size = 10
            };
            
            SearchResults<Report> response = await _searchClient.SearchAsync<Report>(query, options);
            return response.GetResults().Select(r => r.Document).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching reports with query '{Query}' from Azure AI Search", query);
            
            // For development purposes, perform a simple string matching search on sample data
            string normalizedQuery = query.ToLowerInvariant();
            return GetSampleReports()
                .Where(r => 
                    r.Name.ToLowerInvariant().Contains(normalizedQuery) || 
                    r.Description.ToLowerInvariant().Contains(normalizedQuery))
                .ToList();
        }
    }
    
    /// <summary>
    /// Returns sample reports for development purposes
    /// </summary>
    private static List<Report> GetSampleReports()
    {
        return new List<Report>
        {
            new()
            {
                Id = "sales-report-2025",
                Name = "Sales Report 2025",
                Description = "Monthly sales figures across all regions for 2025",
                Path = "/Sales/MonthlyReport",
                Parameters = new List<ReportParameter>
                {
                    new()
                    {
                        Name = "Month",
                        DataType = "string",
                        IsRequired = true,
                        Description = "The month for which to generate the report",
                        AllowedValues = new List<string> { "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" }
                    },
                    new()
                    {
                        Name = "Region",
                        DataType = "string",
                        IsRequired = false,
                        DefaultValue = "All",
                        Description = "The sales region to filter by",
                        AllowedValues = new List<string> { "North", "South", "East", "West", "All" }
                    }
                }
            },
            new()
            {
                Id = "inventory-status-report",
                Name = "Inventory Status Report",
                Description = "Current inventory levels across all warehouses",
                Path = "/Inventory/Status",
                Parameters = new List<ReportParameter>
                {
                    new()
                    {
                        Name = "Warehouse",
                        DataType = "string",
                        IsRequired = false,
                        DefaultValue = "All",
                        Description = "The warehouse to check inventory for",
                        AllowedValues = new List<string> { "Main", "Secondary", "Distribution", "All" }
                    },
                    new()
                    {
                        Name = "Category",
                        DataType = "string",
                        IsRequired = false,
                        DefaultValue = "All",
                        Description = "Product category to filter by",
                        AllowedValues = new List<string> { "Electronics", "Clothing", "Food", "All" }
                    },
                    new()
                    {
                        Name = "InStockOnly",
                        DataType = "boolean",
                        IsRequired = false,
                        DefaultValue = "false",
                        Description = "Show only in-stock items"
                    }
                }
            },
            new()
            {
                Id = "customer-activity-report",
                Name = "Customer Activity Report",
                Description = "Detailed report on customer activity including purchases and returns",
                Path = "/Customers/Activity",
                Parameters = new List<ReportParameter>
                {
                    new()
                    {
                        Name = "CustomerId",
                        DataType = "string",
                        IsRequired = true,
                        Description = "The ID of the customer"
                    },
                    new()
                    {
                        Name = "StartDate",
                        DataType = "datetime",
                        IsRequired = true,
                        Description = "Start date for the activity period (format: YYYY-MM-DD)"
                    },
                    new()
                    {
                        Name = "EndDate",
                        DataType = "datetime",
                        IsRequired = true,
                        Description = "End date for the activity period (format: YYYY-MM-DD)"
                    },
                    new()
                    {
                        Name = "ActivityType",
                        DataType = "string",
                        IsRequired = false,
                        DefaultValue = "All",
                        Description = "Type of activity to include",
                        AllowedValues = new List<string> { "Purchases", "Returns", "Inquiries", "All" }
                    }
                }
            }
        };
    }
}
