using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Plugins;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Service for handling chat interactions with auto function calling.
/// Uses the LLM to orchestrate the conversation flow without explicit state tracking.
/// </summary>
public class ChatService
{
    private readonly Kernel _kernel;
    private readonly Dictionary<string, ChatHistory> _sessionChats = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="kernel">The semantic kernel instance</param>
    public ChatService(Kernel kernel)
    {
        _kernel = kernel;
    }

    /// <summary>
    /// Processes a chat message and generates a response using auto function calling.
    /// The LLM intelligently decides when to call functions from registered plugins.
    /// </summary>
    /// <param name="request">The chat request containing the user message and session ID</param>
    /// <returns>The chat response with optional report URL</returns>
    public async Task<ChatResponse> ProcessMessageAsync(ChatRequest request)
    {
        string sessionId = request.SessionId ?? string.Empty;
        
        // Get or create chat history for this session
        if (!_sessionChats.TryGetValue(sessionId, out var chatHistory))
        {
            chatHistory = new ChatHistory();
            
            // Set initial system message to guide the conversation
            chatHistory.AddSystemMessage(@"
You are a helpful assistant that helps users find and run SSRS (SQL Server Reporting Services) reports.
Your task is to guide users through the complete process of selecting a report, filling in required parameters, and generating a URL to view the report.

Follow this workflow:
1. Help the user select a report based on their description
2. Get the report details and required parameters 
3. Ask the user for values for each required parameter and potentially optional parameters
4. When all parameters are provided, generate the report URL

IMPORTANT - SEMANTIC MATCHING REQUIRED: Report titles in the documentation (stored in Azure Search) often differ from the actual report names in SSRS. You need to perform semantic matching to find the best SSRS report that matches the documentation title:

1. First search for reports in the documentation using SearchReportsAsync from ReportSearchPlugin
2. When the user selects a report from documentation, call GetAllReportsAsync from SsrsPlugin to get all SSRS reports
3. Semantically compare the documentation title with all SSRS report names:
   - Look for similarities in keywords, business terms, and concepts
   - Compare report paths, descriptions, and names
   - Consider abbreviations, different word orders, and synonyms
   - Select the SSRS report with the highest semantic similarity
4. Use the matched SSRS report id when calling other SSRS functions
5. If you're unsure about the match, explain to the user that you're finding the best matching report

For example, if the documentation has a report titled 'Monthly Sales by Region' but the actual SSRS report is named 'RegionalSalesMonthly', you should semantically recognize they're referring to the same report.

When you generate a report URL, include it in your response with a prefix 'REPORT_URL:' on its own line.  Let the user know that they should see the report open to the side.
Example: 'REPORT_URL:https://reportsserver/reports/report1?param1=value1&param2=value2'

Be conversational and helpful. Explain what parameters are needed and why.
Always inform the user what you're doing at each step of the process.
");
            
            _sessionChats[sessionId] = chatHistory;
        }

        // Add the user message to the chat history
        chatHistory.AddUserMessage(request.Message);

        // Create execution settings with auto function calling enabled
        var executionSettings = new OpenAIPromptExecutionSettings
        {
            ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            Temperature = 0.3,
            TopP = 0.95,
        };

        // Get the chat completion service
        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        try
        {
            // Get the response from the model with auto function calling
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel
            );

            // Extract the report URL if it's in the message
            string? reportUrl = null;
            var message = result.Content ?? string.Empty;

            // Look for report URL in the message - it will be prefixed with "REPORT_URL:"
            var reportUrlMarker = "REPORT_URL:";
            var reportUrlIndex = message.IndexOf(reportUrlMarker);
            if (reportUrlIndex >= 0)
            {
                var urlStart = reportUrlIndex + reportUrlMarker.Length;
                var urlEnd = message.IndexOf('\n', urlStart);
                if (urlEnd < 0)
                {
                    urlEnd = message.Length;
                }

                reportUrl = message.Substring(urlStart, urlEnd - urlStart).Trim();
                
                // Remove the URL marker from the message
                message = message.Remove(reportUrlIndex, urlEnd - reportUrlIndex);
                message = message.Trim();
            }

            // Add the assistant message to the history
            chatHistory.AddAssistantMessage(message);

            // Return the chat response
            return new ChatResponse
            {
                Message = message,
                ReportUrl = reportUrl
            };
        }
        catch (Exception ex)
        {
            // Add the error message to the chat history
            var errorMessage = $"I'm sorry, but I encountered an error: {ex.Message}. This might be due to difficulties finding the right report or matching the documentation title to the actual SSRS report name. Let's try again with more details. What kind of report are you looking for?";
            chatHistory.AddAssistantMessage(errorMessage);

            // Return the error response
            return new ChatResponse
            {
                Message = errorMessage
            };
        }
    }
}
