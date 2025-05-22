using System.Text.Json.Serialization;

namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents an SSRS report parameter response from the SSRS API
/// </summary>
public class SsrsReportParameterModel
{
    /// <summary>
    /// The name of the parameter
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The type of the parameter
    /// </summary>
    [JsonPropertyName("ParameterType")]
    public string ParameterType { get; set; } = string.Empty;
    
    /// <summary>
    /// The visibility of the parameter
    /// </summary>
    [JsonPropertyName("ParameterVisibility")]
    public string ParameterVisibility { get; set; } = string.Empty;
    
    /// <summary>
    /// The state of the parameter
    /// </summary>
    [JsonPropertyName("ParameterState")]
    public string ParameterState { get; set; } = string.Empty;
    
    /// <summary>
    /// The valid values for the parameter
    /// </summary>
    [JsonPropertyName("ValidValues")]
    public List<string> ValidValues { get; set; } = new List<string>();
    
    /// <summary>
    /// Indicates if valid values is null
    /// </summary>
    [JsonPropertyName("ValidValuesIsNull")]
    public bool ValidValuesIsNull { get; set; }
    
    /// <summary>
    /// Indicates if the parameter can be null
    /// </summary>
    [JsonPropertyName("Nullable")]
    public bool Nullable { get; set; }
    
    /// <summary>
    /// Indicates if the parameter allows blank values
    /// </summary>
    [JsonPropertyName("AllowBlank")]
    public bool AllowBlank { get; set; }
    
    /// <summary>
    /// Indicates if the parameter is multi-valued
    /// </summary>
    [JsonPropertyName("MultiValue")]
    public bool MultiValue { get; set; }
    
    /// <summary>
    /// The prompt text for the parameter
    /// </summary>
    [JsonPropertyName("Prompt")]
    public string Prompt { get; set; } = string.Empty;
    
    /// <summary>
    /// Indicates if the user should be prompted for this parameter
    /// </summary>
    [JsonPropertyName("PromptUser")]
    public bool PromptUser { get; set; }
    
    /// <summary>
    /// Indicates if this is a query parameter
    /// </summary>
    [JsonPropertyName("QueryParameter")]
    public bool QueryParameter { get; set; }
    
    /// <summary>
    /// Indicates if default values are query-based
    /// </summary>
    [JsonPropertyName("DefaultValuesQueryBased")]
    public bool DefaultValuesQueryBased { get; set; }
    
    /// <summary>
    /// Indicates if valid values are query-based
    /// </summary>
    [JsonPropertyName("ValidValuesQueryBased")]
    public bool ValidValuesQueryBased { get; set; }
    
    /// <summary>
    /// Parameter dependencies
    /// </summary>
    [JsonPropertyName("Dependencies")]
    public List<string> Dependencies { get; set; } = new List<string>();
    
    /// <summary>
    /// Default values for the parameter
    /// </summary>
    [JsonPropertyName("DefaultValues")]
    public List<string> DefaultValues { get; set; } = new List<string>();
    
    /// <summary>
    /// Indicates if default values is null
    /// </summary>
    [JsonPropertyName("DefaultValuesIsNull")]
    public bool DefaultValuesIsNull { get; set; }
    
    /// <summary>
    /// Error message if any
    /// </summary>
    [JsonPropertyName("ErrorMessage")]
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Computed property to determine if the parameter is required based on Nullable and AllowBlank
    /// </summary>
    [JsonIgnore]
    public bool IsRequired => !Nullable && !AllowBlank;
}
