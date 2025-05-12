namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents a parameter in an SSRS report
/// </summary>
public class ReportParameter
{
    /// <summary>
    /// The name of the parameter
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The data type of the parameter
    /// </summary>
    public string DataType { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates whether the parameter is required
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// The default value of the parameter, if any
    /// </summary>
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// A description of the parameter
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// A list of possible values for the parameter, if applicable
    /// </summary>
    public List<string>? AllowedValues { get; set; }
}
