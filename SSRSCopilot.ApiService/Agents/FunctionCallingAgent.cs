using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using SSRSCopilot.ApiService.Models;
using SSRSCopilot.ApiService.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Main agent that uses function calling to control the conversation flow
/// This agent handles the report selection, parameter filling, and URL creation
/// </summary>
public class FunctionCallingAgent
{
    private readonly ReportSelectorAgent _reportSelectorAgent;
    private readonly ISsrsRestApiService _ssrsRestApiService;
    private readonly IReportUrlService _reportUrlService;
    private readonly Kernel _kernel;
    private readonly ILogger<FunctionCallingAgent> _logger;

    public FunctionCallingAgent(
        ReportSelectorAgent reportSelectorAgent,
        ISsrsRestApiService ssrsRestApiService,
        IReportUrlService reportUrlService,
        Kernel kernel,
        ILogger<FunctionCallingAgent> logger)
    {
        _reportSelectorAgent = reportSelectorAgent;
        _ssrsRestApiService = ssrsRestApiService;
        _reportUrlService = reportUrlService;
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Process a user message and generate a response
    /// </summary>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        try
        {
            // Add the user message to the history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });

            // Register functions with the kernel, passing along the context
            RegisterFunctions(context);

            // If we don't have a report selected, we need to pass the message to the report selector agent
            if (context.SelectedReport == null)
            {
                return await _reportSelectorAgent.ProcessMessageAsync(userMessage, context);
            }

            // Create the chat history for the function-calling agent
            var chatHistory = CreateChatHistory(context);

            // Get chat completion service from kernel
            var chatCompletionService = _kernel.Services.GetRequiredService<IChatCompletionService>();

            // Create settings for function calling
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Call the service with function calling enabled
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
                executionSettings);

            // Add the assistant's message to the history if we got a response
            var content = result?.Content ?? "I'm having trouble generating a response. Let's try again.";
            context.History.Add(new ChatMessage { Role = "assistant", Content = content });

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
            _logger.LogError(ex, "Error in FunctionCallingAgent.ProcessMessageAsync");

            return new ChatResponse
            {
                Message = "I'm sorry, but something went wrong. Let's start over. What kind of report are you looking for?",
                State = AgentState.ReportSelection
            };
        }
    }

    /// <summary>
    /// Register functions with the kernel
    /// </summary>
    private void RegisterFunctions(ChatContext? context = null)
    {
        // We're going to use a try-catch approach to handle plugin registration
        // This avoids the need to use API-specific methods for checking/removing plugins
        
        try
        {
            // Create and try to register the SSRS service plugin
            var ssrsServicePlugin = new SsrsServicePlugin(_ssrsRestApiService, context);
            _kernel.Plugins.AddFromObject(ssrsServicePlugin, nameof(SsrsServicePlugin));
        }
        catch (ArgumentException)
        {
            // Plugin already exists, we can ignore this error
            _logger.LogDebug("SsrsServicePlugin already registered, skipping registration");
        }
        
        try
        {
            // Create and try to register the parameter validation plugin
            var reportParamPlugin = new ReportParameterPlugin(_reportUrlService, context);
            _kernel.Plugins.AddFromObject(reportParamPlugin, nameof(ReportParameterPlugin));
        }
        catch (ArgumentException)
        {
            // Plugin already exists, we can ignore this error
            _logger.LogDebug("ReportParameterPlugin already registered, skipping registration");
        }
    }

    /// <summary>
    /// Create a chat history for the function-calling agent based on the current context
    /// </summary>
    private ChatHistory CreateChatHistory(ChatContext context)
    {
        var chatHistory = new ChatHistory();

        // Add system message with instructions based on the current state
        string systemPrompt;

        systemPrompt = @"You are a helpful assistant that helps users find and run SQL Server Reporting Services (SSRS) reports.
Your task is to guide users through the process of selecting a report, filling in required parameters, and generating a URL to view the report.

You have tools available to help you with this task:
1. You can get details about reports from the SSRS server
2. You can validate and collect parameter values for reports
3. You can generate URLs to view reports

Follow these steps:
1. If the user has already selected a report, confirm the selection and move on to parameters
2. Get the parameters required for the report
3. Work with the user to fill in all required parameters, asking for each one step by step
4. Once all required parameters are filled, generate a report URL and let the user know it's ready
5. Use FINAL_ANSWER: when generating the final report URL to indicate when you're done with the full process

Be conversational and helpful. Explain what parameters are needed and why. Verify parameter values when appropriate.";

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
    /// Plugin for SSRS service functions
    /// </summary>
    public class SsrsServicePlugin
    {
        private readonly ISsrsRestApiService _ssrsRestApiService;
        private readonly ChatContext? _context;

        public SsrsServicePlugin(ISsrsRestApiService ssrsRestApiService, ChatContext? context)
        {
            _ssrsRestApiService = ssrsRestApiService;
            _context = context;
        }

        /// <summary>
        /// Get a list of all reports from the SSRS server
        /// </summary>
        [KernelFunction]
        [Description("Get a list of all reports from the SSRS server")]
        public async Task<string> GetAllReportsAsync()
        {
            var reports = await _ssrsRestApiService.GetReportsAsync();
            
            // Return a simplified list of reports for better rendering
            var simpleReports = reports.Select(r => new { r.Name, r.Description }).ToList();
            return JsonSerializer.Serialize(simpleReports);
        }

        /// <summary>
        /// Get details about a specific report by name
        /// </summary>
        [KernelFunction]
        [Description("Get details about a specific report by name")]
        public async Task<string> GetReportDetailsByNameAsync(
            [Description("The name of the report to get details for")] string reportName)
        {
            var report = await _ssrsRestApiService.GetReportByNameAsync(reportName);
            
            if (report == null)
            {
                return JsonSerializer.Serialize(new { Error = "Report not found" });
            }

            // Update the context with the selected report if context is available
            if (_context != null)
            {
                _context.SelectedReport = report;
                _context.State = AgentState.ParameterFilling;
            }

            return JsonSerializer.Serialize(report);
        }

        /// <summary>
        /// Get parameters for a selected report
        /// </summary>
        [KernelFunction]
        [Description("Get parameters for the currently selected report")]
        public string GetSelectedReportParameters()
        {
            if (_context == null || _context.SelectedReport == null)
            {
                return JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }

            return JsonSerializer.Serialize(_context.SelectedReport.Parameters);
        }
    }

    /// <summary>
    /// Plugin for parameter validation and URL generation
    /// </summary>
    public class ReportParameterPlugin
    {
        private readonly IReportUrlService _reportUrlService;
        private readonly ChatContext? _context;

        public ReportParameterPlugin(IReportUrlService reportUrlService, ChatContext? context)
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
            if (_context == null || _context.SelectedReport == null)
            {
                return JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }

            // Check if this is a valid parameter
            var parameter = _context.SelectedReport.Parameters.FirstOrDefault(p => 
                p.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase));

            if (parameter == null)
            {
                return JsonSerializer.Serialize(new { Error = $"Parameter '{parameterName}' not found" });
            }

            // Validate the parameter value if it has allowed values
            if (parameter.AllowedValues != null && parameter.AllowedValues.Any())
            {
                bool isValid = parameter.AllowedValues.Any(v => 
                    v.Equals(parameterValue, StringComparison.OrdinalIgnoreCase));

                if (!isValid)
                {
                    return JsonSerializer.Serialize(new 
                    { 
                        Error = $"Invalid value for parameter '{parameterName}'. Allowed values: {string.Join(", ", parameter.AllowedValues)}" 
                    });
                }
            }

            // Set the parameter value
            _context.ParameterValues[parameter.Name] = parameterValue;

            return JsonSerializer.Serialize(new { Success = true, Message = $"Parameter '{parameterName}' set to '{parameterValue}'" });
        }

        /// <summary>
        /// Get the missing required parameters for the selected report
        /// </summary>
        [KernelFunction]
        [Description("Get the missing required parameters for the selected report")]
        public string GetMissingRequiredParameters()
        {
            if (_context == null || _context.SelectedReport == null)
            {
                return JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }

            var missingParameters = _context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !_context.ParameterValues.ContainsKey(p.Name))
                .ToList();

            return JsonSerializer.Serialize(missingParameters);
        }

        /// <summary>
        /// Generate a URL for the selected report with the current parameter values
        /// </summary>
        [KernelFunction]
        [Description("Generate a URL for the selected report with the current parameter values")]
        public string GenerateReportUrl(
            [Description("The format of the report (default: PDF)")] string format = "PDF")
        {
            if (_context == null || _context.SelectedReport == null)
            {
                return JsonSerializer.Serialize(new { Error = "No report is currently selected" });
            }

            // Check for missing required parameters
            var missingParameters = _context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !_context.ParameterValues.ContainsKey(p.Name))
                .ToList();

            if (missingParameters.Any())
            {
                return JsonSerializer.Serialize(new 
                { 
                    Error = "Missing required parameters", 
                    MissingParameters = missingParameters.Select(p => p.Name).ToList() 
                });
            }

            // Generate the report URL
            string reportUrl = _reportUrlService.GenerateReportUrl(_context.SelectedReport, _context.ParameterValues, format);

            // Save the URL in the context
            _context.ReportUrl = reportUrl;
            _context.State = AgentState.Completed;

            return JsonSerializer.Serialize(new { Success = true, Url = reportUrl });
        }
    }
}
