namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents an agent state in the conversation flow
/// </summary>
public enum AgentState
{
    /// <summary>
    /// The report selection agent is active
    /// </summary>
    ReportSelection,
    
    /// <summary>
    /// The SSRS API agent is active
    /// </summary>
    SsrsApiRetrieval,
    
    /// <summary>
    /// The parameter filling agent is active
    /// </summary>
    ParameterFilling,
    
    /// <summary>
    /// The URL creation agent is active
    /// </summary>
    ReportUrlCreation,
    
    /// <summary>
    /// The conversation has completed
    /// </summary>
    Completed
}
