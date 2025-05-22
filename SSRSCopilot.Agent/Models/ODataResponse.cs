using System.Text.Json.Serialization;

namespace SSRSCopilot.Agent.Models;

/// <summary>
/// Represents a standard OData response wrapper containing a collection of items
/// </summary>
/// <typeparam name="T">The type of items in the collection</typeparam>
public class ODataResponse<T>
{
    /// <summary>
    /// Gets or sets the OData context metadata
    /// </summary>
    [JsonPropertyName("@odata.context")]
    public string? Context { get; set; }
    
    /// <summary>
    /// Gets or sets the collection of items
    /// </summary>
    public List<T> Value { get; set; } = new List<T>();
}
