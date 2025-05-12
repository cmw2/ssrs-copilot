namespace SSRSCopilot.ApiService.Models;

/// <summary>
/// Represents the chat context that carries conversation state between agents
/// </summary>
public class ChatContext
{
    /// <summary>
    /// The conversation history
    /// </summary>
    public List<ChatMessage> History { get; set; } = new List<ChatMessage>();
    
    /// <summary>
    /// The current state of the agent workflow
    /// </summary>
    public AgentState State { get; set; } = AgentState.ReportSelection;
    
    /// <summary>
    /// The selected report, if any
    /// </summary>
    public Report? SelectedReport { get; set; }
    
    /// <summary>
    /// The collected parameter values
    /// </summary>
    public Dictionary<string, string> ParameterValues { get; set; } = new Dictionary<string, string>();
    
    /// <summary>
    /// The generated report URL
    /// </summary>
    public string? ReportUrl { get; set; }
}
