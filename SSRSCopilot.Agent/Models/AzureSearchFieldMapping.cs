using System.ComponentModel.DataAnnotations;

namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Configuration model for mapping logical field names to actual Azure Search index field names
/// </summary>
public class AzureSearchFieldMapping
{
    /// <summary>
    /// The field name used as the unique identifier/key in the search index
    /// </summary>
    [Required]
    public string IdField { get; set; } = "id";

    /// <summary>
    /// The field name containing the document title
    /// </summary>
    [Required]
    public string TitleField { get; set; } = "title";

    /// <summary>
    /// The field name containing the main content/text of the document
    /// </summary>
    [Required]
    public string ContentField { get; set; } = "content";

    /// <summary>
    /// The field name containing the URL or web address of the document
    /// </summary>
    public string? UrlField { get; set; } = "url";

    /// <summary>
    /// The field name containing the file path of the document
    /// </summary>
    public string? FilePathField { get; set; } = "filepath";

    /// <summary>
    /// The field name containing metadata as JSON string
    /// </summary>
    public string? MetadataField { get; set; } = "meta_json_string";

    /// <summary>
    /// The field name containing the vector embedding for vector search
    /// </summary>
    public string? VectorField { get; set; } = "contentVector";

    /// <summary>
    /// The field name containing the parent document identifier (used for chunked documents)
    /// </summary>
    public string? ParentIdField { get; set; } = "parent_id";

    /// <summary>
    /// Validates that all required fields are properly configured
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(IdField))
            throw new InvalidOperationException("IdField is required in AzureSearch field mapping configuration");
            
        if (string.IsNullOrWhiteSpace(TitleField))
            throw new InvalidOperationException("TitleField is required in AzureSearch field mapping configuration");
            
        if (string.IsNullOrWhiteSpace(ContentField))
            throw new InvalidOperationException("ContentField is required in AzureSearch field mapping configuration");
    }
}
