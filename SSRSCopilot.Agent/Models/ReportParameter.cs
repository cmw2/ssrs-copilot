namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents a parameter for an SSRS report
/// </summary>
public class ReportParameter
{
    /// <summary>
    /// The name of the parameter
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The data type of the parameter (string, integer, datetime, etc.)
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the parameter is required
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Indicates whether the parameter allows multiple values
    /// </summary>
    public bool IsMultiValue { get; set; }
    
    /// <summary>
    /// The allowed values for the parameter, if restricted
    /// </summary>
    public List<string> AllowedValues { get; set; } = new();
    
    /// <summary>
    /// The prompt text for the parameter
    /// </summary>
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// The default value for the parameter
    /// </summary>
    public string? DefaultValue { get; set; }
}
