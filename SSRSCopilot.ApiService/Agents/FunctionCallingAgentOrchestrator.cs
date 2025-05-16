using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.ComponentModel;
using System.Text.Json;
using SSRSCopilot.ApiService.Models;
using SSRSCopilot.ApiService.Services;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Main orchestrator for the function-calling based agent system.
/// This orchestrator uses LLM function calling for workflow control rather than explicit state management.
/// </summary>
public class FunctionCallingAgentOrchestrator
{
    private readonly Kernel _kernel;
    private readonly ChitchatAgent _chitchatAgent;
    private readonly ISsrsRestApiService _ssrsRestApiService;
    private readonly IReportUrlService _reportUrlService;
    private readonly IReportService _reportService;
    private readonly ReportSelectorAgent _reportSelectorAgent;
    private readonly ILogger<FunctionCallingAgentOrchestrator> _logger;
    
    public FunctionCallingAgentOrchestrator(
        Kernel kernel,
        ChitchatAgent chitchatAgent,
        ISsrsRestApiService ssrsRestApiService,
        IReportUrlService reportUrlService,
        IReportService reportService,
        ReportSelectorAgent reportSelectorAgent,
        ILogger<FunctionCallingAgentOrchestrator> logger)
    {
        _kernel = kernel;
        _chitchatAgent = chitchatAgent;
        _ssrsRestApiService = ssrsRestApiService;
        _reportUrlService = reportUrlService;
        _reportService = reportService;
        _reportSelectorAgent = reportSelectorAgent;
        _logger = logger;
    }
    
