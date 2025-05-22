using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Services;
using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SSRSCopilot.Agent.Plugins;

/// <summary>
/// Plugin for interacting with the SSRS REST API
/// </summary>
public class SsrsPlugin
{
    private readonly ISsrsService _ssrsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsrsPlugin"/> class.
    /// </summary>
    /// <param name="ssrsService">The SSRS service</param>
    public SsrsPlugin(ISsrsService ssrsService)
    {
        _ssrsService = ssrsService;
    }

    /// <summary>
    /// Gets a list of all available reports from the SSRS server
    /// </summary>
    /// <returns>A list of available reports</returns>
    [KernelFunction]
    [Description("Get all available reports from the SSRS server. IMPORTANT: Always call this method after a user selects a report from documentation. You must compare the documentation title with all these SSRS report names to find the best semantic match. Look for similarities in keywords, business terms, concepts, abbreviations, and synonyms.")]
    public async Task<List<SsrsReportModel>> GetAllReportsAsync()
    {
        return await _ssrsService.GetReportsAsync();
    }

    /// <summary>
    /// Gets a specific report by its ID
    /// </summary>
    /// <param name="reportId">The ID of the report</param>
    /// <returns>The requested report or null if not found</returns>
    [KernelFunction]
    [Description("Get a specific report by its exact ID in SSRS. Use this when you already have the report ID.")]
    public async Task<SsrsReportModel?> GetReportByIdAsync(string reportId)
    {
        return await _ssrsService.GetReportByIdAsync(reportId);
    }

    /// <summary>
    /// Gets the parameters for a specific report
    /// </summary>
    /// <param name="reportId">The ID of the report to get parameters for</param>
    /// <returns>A list of parameters for the report</returns>
    [KernelFunction]
    [Description("Get the parameters for a specific report by its exact SSRS ID. Use this when you already have the report ID.")]
    public async Task<List<SsrsReportParameterModel>> GetReportParametersAsync(string reportId)
    {
        var report = await _ssrsService.GetReportByIdAsync(reportId);
        if (report == null)
        {
            return new List<SsrsReportParameterModel>();
        }
        
        return await _ssrsService.GetReportParametersAsync(reportId);
    }

    /// <summary>
    /// Generates a URL for running a report with the specified parameters
    /// </summary>
    /// <param name="reportId">The ID of the report to run</param>
    /// <param name="parameterValues">Optional. Dictionary containing parameter names and values. If null, an empty dictionary will be used.</param>
    /// <returns>A URL that can be used to view the report</returns>
    [KernelFunction]
    [Description("Generate a URL for running a report with the specified parameters. Make sure you're using the exact SSRS report ID.")]
    public async Task<string> GenerateReportUrlAsync(string reportId, Dictionary<string, string>? parameterValues = null)
    {        try 
        {
            var report = await _ssrsService.GetReportByIdAsync(reportId);
            if (report == null)
            {
                throw new ArgumentException($"Report with ID '{reportId}' not found");
            }
            
            // Ensure parameterValues is not null
            parameterValues ??= new Dictionary<string, string>();
            
            // Validate parameters before generating URL
            var validation = await ValidateParameterValuesAsync(reportId, parameterValues);
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Invalid parameter values: {string.Join(", ", validation.ErrorMessages)}");
            }
            
            return await _ssrsService.GenerateReportUrlAsync(report, parameterValues);
        }
        catch (Exception ex)
        {
            // Log the exception but return a user-friendly error
            return $"Error generating report URL: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates if the parameter values provided are correct for the report
    /// </summary>
    /// <param name="reportId">The ID of the report</param>
    /// <param name="parameterValues">Optional. Dictionary containing parameter names and values to validate. If null, an empty dictionary will be used.</param>
    /// <returns>Validation result with success flag and any error messages</returns>
    [KernelFunction]
    [Description("Validate if the parameter values provided are correct for the report. Make sure you're using the exact SSRS report ID.")]
    public async Task<(bool IsValid, string[] ErrorMessages)> ValidateParameterValuesAsync(string reportId, Dictionary<string, string>? parameterValues = null)    {
        var report = await _ssrsService.GetReportByIdAsync(reportId);
        if (report == null)
        {
            return (false, new[] { $"Report with ID '{reportId}' not found" });
        }
        
        var parameters = await _ssrsService.GetReportParametersAsync(reportId);
        var errorMessages = new List<string>();
        
        // Ensure parameterValues is not null
        parameterValues ??= new Dictionary<string, string>();
        
        // Check if all required parameters are provided
        foreach (var parameter in parameters.Where(p => p.IsRequired))
        {
            if (!parameterValues.ContainsKey(parameter.Name) || string.IsNullOrEmpty(parameterValues[parameter.Name]))
            {
                errorMessages.Add($"Required parameter '{parameter.Name}' is missing");
            }
        }
        
        // Check if provided parameters are valid
        foreach (var kvp in parameterValues)
        {
            var parameter = parameters.FirstOrDefault(p => p.Name == kvp.Key);
            if (parameter == null)
            {
                errorMessages.Add($"Unknown parameter '{kvp.Key}'");
                continue;
            }
            
            // Check if the value is in the allowed values (if restricted)
            if (parameter.ValidValues.Count > 0 && !parameter.ValidValues.Contains(kvp.Value))
            {
                errorMessages.Add($"Value '{kvp.Value}' is not allowed for parameter '{kvp.Key}'. Allowed values: {string.Join(", ", parameter.ValidValues)}");
            }
            
            // Additional validation based on data type could be added here
        }
        
        return (errorMessages.Count == 0, errorMessages.ToArray());
    }
}
