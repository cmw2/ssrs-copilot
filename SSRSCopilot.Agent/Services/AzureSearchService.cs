using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SSRSCopilot.Agent.Models;
using System.Text.Json;
using Polly;
using Polly.Retry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.AI;
using System.Text;
using Microsoft.Extensions.Options;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Implementation of the Azure Search Service for finding reports using
/// hybrid search capabilities including semantic search and vector search
/// </summary>
public class AzureSearchService : IAzureSearchService
{
    private readonly SearchClient _searchClient;
    private readonly ILogger<AzureSearchService> _logger;
    private readonly string _semanticConfigurationName;
    private readonly AsyncRetryPolicy _retryPolicy;
    private readonly bool _vectorSearchEnabled;
    private readonly IEmbeddingGenerator<string, Embedding<float>>? _embeddingService;
    private readonly AzureSearchFieldMapping _fieldMapping;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureSearchService"/> class.
    /// </summary>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger</param>
    /// <param name="serviceProvider">The service provider for accessing registered services</param>
    public AzureSearchService(
        IConfiguration configuration, 
        ILogger<AzureSearchService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;

        // Load field mapping configuration
        _fieldMapping = new AzureSearchFieldMapping();
        configuration.GetSection("AzureSearch:FieldMapping").Bind(_fieldMapping);
        _fieldMapping.Validate(); // Ensure required fields are configured

        var endpoint = configuration["AzureSearch:Endpoint"] 
            ?? throw new InvalidOperationException("AzureSearch:Endpoint configuration is required");
        
        var key = configuration["AzureSearch:ApiKey"] 
            ?? throw new InvalidOperationException("AzureSearch:ApiKey configuration is required");
        
        var indexName = configuration["AzureSearch:IndexName"] 
            ?? throw new InvalidOperationException("AzureSearch:IndexName configuration is required");
            
        // Get semantic configuration name, default to "azureml-default" if not specified
        _semanticConfigurationName = configuration["AzureSearch:SemanticConfigurationName"] ?? "azureml-default";
        
        // Get vector search settings
        if (bool.TryParse(configuration["AzureSearch:VectorSearchEnabled"], out bool vectorSearchEnabled))
        {
            _vectorSearchEnabled = vectorSearchEnabled;
            
            // If vector search is enabled, try to get the embedding service from the service provider
            if (_vectorSearchEnabled)
            {
                try
                {
                    _embeddingService = serviceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();
                    if (_embeddingService == null)
                    {
                        _logger.LogWarning("Vector search is enabled but embedding service is not registered. Vector search will be disabled.");
                        _vectorSearchEnabled = false;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error retrieving embedding service. Vector search will be disabled.");
                    _vectorSearchEnabled = false;
                }
            }
        }

        _searchClient = new SearchClient(
            new Uri(endpoint), 
            indexName, 
            new AzureKeyCredential(key));
            
        // Define a retry policy for transient failures
        _retryPolicy = Policy
            .Handle<RequestFailedException>(ex => ex.Status == 429 || (ex.Status >= 500 && ex.Status < 600))
            .WaitAndRetryAsync(
                3, // Number of retries
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, 
                        "Request to Azure Search failed (attempt {RetryCount}). Retrying in {RetryTimeSpan}.", 
                        retryCount, timeSpan);
                });
    }

    /// <inheritdoc/>
    public async Task<List<SearchDocument>> SearchReportsAsync(string query)
    {
        try
        {
            _logger.LogInformation("Searching for reports with query: {Query}", query);

            // Set up search options for hybrid semantic search
            var searchOptions = new SearchOptions
            {
                IncludeTotalCount = true,
                Size = 20, // Increase to get more results, since we may have multiple chunks per document
                QueryType = SearchQueryType.Semantic, // Use semantic search capabilities
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = _semanticConfigurationName
                }
            };

            // Add select fields to include - use configurable field names
            searchOptions.Select.Add(_fieldMapping.IdField);
            searchOptions.Select.Add(_fieldMapping.TitleField);
            searchOptions.Select.Add(_fieldMapping.ContentField);
            
            // Add optional fields if they are configured
            if (!string.IsNullOrWhiteSpace(_fieldMapping.UrlField))
                searchOptions.Select.Add(_fieldMapping.UrlField);
            if (!string.IsNullOrWhiteSpace(_fieldMapping.FilePathField))
                searchOptions.Select.Add(_fieldMapping.FilePathField);
            if (!string.IsNullOrWhiteSpace(_fieldMapping.MetadataField))
                searchOptions.Select.Add(_fieldMapping.MetadataField);
            if (!string.IsNullOrWhiteSpace(_fieldMapping.ParentIdField))
                searchOptions.Select.Add(_fieldMapping.ParentIdField);
            
            // Enable vector search if configured
            if (_vectorSearchEnabled && _embeddingService != null)
            {
                try
                {
                    var embedding = await _embeddingService.GenerateAsync(query);
                    if (embedding != null)
                    {
                        searchOptions.VectorSearch = new VectorSearchOptions
                        {
                            Queries = { new VectorizedQuery(embedding.Vector) 
                            { 
                                KNearestNeighborsCount = 20,
                                Fields = { _fieldMapping.VectorField ?? "contentVector" } 
                            }}
                        };
                        _logger.LogInformation("Vector search enabled with {VectorSize} dimensions", embedding.Vector.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Vector search could not be enabled. Falling back to semantic search only.");
                }
            }

            // Execute the search with retry pattern for transient failures
            Response<SearchResults<SearchDocument>> response = await ExecuteWithRetryAsync(() => 
                _searchClient.SearchAsync<SearchDocument>(query, searchOptions));
            
            var searchResult = response.Value;
            
            // Simply return the search documents directly
            return searchResult.GetResults().Select(result => result.Document).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for reports with query: {Query}", query);
            return new List<SearchDocument>();
        }
    }

    /// <inheritdoc/>
    public async Task<string> GetReportDocumentationAsync(string reportName)
    {
        try
        {
            _logger.LogInformation("Getting documentation for report: {ReportName}", reportName);

            // Create a semantic search query to find the report by name with semantic capabilities
            var searchOptions = new SearchOptions
            {
                Size = 5, // Get multiple chunks that may be related to this report
                QueryType = SearchQueryType.Semantic, // Use semantic search
                SemanticSearch = new SemanticSearchOptions
                {
                    SemanticConfigurationName = _semanticConfigurationName
                }
            };

            // Add fields to select using configurable field names
            searchOptions.Select.Add(_fieldMapping.ContentField); // Report documentation is expected to be in the content field
            
            // Add optional fields if they are configured
            if (!string.IsNullOrWhiteSpace(_fieldMapping.UrlField))
                searchOptions.Select.Add(_fieldMapping.UrlField);  // May contain the report name/path
            if (!string.IsNullOrWhiteSpace(_fieldMapping.FilePathField))
                searchOptions.Select.Add(_fieldMapping.FilePathField); // May contain the report name/path
            if (!string.IsNullOrWhiteSpace(_fieldMapping.TitleField))
                searchOptions.Select.Add(_fieldMapping.TitleField); // May contain report name
            if (!string.IsNullOrWhiteSpace(_fieldMapping.MetadataField))
                searchOptions.Select.Add(_fieldMapping.MetadataField); // Additional metadata

            // Create a simple search text that will match across fields
            string searchText = reportName;

            Response<SearchResults<SearchDocument>> response = await ExecuteWithRetryAsync(() => 
                _searchClient.SearchAsync<SearchDocument>(searchText, searchOptions));
                
            var searchResult = response.Value;
            
            // If no results found
            if (!searchResult.GetResults().Any())
            {
                return "No detailed documentation available for this report.";
            }

            // Combine content from all results using configurable field name
            var documentationBuilder = new StringBuilder();
            foreach (var result in searchResult.GetResults())
            {
                if (result.Document.ContainsKey(_fieldMapping.ContentField) && result.Document[_fieldMapping.ContentField] != null)
                {
                    documentationBuilder.AppendLine(result.Document[_fieldMapping.ContentField]?.ToString() ?? "");
                    documentationBuilder.AppendLine();
                }
            }

            string documentation = documentationBuilder.ToString().Trim();
            return string.IsNullOrEmpty(documentation) 
                ? "No detailed documentation available for this report." 
                : documentation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documentation for report: {ReportName}", reportName);
            return "Unable to retrieve documentation due to an error.";
        }
    }

    /// <summary>
    /// Executes a function with retry logic using the defined retry policy
    /// </summary>
    /// <typeparam name="T">The return type of the function</typeparam>
    /// <param name="action">The function to execute</param>
    /// <returns>The result of the function execution</returns>
    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action)
    {
        return await _retryPolicy.ExecuteAsync(action);
    }

    /// <summary>
    /// Extracts the filename from a path string
    /// </summary>
    /// <param name="path">The path string</param>
    /// <returns>The extracted filename or null if path is null</returns>
    private string? GetFilenameFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        
        // Extract just the filename without path
        return Path.GetFileName(path);
    }

}
