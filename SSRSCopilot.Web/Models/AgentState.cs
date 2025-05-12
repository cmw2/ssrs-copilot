namespace SSRSCopilot.Web.Models;

/// <summary>
/// Represents the state of the agent workflow
/// </summary>
public enum AgentState
{
    /// <summary>
    /// The report selection agent is active
    /// </summary>
    ReportSelection,
    
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