    /// <summary>
    /// Processes a user message and generates a response using the kernel to handle function calling
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, string sessionId)
    {
        // Get or create a chat context for this session
        var context = GetOrCreateContext(sessionId);
        
        try
        {
            _logger.LogInformation("Processing message in session {SessionId}: {Message}", sessionId, userMessage);
            
            // Check if this is a chitchat message
            if (IsChitchatMessage(userMessage))
            {
                // Process through the chitchat agent but don't change state
                return await _chitchatAgent.ProcessMessageAsync(userMessage, context);
            }
            
            // Add the user message to the chat history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });
            
            // Register all tools/functions with the kernel for the LLM to use
            RegisterFunctions(context);
            
            // Create a chat history from the context
            var chatHistory = CreateChatHistory(context);
            
            // Create execution settings with function calling enabled
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.2, // Lower temperature for more deterministic responses with function calls
                TopP = 0.95
            };
            
            // Create kernel arguments
            var kernelArguments = new KernelArguments(executionSettings);
            
            _logger.LogInformation("Executing kernel function with chat completion");
            
            // Create a function that uses the kernel and all registered plugins
            // This is the key change - using the kernel to orchestrate the entire interaction
            var function = _kernel.CreateFunctionFromPrompt(
                GetSystemPrompt(),
                executionSettings);
            
            // Invoke the kernel function with the chat history
            var result = await _kernel.InvokeAsync(
                function,
                arguments: new KernelArguments
                {
                    ["chatHistory"] = chatHistory,
                    ["userMessage"] = userMessage
                });
            
            // Extract the content from the result
            var content = result.GetValue<string>() ?? "I'm having trouble generating a response. Let's try again.";
            
            _logger.LogInformation("Kernel execution completed with response of {Length} characters", content.Length);
            
            // Add the assistant's message to the history
            context.History.Add(new ChatMessage { Role = "assistant", Content = content });
            
            // Save the updated context
            SaveContext(sessionId, context);
            
            // Return the response
            return new ChatResponse
            {
                Message = content,
                ReportUrl = context.ReportUrl,
                State = context.State
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in FunctionCallingAgentOrchestrator.ProcessMessageAsync");
            
            return new ChatResponse
            {
                Message = "I'm sorry, but something went wrong. Let's start over. What kind of report are you looking for?",
                State = AgentState.ReportSelection
            };
        }
    }
    
    /// <summary>
    /// Register all functions with the kernel, ensuring clean registration
    /// </summary>
    private void RegisterFunctions(ChatContext context)
    {
        _logger.LogInformation("Registering function calling plugins with kernel");
        
        // First, remove existing plugins if they exist
        RemoveExistingPlugins();
        
        try
        {
            // Register the SSRS service plugin
            var ssrsServicePlugin = new SsrsServicePlugin(_ssrsRestApiService, context);
            _kernel.Plugins.AddFromObject(ssrsServicePlugin, "SsrsService");
            _logger.LogInformation("Registered SsrsService plugin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering SsrsService plugin");
        }
        
        try
        {
            // Register the report parameters plugin
            var reportParamPlugin = new ReportParameterPlugin(_reportUrlService, context);
            _kernel.Plugins.AddFromObject(reportParamPlugin, "ReportParameters");
            _logger.LogInformation("Registered ReportParameters plugin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering ReportParameters plugin");
        }
        
        try
        {
            // Register the advanced report selector plugin 
            var reportSelectorPlugin = new ReportSelectorPlugin(_reportSelectorAgent, _reportService, context, _logger);
            _kernel.Plugins.AddFromObject(reportSelectorPlugin, "ReportSelector");
            _logger.LogInformation("Registered ReportSelector plugin");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering ReportSelector plugin");
        }
    }
    
    /// <summary>
    /// Removes existing plugins to ensure clean registration
    /// </summary>
    private void RemoveExistingPlugins()
    {
        // List of plugin names to ensure they're removed before re-registration
        string[] pluginNames = { "SsrsService", "ReportParameters", "ReportSelector" };
        
        foreach (var name in pluginNames)
        {
            try
            {
                if (_kernel.Plugins.TryGetPlugin(name, out var plugin))
                {
                    _kernel.Plugins.Remove(plugin);
                    _logger.LogInformation("Removed existing plugin: {Name}", name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing plugin {Name}", name);
            }
        }
    }
    
    /// <summary>
    /// Create a chat history for the LLM based on the current context
    /// </summary>
    private ChatHistory CreateChatHistory(ChatContext context)
    {
        var chatHistory = new ChatHistory();

        // Add system message with instructions
        string systemPrompt = @"You are a helpful assistant that helps users find and run SQL Server Reporting Services (SSRS) reports.
Your task is to guide users through the complete process of selecting a report, filling in required parameters, and generating a URL to view the report.

Follow this workflow:
1. Help the user select a report based on their needs
2. Once a report is selected, retrieve its details and parameters automatically
3. Guide the user through filling in each required parameter
4. When all required parameters are provided, generate the report URL

You have the following tools available:
- Advanced search for reports using documentation and the report database (ReportSelector.FindReportsAsync) - USE THIS FIRST when finding reports
- Regular search for reports by name, topic, or description (SsrsService.SearchReportsAsync) - only use as a backup
- Get all available reports (SsrsService.GetAllReportsAsync)
- Get report details (SsrsService.GetReportDetailsAsync)
- Select a specific report (ReportSelector.SelectReportAsync)
- Set parameter values and validate them (ReportParameters.SetParameterValue)
- Get parameters for a report (ReportParameters.GetParameters)
- Get missing required parameters (ReportParameters.GetMissingRequiredParameters)
- Generate report URLs (ReportParameters.GenerateReportUrl)

For finding reports, ALWAYS use ReportSelector.FindReportsAsync FIRST, as it provides the most comprehensive results by searching through documentation and the database.

IMPORTANT WORKFLOW GUIDELINES:
- When a user asks for a report, IMMEDIATELY call ReportSelector.FindReportsAsync with their request
- If multiple reports are found, help the user choose by showing the options and waiting for their selection
- When a report is selected, IMMEDIATELY call SsrsService.GetReportDetailsAsync to get parameters
- After getting parameters, guide the user through providing values for each required parameter
- Once all parameters are provided, generate the report URL

Be conversational and helpful. Explain what parameters are needed and why. Verify parameter values when appropriate.
Always inform the user what you're doing at each step of the process.

Never leave a workflow step hanging - always follow through to completion. If you say you're going to search for reports, make sure you actually call the appropriate function and show results.";

        chatHistory.AddSystemMessage(systemPrompt);

        // Add conversation history (skip system messages)
        foreach (var message in context.History.Where(m => m.Role != "system"))
        {
            if (message.Role.Equals("user", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddUserMessage(message.Content);
            }
            else if (message.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase))
            {
                chatHistory.AddAssistantMessage(message.Content);
            }
        }

        return chatHistory;
    }
    
    /// <summary>
    /// Plugin for SSRS report search and detail retrieval
    /// </summary>
    public class SsrsServicePlugin
    {
        private readonly ISsrsRestApiService _ssrsRestApiService;
        private readonly ChatContext _context;
        
        public SsrsServicePlugin(ISsrsRestApiService ssrsRestApiService, ChatContext context)
        {
            _ssrsRestApiService = ssrsRestApiService;
            _context = context;
        }
        
        /// <summary>
        /// Search for reports matching a query
        /// </summary>
        [KernelFunction]
        [Description("Search for reports matching a query")]
        public async Task<string> SearchReportsAsync(
            [Description("The search query for finding reports")] string query)
        {
            try
            {
                // Get all reports first
                var allReports = await _ssrsRestApiService.GetReportsAsync();
                
                // Filter reports based on the query
                var reports = allReports.Where(r => 
                    r.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    (r.Description != null && r.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    r.Path.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();
                
                if (reports == null || !reports.Any())
                {
                    return System.Text.Json.JsonSerializer.Serialize(new { Message = "No reports found matching your query." });
                }
                
                return System.Text.Json.JsonSerializer.Serialize(reports);
            }
            catch (Exception ex)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = $"Error searching for reports: {ex.Message}" });
            }
        }
        
        /// <summary>
        /// Get all available reports
        /// </summary>
        [KernelFunction]
        [Description("Get a list of all available reports")]
        public async Task<string> GetAllReportsAsync()
        {
            try
            {
                var reports = await _ssrsRestApiService.GetReportsAsync();
                return System.Text.Json.JsonSerializer.Serialize(reports);
            }
            catch (Exception ex)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = $"Error retrieving reports: {ex.Message}" });
            }
        }
        
        /// <summary>
        /// Get detailed information about a specific report
        /// </summary>
        [KernelFunction]
        [Description("Get detailed information about a specific report")]
        public async Task<string> GetReportDetailsAsync(
            [Description("The name or ID of the report")] string reportNameOrId)
        {
            try
            {
                var report = await _ssrsRestApiService.GetReportByNameAsync(reportNameOrId);
                
                if (report == null)
                {
                    return System.Text.Json.JsonSerializer.Serialize(new { Error = "Report not found" });
                }
                
                // Save the report in the context for future use
                _context.SelectedReport = report;
                
                return System.Text.Json.JsonSerializer.Serialize(report);
            }
            catch (Exception ex)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = $"Error retrieving report details: {ex.Message}" });
            }
        }
    }
    
    /// <summary>
    /// Plugin for report parameters and URL generation
    /// </summary>
    public class ReportParameterPlugin
    {
        private readonly IReportUrlService _reportUrlService;
        private readonly ChatContext _context;
        
        public ReportParameterPlugin(IReportUrlService reportUrlService, ChatContext context)
        {
            _reportUrlService = reportUrlService;
            _context = context;
        }
        
        /// <summary>
        /// Set a parameter value for the selected report
        /// </summary>
        [KernelFunction]
        [Description("Set a parameter value for the selected report")]
        public string SetParameterValue(
            [Description("The name of the parameter")] string parameterName,
            [Description("The value to set for the parameter")] string parameterValue)
        {
            if (_context.SelectedReport == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }
            
            var parameter = _context.SelectedReport.Parameters.FirstOrDefault(p => 
                p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));
                
            if (parameter == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = $"Parameter '{parameterName}' not found" });
            }
            
            // Validate parameter value if it has allowed values
            if (parameter.AllowedValues != null && parameter.AllowedValues.Any())
            {
                bool isValid = parameter.AllowedValues.Any(v => 
                    v.Equals(parameterValue, StringComparison.OrdinalIgnoreCase));
                    
                if (!isValid)
                {
                    return System.Text.Json.JsonSerializer.Serialize(new 
                    { 
                        Error = $"Invalid value for parameter '{parameterName}'. Allowed values: {string.Join(", ", parameter.AllowedValues)}" 
                    });
                }
            }
            
            // Set the parameter value
            _context.ParameterValues[parameter.Name] = parameterValue;
            
            return System.Text.Json.JsonSerializer.Serialize(new { 
                Success = true, 
                Message = $"Parameter '{parameterName}' set to '{parameterValue}'",
                RemainingRequired = _context.SelectedReport.Parameters
                    .Where(p => p.IsRequired && !_context.ParameterValues.ContainsKey(p.Name))
                    .Select(p => p.Name)
                    .ToList()
            });
        }
        
        /// <summary>
        /// Get a list of all parameters for the selected report
        /// </summary>
        [KernelFunction]
        [Description("Get a list of all parameters for the selected report")]
        public string GetParameters()
        {
            if (_context.SelectedReport == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }
            
            return System.Text.Json.JsonSerializer.Serialize(_context.SelectedReport.Parameters);
        }
        
        /// <summary>
        /// Get a list of missing required parameters for the selected report
        /// </summary>
        [KernelFunction]
        [Description("Get a list of missing required parameters for the selected report")]
        public string GetMissingRequiredParameters()
        {
            if (_context.SelectedReport == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }
            
            var missingParams = _context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !_context.ParameterValues.ContainsKey(p.Name))
                .ToList();
                
            return System.Text.Json.JsonSerializer.Serialize(missingParams);
        }
        
        /// <summary>
        /// Generate a URL for the selected report with the current parameter values
        /// </summary>
        [KernelFunction]
        [Description("Generate a URL for the selected report with the current parameter values")]
        public string GenerateReportUrl(
            [Description("The format of the report (PDF, Excel, etc.)")] string format = "PDF")
        {
            if (_context.SelectedReport == null)
            {
                return System.Text.Json.JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }
            
            // Check for missing required parameters
            var missingParameters = _context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !_context.ParameterValues.ContainsKey(p.Name))
                .ToList();
                
            if (missingParameters.Any())
            {
                return System.Text.Json.JsonSerializer.Serialize(new 
                { 
                    Error = "Missing required parameters", 
                    MissingParameters = missingParameters.Select(p => p.Name).ToList() 
                });
            }
            
            // Generate the report URL
            string reportUrl = _reportUrlService.GenerateReportUrl(
                _context.SelectedReport, 
                _context.ParameterValues, 
                format);
                
            // Save the URL in the context
            _context.ReportUrl = reportUrl;
            _context.State = AgentState.Completed;
            
            return System.Text.Json.JsonSerializer.Serialize(new { Success = true, Url = reportUrl });
        }
    }
    
    /// <summary>
    /// Plugin for advanced report search using Azure Search and LLM
    /// </summary>
    public class ReportSelectorPlugin
    {
        private readonly ReportSelectorAgent _reportSelectorAgent;
        private readonly IReportService _reportService;
        private readonly ChatContext _context;
        private readonly ILogger<FunctionCallingAgentOrchestrator> _logger;
        
        public ReportSelectorPlugin(ReportSelectorAgent reportSelectorAgent, IReportService reportService, ChatContext context, ILogger<FunctionCallingAgentOrchestrator> logger)
        {
            _reportSelectorAgent = reportSelectorAgent;
            _reportService = reportService;
            _context = context;
            _logger = logger;
        }
        
        /// <summary>
        /// Advanced search for reports using Azure Search and LLM
        /// </summary>
        [KernelFunction]
        [Description("Advanced search for reports using Azure Search and LLM to find PDF report documentation")]
        public async Task<string> FindReportsAsync(
            [Description("The natural language query to find reports")] string query)
        {
            try
            {
                _logger.LogInformation("Starting advanced report search for query: {Query}", query);
                
                // First, try the direct database search as it's faster
                var dbReports = await _reportService.SearchReportsAsync(query);
                
                // Then call the report selector agent to search documentation if needed
                var response = await _reportSelectorAgent.ProcessMessageAsync(query, _context);
                
                // If a report was selected during the ReportSelectorAgent flow, use it
                if (_context.SelectedReport != null)
                {
                    _logger.LogInformation("Report selected by ReportSelectorAgent: {ReportName}", _context.SelectedReport.Name);
                    return JsonSerializer.Serialize(new { 
                        Success = true, 
                        SelectedReport = _context.SelectedReport,
                        Message = $"I found the '{_context.SelectedReport.Name}' report which matches your request. Let me retrieve its details for you."
                    });
                }
                
                // Extract reports from history (where ReportSelectorAgent may have stored them)
                var reports = ExtractReportsFromHistory(_context);
                
                // Combine results from both sources
                if (reports.Count == 0 && dbReports.Count > 0)
                {
                    reports = dbReports;
                }
                else if (dbReports.Count > 0)
                {
                    // Merge the lists, avoiding duplicates
                    foreach (var report in dbReports)
                    {
                        if (!reports.Any(r => r.Id == report.Id))
                        {
                            reports.Add(report);
                        }
                    }
                }
                
                _logger.LogInformation("Found {Count} reports matching the query", reports.Count);
                
                if (reports.Count == 0)
                {
                    return JsonSerializer.Serialize(new { 
                        Success = false,
                        Message = "I couldn't find any reports matching your query. Could you try with different keywords or describe what you're looking for in more detail?"
                    });
                }
                
                if (reports.Count == 1)
                {
                    // Auto-select if only one report found
                    _context.SelectedReport = reports[0];
                    _context.State = AgentState.SsrsApiRetrieval;
                    
                    return JsonSerializer.Serialize(new { 
                        Success = true, 
                        SelectedReport = reports[0],
                        Message = $"I found the '{reports[0].Name}' report which matches your request. Let me retrieve its parameters for you."
                    });
                }
                
                // Store reports in context for later reference
                _context.History.Add(new ChatMessage { 
                    Role = "system", 
                    Content = JsonSerializer.Serialize(reports) 
                });
                
                // Format the response for multiple reports
                var responseBuilder = new StringBuilder();
                responseBuilder.AppendLine("I found several reports that might match what you're looking for:");
                responseBuilder.AppendLine();
                
                for (int i = 0; i < reports.Count; i++)
                {
                    responseBuilder.AppendLine($"{i + 1}. {reports[i].Name}");
                    if (!string.IsNullOrEmpty(reports[i].Description))
                    {
                        responseBuilder.AppendLine($"   Description: {reports[i].Description}");
                    }
                }
                
                responseBuilder.AppendLine();
                responseBuilder.AppendLine("Which one would you like to use? You can select by number or name.");
                
                return JsonSerializer.Serialize(new {
                    Success = true,
                    Reports = reports,
                    Message = responseBuilder.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in advanced report search: {Message}", ex.Message);
                
                return JsonSerializer.Serialize(new { 
                    Success = false,
                    Error = "I encountered an error while searching for reports. Let's try a different approach.",
                    Message = "I encountered an error while searching for reports. Let's try a different approach. Can you tell me more about the type of product report you're looking for?"
                });
            }
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
        
        /// <summary>
        /// Select a specific report from the search results
        /// </summary>
        [KernelFunction]
        [Description("Select a specific report from the search results by name or number")]
        public async Task<string> SelectReportAsync(
            [Description("The name or number of the report to select")] string selection)
        {
            try
            {
                _logger.LogInformation("Selecting report with identifier: {Selection}", selection);
                
                // Extract reports from context history
                var reports = ExtractReportsFromHistory(_context);
                
                // If no reports in history, try to get them from the service
                if (reports.Count == 0)
                {
                    _logger.LogInformation("No reports found in context history, fetching from service");
                    reports = await _reportService.GetReportsAsync();
                }
                
                Report? selectedReport = null;
                
                // Try to parse the selection as a number (1-based index)
                if (int.TryParse(selection, out int index) && index > 0 && index <= reports.Count)
                {
                    selectedReport = reports[index - 1];
                    _logger.LogInformation("Selected report by index {Index}: {ReportName}", index, selectedReport.Name);
                }
                else
                {
                    // Try to find by name
                    selectedReport = reports.FirstOrDefault(r => 
                        r.Name.Equals(selection, StringComparison.OrdinalIgnoreCase) ||
                        r.Name.Contains(selection, StringComparison.OrdinalIgnoreCase));
                    
                    if (selectedReport != null)
                    {
                        _logger.LogInformation("Selected report by name: {ReportName}", selectedReport.Name);
                    }
                    
                    // If still not found, try to get it directly from the API
                    if (selectedReport == null)
                    {
                        _logger.LogInformation("Attempting to find report by ID: {Selection}", selection);
                        selectedReport = await _reportService.GetReportByIdAsync(selection);
                        
                        if (selectedReport != null)
                        {
                            _logger.LogInformation("Found report by ID: {ReportName}", selectedReport.Name);
                        }
                    }
                }
                
                if (selectedReport == null)
                {
                    _logger.LogWarning("Report not found: {Selection}", selection);
                    return JsonSerializer.Serialize(new { 
                        Success = false,
                        Error = "Report not found. Please provide a valid report name or number.",
                        Message = "I couldn't find a report matching your selection. Please provide a valid report name or number, or we can try searching again with different keywords."
                    });
                }
                
                // Update the context with the selected report
                _context.SelectedReport = selectedReport;
                _context.State = AgentState.SsrsApiRetrieval;
                
                return JsonSerializer.Serialize(new { 
                    Success = true, 
                    SelectedReport = selectedReport,
                    Message = $"I'll use the '{selectedReport.Name}' report. Let me retrieve the parameters for this report now."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error selecting report: {Message}", ex.Message);
                return JsonSerializer.Serialize(new { 
                    Success = false,
                    Error = $"Error selecting report: {ex.Message}",
                    Message = "I encountered an error while selecting the report. Let's try again. Could you specify which report you want to use?"
                });
            }
        }
    }
    
    /// <summary>
    /// Determines if a message is likely to be chitchat (greeting, etc.)
    /// </summary>
    private bool IsChitchatMessage(string message)
    {
        // Normalize the message for case-insensitive comparison
        string normalizedMessage = message.ToLowerInvariant().Trim();
        
        // Check for common greetings and chitchat patterns
        string[] chitchatPatterns = 
        {
            "^hi$", "^hello$", "^hey$", "^hi there$", "^hello there$", "^hey there$",
            "^good morning$", "^good afternoon$", "^good evening$", "^howdy$",
            "^how are you$", "^how are you doing$", "^how's it going$", "^what's up$",
            "^greetings$", "^yo$", "^hiya$", "^sup$", "^test$", "^testing$",
            "^are you there$", "^you there$", "^anybody home$", "^anyone there$"
        };
        
        // Check if the message matches any of the chitchat patterns
        return chitchatPatterns.Any(pattern => System.Text.RegularExpressions.Regex.IsMatch(normalizedMessage, pattern));
    }
    
    // In a real application, these would use a distributed cache, database, or state store
    // For simplicity, we're using an in-memory dictionary here
    private static readonly Dictionary<string, ChatContext> _sessionStore = new();
    
    /// <summary>
    /// Gets or creates a chat context for the given session ID
    /// </summary>
    private static ChatContext GetOrCreateContext(string sessionId)
    {
        if (!_sessionStore.TryGetValue(sessionId, out var context))
        {
            context = new ChatContext();
            _sessionStore[sessionId] = context;
        }
        
        return context;
    }
    
    /// <summary>
    /// Saves the chat context for the given session ID
    /// </summary>
    private static void SaveContext(string sessionId, ChatContext context)
    {
        _sessionStore[sessionId] = context;
    }
    
    /// <summary>
    /// Gets the system prompt for the AI model
    /// </summary>
    private string GetSystemPrompt()
    {
        return @"You are a helpful assistant that helps users find and run SQL Server Reporting Services (SSRS) reports.
Your task is to guide users through the complete process of selecting a report, filling in required parameters, and generating a URL to view the report.

Follow this workflow:
1. Help the user select a report based on their needs
2. Once a report is selected, retrieve its details and parameters automatically
3. Guide the user through filling in each required parameter
4. When all required parameters are provided, generate the report URL

You have the following tools available:
- Advanced search for reports using documentation and the report database (ReportSelector.FindReportsAsync) - USE THIS FIRST when finding reports
- Regular search for reports by name, topic, or description (SsrsService.SearchReportsAsync) - only use as a backup
- Get all available reports (SsrsService.GetAllReportsAsync)
- Get report details (SsrsService.GetReportDetailsAsync)
- Select a specific report (ReportSelector.SelectReportAsync)
- Set parameter values and validate them (ReportParameters.SetParameterValue)
- Get parameters for a report (ReportParameters.GetParameters)
- Get missing required parameters (ReportParameters.GetMissingRequiredParameters)
- Generate report URLs (ReportParameters.GenerateReportUrl)

For finding reports, ALWAYS use ReportSelector.FindReportsAsync FIRST, as it provides the most comprehensive results by searching through documentation and the database.

IMPORTANT WORKFLOW GUIDELINES:
- When a user asks for a report, IMMEDIATELY call ReportSelector.FindReportsAsync with their request
- If multiple reports are found, help the user choose by showing the options and waiting for their selection
- When a report is selected, IMMEDIATELY call SsrsService.GetReportDetailsAsync to get parameters
- After getting parameters, guide the user through providing values for each required parameter
- Once all parameters are provided, generate the report URL

Be conversational and helpful. Explain what parameters are needed and why. Verify parameter values when appropriate.
Always inform the user what you're doing at each step of the process.

Never leave a workflow step hanging - always follow through to completion. If you say you're going to search for reports, make sure you actually call the appropriate function and show results.";
    }
}
