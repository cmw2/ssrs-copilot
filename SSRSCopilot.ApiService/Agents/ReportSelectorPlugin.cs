using Microsoft.SemanticKernel;
using System.ComponentModel;
using SSRSCopilot.ApiService.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Plugin that wraps the existing ReportSelectorAgent for use with kernel function calls
/// </summary>
public class ReportSelectorPlugin
{
    private readonly ReportSelectorAgent _reportSelectorAgent;
    private readonly ChatContext _context;

    [JsonConstructor]
    public ReportSelectorPlugin(ReportSelectorAgent reportSelectorAgent, ChatContext context)
    {
        _reportSelectorAgent = reportSelectorAgent;
        _context = context;
    }

    /// <summary>
    /// Find reports using natural language queries through Use Your Own Data feature
    /// </summary>
    [KernelFunction]
    [Description("Find reports using natural language queries through Use Your Own Data feature")]
    public async Task<string> FindReportsAsync(
        [Description("The natural language query to find reports")] string query)
    {
        // Call the report selector agent to process the query
        var response = await _reportSelectorAgent.ProcessMessageAsync(query, _context);
        
        // If a report was selected, update the context
        if (_context.SelectedReport != null)
        {
            return JsonSerializer.Serialize(new { 
                Success = true, 
                SelectedReport = _context.SelectedReport,
                Message = response.Message
            });
        }
        
        // Otherwise, return the list of found reports (extracted from history by the LLM)
        var reports = ExtractReportsFromHistory(_context);
        
        return JsonSerializer.Serialize(new {
            Success = true,
            Reports = reports,
            Message = response.Message
        });
    }
    
    /// <summary>
    /// Select a specific report from the search results by name or number
    /// </summary>
    [KernelFunction]
    [Description("Select a specific report from the search results by name or number")]
    public string SelectReport(
        [Description("The name or number of the report to select")] string selection)
    {
        // Extract reports from context history
        var reports = ExtractReportsFromHistory(_context);
        
        Report? selectedReport = null;
        
        // Try to parse the selection as a number (1-based index)
        if (int.TryParse(selection, out int index) && index > 0 && index <= reports.Count)
        {
            selectedReport = reports[index - 1];
        }
        else
        {
            // Try to find by name
            selectedReport = reports.FirstOrDefault(r => 
                r.Name.Equals(selection, StringComparison.OrdinalIgnoreCase));
        }
        
        if (selectedReport == null)
        {
            return JsonSerializer.Serialize(new { 
                Error = "Report not found. Please provide a valid report name or number." 
            });
        }
        
        // Update the context with the selected report
        _context.SelectedReport = selectedReport;
        _context.State = AgentState.SsrsApiRetrieval;
        
        return JsonSerializer.Serialize(new { 
            Success = true, 
            SelectedReport = selectedReport 
        });
    }
    
    /// <summary>
    /// Extract reports from chat history
    /// </summary>
    private List<Report> ExtractReportsFromHistory(ChatContext context)
    {
        // Look for reports in system messages (where ReportSelectorAgent stores JSON data)
        var systemMessages = context.History
            .Where(m => m.Role.Equals("system", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.Content)
            .ToList();
        
        foreach (var message in systemMessages)
        {
            try
            {
                // Try to deserialize as a list of reports
                var reports = JsonSerializer.Deserialize<List<Report>>(message);
                if (reports != null && reports.Any())
                {
                    return reports;
                }
            }
            catch
            {
                // Ignore deserialization errors and try next message
            }
        }
        
        return new List<Report>();
    }
}
