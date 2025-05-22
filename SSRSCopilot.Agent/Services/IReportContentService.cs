namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Interface for the service that retrieves report content
/// </summary>
public interface IReportContentService
{
    /// <summary>
    /// Gets the content of a report from its URL
    /// </summary>
    /// <param name="reportUrl">The full URL to the report</param>
    /// <returns>A tuple containing the report content as a byte array and its content type</returns>
    Task<(byte[] Content, string ContentType)> GetReportContentAsync(string reportUrl);
}
