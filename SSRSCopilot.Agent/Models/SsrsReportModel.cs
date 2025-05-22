using System.Text.Json.Serialization;

namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents an SSRS report response from the SSRS API
/// </summary>
public class SsrsReportModel
{
    /// <summary>
    /// The OData identifier of the report
    /// </summary>
    [JsonPropertyName("Id")]
    public string Id { get; set; } = string.Empty;
    
    /// <summary>
    /// The name of the report
    /// </summary>
    [JsonPropertyName("Name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The path to the report in the SSRS server
    /// </summary>
    [JsonPropertyName("Path")]
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// The description of the report
    /// </summary>
    [JsonPropertyName("Description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// The creation date of the report
    /// </summary>
    [JsonPropertyName("CreationDate")]
    public DateTime? CreationDate { get; set; }
    
    /// <summary>
    /// The modification date of the report
    /// </summary>
    [JsonPropertyName("ModifiedDate")]
    public DateTime? ModifiedDate { get; set; }
    
    /// <summary>
    /// The parent folder ID
    /// </summary>
    [JsonPropertyName("ParentFolderId")]
    public string? ParentFolderId { get; set; }
    
    /// <summary>
    /// Indicates if the report is hidden
    /// </summary>
    [JsonPropertyName("Hidden")]
    public bool? Hidden { get; set; }
    
    /// <summary>
    /// The size of the report in bytes
    /// </summary>
    [JsonPropertyName("Size")]
    public long? Size { get; set; }
    
    /// <summary>
    /// The SSRS report definition type
    /// </summary>
    [JsonPropertyName("Type")]
    public string? Type { get; set; }
}
