namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents an SSRS report
/// </summary>
public class Report
{
    /// <summary>
    /// The unique identifier of the report
    /// </summary>
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The name of the report
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The path to the report in the SSRS server
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of the report
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// The URL of the report document
    /// </summary>
    public string Url { get; set; } = string.Empty;
    
    /// <summary>
    /// The file path of the report document
    /// </summary>
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// The metadata JSON string associated with the report document
    /// </summary>
    public string MetaJsonString { get; set; } = string.Empty;
    
    /// <summary>
    /// The list of parameters for the report
    /// </summary>
    public List<ReportParameter> Parameters { get; set; } = new();
}
