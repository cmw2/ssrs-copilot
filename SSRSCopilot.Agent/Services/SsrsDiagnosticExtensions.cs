using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SSRSCopilot.Agent.Services
{
    /// <summary>
    /// Extension methods for the SsrsService and DiagnosticsController to help with testing
    /// </summary>
    public static class SsrsDiagnosticExtensions
    {
        /// <summary>
        /// Gets the raw JSON response from the SSRS API
        /// </summary>
        /// <param name="httpClient">The HTTP client to use</param>
        /// <param name="relativeUrl">The relative URL to fetch</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>A task that resolves to the raw JSON object</returns>
        public static async Task<object?> GetRawSsrsResponseAsync(this HttpClient httpClient, string relativeUrl, ILogger? logger = null)
        {
            try
            {
                logger?.LogInformation("Fetching raw SSRS response from: {RelativeUrl}", relativeUrl);
                
                var response = await httpClient.GetAsync(relativeUrl);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                
                // Parse the response into a dynamic JsonDocument
                using var jsonDoc = JsonDocument.Parse(content);
                
                // Convert to dictionary/array for serialization
                return ConvertJsonElementToObject(jsonDoc.RootElement);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error fetching raw SSRS response from: {RelativeUrl}", relativeUrl);
                throw;
            }
        }
        
        /// <summary>
        /// Converts a JsonElement to a dynamic object (Dictionary, List, primitive value)
        /// </summary>
        private static object? ConvertJsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var dictionary = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                    {
                        dictionary[property.Name] = ConvertJsonElementToObject(property.Value);
                    }
                    return dictionary;
                    
                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonElementToObject(item));
                    }
                    return list;
                    
                case JsonValueKind.String:
                    return element.GetString();
                    
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intValue))
                        return intValue;
                    if (element.TryGetInt64(out long longValue))
                        return longValue;
                    return element.GetDouble();
                    
                case JsonValueKind.True:
                    return true;
                    
                case JsonValueKind.False:
                    return false;
                    
                case JsonValueKind.Null:
                    return null;
                    
                default:
                    return null;
            }
        }
    }
}
