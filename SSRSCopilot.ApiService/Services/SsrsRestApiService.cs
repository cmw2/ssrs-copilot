using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using SSRSCopilot.ApiService.Models;

namespace SSRSCopilot.ApiService.Services;

/// <summary>
/// Implementation of ISsrsRestApiService for interacting with SQL Reporting Services REST API
/// </summary>
public class SsrsRestApiService : ISsrsRestApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SsrsRestApiService> _logger;
    private readonly string _baseUrl;
    
    public SsrsRestApiService(IConfiguration configuration, ILogger<SsrsRestApiService> logger)
    {
        _logger = logger;
        
        // Get base URL from configuration
        _baseUrl = configuration["SSRS:ApiUrl"] ?? "https://ssrs.example.com/api/v2.0";
        
        // Create and configure the HTTP client
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Set timeout to 2 minutes to match the client-side timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(2);
        
        // Configure authentication if needed
        ConfigureAuthentication(configuration);
    }
    
    /// <summary>
    /// Configures authentication for the SSRS REST API
    /// </summary>
    private void ConfigureAuthentication(IConfiguration configuration)
    {
        // Read authentication settings from configuration
        string authType = configuration["SSRS:AuthType"] ?? "None";
        
        switch (authType.ToLowerInvariant())
        {
            case "basic":
                string username = configuration["SSRS:Username"] ?? "";
                string password = configuration["SSRS:Password"] ?? "";
                
                if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                {
                    var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
                }
                break;
                
            case "bearer":
                string token = configuration["SSRS:BearerToken"] ?? "";
                
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
                break;
                
            // Add other authentication methods as needed
        }
    }
    
    /// <inheritdoc/>
    public async Task<List<Report>> GetReportsAsync()
    {
        try
        {
            _logger.LogInformation("Fetching reports from SSRS REST API");
            
            // Get reports from SSRS REST API
            var response = await _httpClient.GetAsync("Reports");
            response.EnsureSuccessStatusCode();
            
            var content = await response.Content.ReadAsStringAsync();
            var reportData = JsonSerializer.Deserialize<ReportsApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (reportData?.Value == null)
            {
                return new List<Report>();
            }
            
            // Map API reports to our Report model
            return reportData.Value.Select(apiReport => new Report
            {
                Id = apiReport.Id,
                Name = apiReport.Name,
                Path = apiReport.Path,
                Description = apiReport.Description ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching reports from SSRS REST API");
            return new List<Report>();
        }
    }
    
    /// <inheritdoc/>
    public async Task<Report?> GetReportAsync(string reportId)
    {
        try
        {
            _logger.LogInformation("Fetching report {ReportId} from SSRS REST API", reportId);
            
            // Get specific report from SSRS REST API
            var response = await _httpClient.GetAsync($"Reports({reportId})");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Report {ReportId} not found", reportId);
                return null;
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var apiReport = JsonSerializer.Deserialize<ApiReport>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (apiReport == null)
            {
                return null;
            }
            
            // Map API report to our Report model
            var report = new Report
            {
                Id = apiReport.Id,
                Name = apiReport.Name,
                Path = apiReport.Path,
                Description = apiReport.Description ?? ""
            };
            
            // Get report parameters
            report.Parameters = await GetReportParameterDefinitionsAsync(reportId);
            
            return report;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching report {ReportId} from SSRS REST API", reportId);
            return null;
        }
    }
    
    /// <inheritdoc/>
    public async Task<Report?> GetReportByNameAsync(string reportName)
    {
        try
        {
            _logger.LogInformation("Searching for report by name: {ReportName}", reportName);
            
            // Get all reports first (may need to implement paging for large report servers)
            var reports = await GetReportsAsync();
            
            // Try to find an exact match first
            var report = reports.FirstOrDefault(r => 
                r.Name.Equals(reportName, StringComparison.OrdinalIgnoreCase));
            
            // If no exact match, try a contains match
            if (report == null)
            {
                report = reports.FirstOrDefault(r => 
                    r.Name.Contains(reportName, StringComparison.OrdinalIgnoreCase) ||
                    reportName.Contains(r.Name, StringComparison.OrdinalIgnoreCase));
            }
            
            // If found, get complete report details including parameters
            if (report != null)
            {
                return await GetReportAsync(report.Id);
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding report by name: {ReportName}", reportName);
            return null;
        }
    }
    
    /// <inheritdoc/>
    public async Task<List<ReportParameter>> GetReportParameterDefinitionsAsync(string reportId)
    {
        try
        {
            _logger.LogInformation("Fetching parameter definitions for report {ReportId}", reportId);
            
            // Get parameter definitions from SSRS REST API
            var response = await _httpClient.GetAsync($"Reports({reportId})/ParameterDefinitions");
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Parameter definitions for report {ReportId} not found", reportId);
                return new List<ReportParameter>();
            }
            
            var content = await response.Content.ReadAsStringAsync();
            var parameterData = JsonSerializer.Deserialize<ParameterDefinitionsApiResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            
            if (parameterData?.Value == null)
            {
                return new List<ReportParameter>();
            }
            
            // Map API parameter definitions to our ReportParameter model
            return parameterData.Value.Select(apiParam => new ReportParameter
            {
                Name = apiParam.Name,
                DataType = apiParam.Type,
                IsRequired = !apiParam.Nullable,
                DefaultValue = apiParam.DefaultValue,
                Description = apiParam.Prompt ?? apiParam.Name,
                AllowedValues = apiParam.AllowedValues?.ToList()
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching parameter definitions for report {ReportId}", reportId);
            return new List<ReportParameter>();
        }
    }
    
    #region API Response Models
    
    // Model for the API response when retrieving reports
    private class ReportsApiResponse
    {
        public List<ApiReport> Value { get; set; } = new List<ApiReport>();
    }
    
    // Model for a report from the API
    private class ApiReport
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
    
    // Model for the API response when retrieving parameter definitions
    private class ParameterDefinitionsApiResponse
    {
        public List<ApiParameterDefinition> Value { get; set; } = new List<ApiParameterDefinition>();
    }
    
    // Model for a parameter definition from the API
    private class ApiParameterDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool Nullable { get; set; }
        public string? DefaultValue { get; set; }
        public string? Prompt { get; set; }
        public string[]? AllowedValues { get; set; }
    }
    
    #endregion
}
