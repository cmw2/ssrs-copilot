using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace SSRSCopilot.Agent.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ILogger<DiagnosticsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly ISsrsService _ssrsService;
        private readonly HttpClient _ssrsHttpClient;

        public DiagnosticsController(
            ILogger<DiagnosticsController> logger,
            IConfiguration configuration,
            ISsrsService ssrsService,
            SsrsHttpClient ssrsHttpClient)
        {
            _logger = logger;
            _configuration = configuration;
            _ssrsService = ssrsService;
            _ssrsHttpClient = ssrsHttpClient.Client;
        }

        [HttpGet("ntlm-test")]
        public async Task<IActionResult> TestNtlmAuthentication()
        {
            try
            {
                _logger.LogInformation("Starting NTLM authentication diagnostic test");

                // Get SSRS configuration
                var apiBaseUrl = _configuration["Ssrs:ApiBaseUrl"];
                var username = _configuration["Ssrs:Username"];
                var password = _configuration["Ssrs:Password"];
                var domain = _configuration["Ssrs:Domain"];

                // Create a diagnostic response object
                var diagnostic = new
                {
                    ApiBaseUrl = apiBaseUrl,
                    Username = !string.IsNullOrEmpty(username) ? username : "(not set)",
                    Domain = !string.IsNullOrEmpty(domain) ? domain : "(not set)",
                    PasswordProvided = !string.IsNullOrEmpty(password),
                    AuthConfig = new
                    {
                        LogAuthHeadersEnabled = _configuration["LogAuthenticationHeaders"] == "true" ||
                                               _configuration["Logging:LogAuthenticationHeaders"] == "true"
                    }
                };

                // Try to get reports using the existing service
                var reports = await _ssrsService.GetReportsAsync();

                // Return result
                return Ok(new
                {
                    Diagnostic = diagnostic,
                    Success = reports.Count > 0,
                    ReportCount = reports.Count,
                    Message = reports.Count > 0
                        ? $"Successfully authenticated and retrieved {reports.Count} reports"
                        : "Authentication successful but no reports found"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "NTLM authentication test failed");

                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerError = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("get-reports")]
        public async Task<IActionResult> GetReports()
        {
            try
            {
                _logger.LogInformation("Testing GetReportsAsync method");

                var reports = await _ssrsService.GetReportsAsync();

                // Get the raw SSRS response
                var rawResponse = await _ssrsHttpClient.GetRawSsrsResponseAsync("reports", _logger);

                return Ok(new
                {
                    Success = true,
                    ReportCount = reports.Count,
                    Reports = reports,
                    RawSsrsResponse = rawResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReportsAsync test failed");

                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerError = ex.InnerException?.Message
                });
            }
        }


        [HttpGet("get-report-by-id")]
        public async Task<IActionResult> GetReportById([FromQuery] string id)
        {
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    return BadRequest("Report ID is required");
                }

                _logger.LogInformation("Testing GetReportByIdAsync method with ID: {Id}", id);

                var report = await _ssrsService.GetReportByIdAsync(id);

                // Get the raw SSRS response
                var reportId = id.StartsWith('/') ? id.TrimStart('/') : id;
                var rawResponse = await _ssrsHttpClient.GetRawSsrsResponseAsync($"reports({reportId})", _logger);

                if (report == null)
                {
                    return NotFound($"Report with ID '{id}' not found");
                }

                return Ok(new
                {
                    Success = true,
                    Report = report,
                    RawSsrsResponse = rawResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReportByIdAsync test failed for ID: {Id}", id);

                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerError = ex.InnerException?.Message
                });
            }
        }

        [HttpGet("get-report-parameters")]
        public async Task<IActionResult> GetReportParameters([FromQuery] string reportId)
        {
            try
            {
                if (string.IsNullOrEmpty(reportId))
                {
                    return BadRequest("Report ID is required");
                }

                SsrsReportModel? report = null;

                // Get the report by ID
                _logger.LogInformation("Looking up report for parameters by ID: {ReportId}", reportId);
                report = await _ssrsService.GetReportByIdAsync(reportId);

                if (report == null)
                {
                    return NotFound($"Report with ID '{reportId}' not found");
                }

                _logger.LogInformation("Testing GetReportParametersAsync method for report: {ReportName}", report.Name);

                var parameters = await _ssrsService.GetReportParametersAsync(reportId);

                // Get raw SSRS response for parameters
                var reportIdForParams = report.Id.TrimStart('/');
                var rawResponse = await _ssrsHttpClient.GetRawSsrsResponseAsync($"reports({reportIdForParams})/ParameterDefinitions", _logger);

                return Ok(new
                {
                    Success = true,
                    ReportName = report.Name,
                    ReportId = report.Id,
                    ReportPath = report.Path,
                    ParameterCount = parameters.Count,
                    Parameters = parameters,
                    RawSsrsResponse = rawResponse
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReportParametersAsync test failed");

                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerError = ex.InnerException?.Message
                });
            }
        }

        [HttpPost("generate-report-url")]
        public async Task<IActionResult> GenerateReportUrl([FromBody] GenerateReportUrlRequest request)
        {
            try
            {
                if (request == null)
                {
                    return BadRequest("Request body is required");
                }

                if (string.IsNullOrEmpty(request.ReportId))
                {
                    return BadRequest("Report ID is required");
                }

                SsrsReportModel? report = null;

                // Get the report by ID
                _logger.LogInformation("Looking up report for URL generation by ID: {ReportId}", request.ReportId);
                report = await _ssrsService.GetReportByIdAsync(request.ReportId);

                if (report == null)
                {
                    return NotFound($"Report with ID '{request.ReportId}' not found");
                }

                _logger.LogInformation("Testing GenerateReportUrlAsync method for report: {ReportName}", report.Name);

                // Get the parameters for the report to validate the parameter values
                var parameters = await _ssrsService.GetReportParametersAsync(request.ReportId);

                // Convert the parameter values from the request to a dictionary
                var parameterValues = request.ParameterValues ?? new Dictionary<string, string>();

                // Check for missing required parameters
                var missingRequiredParams = parameters
                    .Where(p => p.IsRequired &&
                        (!parameterValues.ContainsKey(p.Name) || string.IsNullOrEmpty(parameterValues[p.Name])))
                    .ToList();

                if (missingRequiredParams.Any())
                {
                    var missingParamNames = string.Join(", ", missingRequiredParams.Select(p => p.Name));
                    return BadRequest($"Missing required parameters: {missingParamNames}");
                }

                var reportUrl = await _ssrsService.GenerateReportUrlAsync(report, parameterValues);

                return Ok(new
                {
                    Success = true,
                    ReportName = report.Name,
                    ReportId = report.Id,
                    ReportPath = report.Path,
                    Url = reportUrl,
                    Parameters = parameterValues
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GenerateReportUrlAsync test failed");

                return StatusCode(500, new
                {
                    Success = false,
                    Error = ex.Message,
                    StackTrace = ex.StackTrace,
                    InnerError = ex.InnerException?.Message
                });
            }
        }
    }
    public class GenerateReportUrlRequest
    {
        public string? ReportId { get; set; }
        public Dictionary<string, string>? ParameterValues { get; set; }
    }
    
    /// <summary>
    /// Request model for getting raw SSRS API responses
    /// </summary>
    public class SsrsRawRequest
    {
        /// <summary>
        /// The relative URL path to call on the SSRS API
        /// </summary>
        public string RelativeUrl { get; set; } = string.Empty;
    }
}