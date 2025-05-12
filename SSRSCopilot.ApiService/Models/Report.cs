namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents an SSRS report with its metadata
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
    /// The description of the report
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// The path to the report on the SSRS server
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// The parameters required to run the report
    /// </summary>
    public List<ReportParameter> Parameters { get; set; } = new List<ReportParameter>();
}
