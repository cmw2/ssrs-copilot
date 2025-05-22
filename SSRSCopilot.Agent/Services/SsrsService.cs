using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SSRSCopilot.Agent.Models;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Implementation of the SSRS Service for interacting with SQL Server Reporting Services
/// </summary>
public class SsrsService : ISsrsService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SsrsService> _logger;
    private readonly string _reportViewerUrl;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsrsService"/> class.
    /// </summary>
    /// <param name="ssrsHttpClient">The typed SSRS HTTP client</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger</param>
    public SsrsService(
        SsrsHttpClient ssrsHttpClient,
        IConfiguration configuration,
        ILogger<SsrsService> logger)
    {
        _logger = logger;
        _httpClient = ssrsHttpClient.Client;
        
        _reportViewerUrl = configuration["Ssrs:ReportViewerUrl"] 
            ?? throw new InvalidOperationException("Ssrs:ReportViewerUrl configuration is required");

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        _logger.LogInformation("SsrsService initialized with base URL: {BaseUrl}", _httpClient.BaseAddress);
    }

    /// <inheritdoc/>
    public async Task<List<SsrsReportModel>> GetReportsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching all reports from SSRS");
            
            // Check if the HttpClient has credentials configured
            if (_httpClient is HttpClient client)
            {
                _logger.LogDebug("Checking HTTP client authentication configuration");
                var handler = client.GetType().GetField("_handler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(client);
                
                if (handler != null)
                {
                    _logger.LogDebug("Handler type: {HandlerType}", handler.GetType().FullName);
                    
                    // Try to examine if we have an authentication header
                    if (client.DefaultRequestHeaders.Authorization != null)
                    {
                        _logger.LogDebug("Authentication header found: {Scheme}", client.DefaultRequestHeaders.Authorization.Scheme);
                    }
                    else
                    {
                        _logger.LogWarning("No authentication header found in client default headers");
                    }
                }
            }

            var response = await _httpClient.GetAsync("reports");
            
            // Log the authentication challenge details before ensuring success
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Received 401 Unauthorized response");
                
                if (response.Headers.WwwAuthenticate.Any())
                {
                    foreach (var challenge in response.Headers.WwwAuthenticate)
                    {
                        _logger.LogInformation("Authentication challenge: {Scheme} {Parameter}", 
                            challenge.Scheme, challenge.Parameter);
                    }
                }
            }
            
            response.EnsureSuccessStatusCode();

            // Deserialize the OData response
            var odataResponse = await response.Content.ReadFromJsonAsync<ODataResponse<SsrsReportModel>>(_jsonOptions);
            
            if (odataResponse?.Value == null)
            {
                _logger.LogWarning("No reports found or invalid response format");
                return new List<SsrsReportModel>();
            }
            
            _logger.LogInformation("Successfully retrieved {Count} reports from SSRS", odataResponse.Value.Count);
            return odataResponse.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reports from SSRS");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SsrsReportModel?> GetReportByIdAsync(string id)
    {
        try
        {
            _logger.LogInformation("Fetching report by ID: {ReportId}", id);

            // Ensure the ID is properly formatted for the API call
            // The ID should be in the format '/path/to/report'
            var reportId = id.StartsWith('/') ? id.TrimStart('/') : id;
            
            // The OData endpoint for getting a report by ID
            var response = await _httpClient.GetAsync($"reports({reportId})");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Report not found by ID: {ReportId}, Status: {StatusCode}", id, response.StatusCode);
                return null;
            }

            // Deserialize the response
            var ssrsReport = await response.Content.ReadFromJsonAsync<SsrsReportModel>(_jsonOptions);
            
            if (ssrsReport == null)
            {
                _logger.LogWarning("Failed to deserialize report with ID: {ReportId}", id);
                return null;
            }
            
            _logger.LogInformation("Successfully retrieved report with ID: {ReportId}, Name: {ReportName}", 
                id, ssrsReport.Name);
            return ssrsReport;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching report by ID: {ReportId}", id);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<List<SsrsReportParameterModel>> GetReportParametersAsync(string reportId)
    {
        try
        {
            _logger.LogInformation("Fetching parameters for report: {reportId}", reportId);

            SsrsReportModel? report = await GetReportByIdAsync(reportId);
            if (report == null)
            {
                _logger.LogWarning("Report not found for ID: {ReportId}", reportId);
                return new List<SsrsReportParameterModel>();
            }
            
            // For parameters, the OData URL format is /Reports('{id}')/Parameters
            // The ID needs to be enclosed in single quotes
            //var reportId = report.Id.TrimStart('/');
            var response = await _httpClient.GetAsync($"reports({reportId})/ParameterDefinitions");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch parameters for report: {ReportName}, Status: {StatusCode}", 
                    report.Name, response.StatusCode);
                return new List<SsrsReportParameterModel>();
            }

            // Deserialize the OData response
            var odataResponse = await response.Content.ReadFromJsonAsync<ODataResponse<SsrsReportParameterModel>>(_jsonOptions);
            
            if (odataResponse?.Value == null)
            {
                _logger.LogWarning("No parameters found for report: {ReportName}", report.Name);
                return new List<SsrsReportParameterModel>();
            }
            
            return odataResponse.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching parameters for report: {reportId}", reportId);
            return new List<SsrsReportParameterModel>();
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateReportUrlAsync(SsrsReportModel report, Dictionary<string, string> parameterValues)
    {
        try
        {
            _logger.LogInformation("Generating URL for report: {ReportName} with {ParameterCount} parameters", 
                report.Name, parameterValues.Count);

            // Verify that all required parameters are provided
            var parameters = await GetReportParametersAsync(report.Id);
            
            // Create a set of valid parameter names for quick lookup
            var validParameterNames = new HashSet<string>(parameters.Select(p => p.Name));
            
            // Remove rs:Format from the valid parameter list since we'll always add it automatically
            // validParameterNames.Add("rs:Format");
            
            // Check for invalid parameters
            var invalidParameters = parameterValues.Keys
                .Where(key => !validParameterNames.Contains(key))
                .ToList();
                
            if (invalidParameters.Any())
            {
                var invalidNames = string.Join(", ", invalidParameters);
                throw new ArgumentException($"Invalid parameters provided: {invalidNames}");
            }
            
            var missingParameters = parameters
                .Where(p => p.IsRequired && (!parameterValues.ContainsKey(p.Name) || string.IsNullOrEmpty(parameterValues[p.Name])))
                .ToList();

            if (missingParameters.Any())
            {
                var missingNames = string.Join(", ", missingParameters.Select(p => p.Name));
                throw new ArgumentException($"Missing required parameters: {missingNames}");
            }

            // Format the path according to SSRS URL access syntax requirements
            // The pathinfo for native mode should begin with a slash
            var reportPath = report.Path ?? $"/{report.Name}";
            if (!reportPath.StartsWith("/"))
            {
                reportPath = $"/{reportPath}";
            }
            
            // Build the parameter query string
            var queryStringBuilder = new StringBuilder();
            
            // Add the required rs:Command=Render parameter first
            queryStringBuilder.Append("&rs:Command=Render");
            
            // Get a dictionary to quickly look up parameter information
            var parameterDict = parameters.ToDictionary(p => p.Name);
            
            // Process all report parameters from the parameter definitions
            foreach (var parameter in parameters)
            {
                string paramValue = string.Empty;
                bool useDefaultValue = false;
                
                // Check if a value was provided for this parameter
                if (parameterValues.TryGetValue(parameter.Name, out var providedValue))
                {
                    // Check if the provided value is the same as the default value (if any)
                    if (!parameter.DefaultValuesIsNull && 
                        parameter.DefaultValues.Count > 0 && 
                        parameter.DefaultValues[0] == providedValue)
                    {
                        // The provided value is the same as the default, no need to include it
                        useDefaultValue = true;
                        _logger.LogDebug("Parameter {ParameterName} using default value: {DefaultValue}", 
                            parameter.Name, providedValue);
                    }
                    else
                    {
                        // Use the provided value (even if it's empty)
                        paramValue = providedValue;
                    }
                }
                else if (!parameter.DefaultValuesIsNull && parameter.DefaultValues.Count > 0)
                {
                    // Parameter not provided but has a default value, no need to include it
                    useDefaultValue = true;
                    _logger.LogDebug("Parameter {ParameterName} using default value: {DefaultValue}", 
                        parameter.Name, parameter.DefaultValues[0]);
                }
                else
                {
                    // Parameter not provided and no default value, include as empty
                    paramValue = string.Empty;
                }
                
                // Add parameter to URL if it's not using the default value
                if (!useDefaultValue)
                {
                    queryStringBuilder.Append('&');
                    // Report parameters don't use a prefix and are case-sensitive
                    queryStringBuilder.Append($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(paramValue ?? string.Empty)}");
                }
            }
            
            // Always add rs:Format=PDF to the URL
            queryStringBuilder.Append("&rs:Format=PDF");
            
            // Construct the final URL using the format: https://[rswebserviceurl]?[pathinfo]&[parameters]
            // Note that the question mark comes before the report path, not after
            var reportUrl = $"{_reportViewerUrl}?{reportPath}{queryStringBuilder}";

            _logger.LogInformation("Successfully generated report URL: {ReportUrl}", reportUrl);
            return reportUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report URL for report: {ReportName}", report.Name);
            throw; // Rethrow to ensure the error is properly handled by the calling code
        }
    }
}
