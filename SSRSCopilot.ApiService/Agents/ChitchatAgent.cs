using Microsoft.SemanticKernel;
using SSRSCopilot.ApiService.Models;
using System.Text;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Agent responsible for handling chitchat messages that aren't related to reports
/// </summary>
public class ChitchatAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<ChitchatAgent> _logger;
    
    public ChitchatAgent(
        Kernel kernel,
        ILogger<ChitchatAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
    }
    
    /// <inheritdoc/>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        try
        {
            // Add the user message to the history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });
            
            // Create a system prompt for the chitchat responses
            var systemPrompt = new StringBuilder();
            systemPrompt.AppendLine("You are a friendly assistant helping with SQL Server Reporting Services (SSRS) reports.");
            systemPrompt.AppendLine("The user has sent a greeting or a casual message not related to reports.");
            systemPrompt.AppendLine("Respond in a friendly manner and gently guide the conversation toward selecting reports.");
            systemPrompt.AppendLine("Keep your response short, friendly and helpful.");
            
            // Use Semantic Kernel to generate a response
            var promptOptions = new PromptExecutionSettings
            {
                ModelId = "gpt-4o"
            };
            
            var chitchatFunction = _kernel.CreateFunctionFromPrompt(
                systemPrompt.ToString(),
                promptOptions);
            
            var result = await _kernel.InvokeAsync(chitchatFunction, new KernelArguments() { ["input"] = userMessage });
            
            var responseContent = result.ToString();
            
            // Add the response to the history
            context.History.Add(new ChatMessage { Role = "assistant", Content = responseContent });
            
            // Stay in the current state - typically this would be ReportSelection
            // We don't change the state when handling chitchat
            return new ChatResponse
            {
                Message = responseContent,
                State = context.State
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChitchatAgent");
            return new ChatResponse
            {
                Message = "I'm sorry, but I encountered an error. How can I help you find a report?",
                State = context.State
            };
        }
    }
}
