using Microsoft.SemanticKernel;
using SSRSCopilot.ApiService.Models;
using System.Text;
using System.Text.Json;

namespace SSRSCopilot.ApiService.Agents;

/// <summary>
/// Agent responsible for helping users fill in report parameters
/// </summary>
public class ParameterFillerAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<ParameterFillerAgent> _logger;
    
    public ParameterFillerAgent(
        Kernel kernel,
        ILogger<ParameterFillerAgent> logger)
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
            
            // Validate that we have a selected report
            if (context.SelectedReport == null)
            {
                context.State = AgentState.ReportSelection;
                return new ChatResponse
                {
                    Message = "Let's first select a report before filling in parameters. What kind of report are you looking for?",
                    State = AgentState.ReportSelection
                };
            }
            
            // Check for missing required parameters
            var missingParameters = context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !context.ParameterValues.ContainsKey(p.Name))
                .ToList();
            
            if (missingParameters.Count == 0)
            {
                // All required parameters are filled
                context.State = AgentState.ReportUrlCreation;
                
                var allParamsFilledMessage = "Great! I have all the necessary parameters. I'll generate the report for you now.";
                context.History.Add(new ChatMessage { Role = "assistant", Content = allParamsFilledMessage });
                
                return new ChatResponse
                {
                    Message = allParamsFilledMessage,
                    State = AgentState.ReportUrlCreation
                };
            }
            
            // Process the user message to extract parameter values
            await ExtractParameterValuesAsync(userMessage, context);
            
            // After extraction, check again for missing parameters
            missingParameters = context.SelectedReport.Parameters
                .Where(p => p.IsRequired && !context.ParameterValues.ContainsKey(p.Name))
                .ToList();
            
            if (missingParameters.Count == 0)
            {
                // All required parameters are now filled
                context.State = AgentState.ReportUrlCreation;
                
                var paramsFilledMessage = "Perfect! I have all the information I need. I'll generate the report for you now.";
                context.History.Add(new ChatMessage { Role = "assistant", Content = paramsFilledMessage });
                
                return new ChatResponse
                {
                    Message = paramsFilledMessage,
                    State = AgentState.ReportUrlCreation
                };
            }
            
            // Ask for the next missing parameter
            var nextParameter = missingParameters[0];
            
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine($"For the {context.SelectedReport.Name} report, I need some more information:");
            promptBuilder.AppendLine();
            
            promptBuilder.AppendLine($"Please provide a value for '{nextParameter.Name}': {nextParameter.Description}");
            
            if (nextParameter.AllowedValues != null && nextParameter.AllowedValues.Count > 0)
            {
                promptBuilder.AppendLine();
                promptBuilder.AppendLine("Allowed values:");
                foreach (var value in nextParameter.AllowedValues)
                {
                    promptBuilder.AppendLine($"- {value}");
                }
            }
            
            var promptMessage = promptBuilder.ToString();
            context.History.Add(new ChatMessage { Role = "assistant", Content = promptMessage });
            
            return new ChatResponse
            {
                Message = promptMessage,
                State = AgentState.ParameterFilling
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ParameterFillerAgent");
            return new ChatResponse
            {
                Message = "I'm sorry, but I encountered an error while processing the report parameters. Please try again.",
                State = AgentState.ParameterFilling
            };
        }
    }
    
    /// <summary>
    /// Extracts parameter values from the user message
    /// </summary>
    private async Task ExtractParameterValuesAsync(string userMessage, ChatContext context)
    {
        // Create a system prompt for the LLM
        var systemPrompt = new StringBuilder();
        systemPrompt.AppendLine("You are an assistant that extracts parameter values from user messages.");
        systemPrompt.AppendLine("The user is providing values for report parameters.");
        systemPrompt.AppendLine("Extract any parameter values mentioned in their message.");
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("The available parameters are:");
        
        foreach (var param in context.SelectedReport!.Parameters)
        {
            systemPrompt.AppendLine($"- {param.Name}: {param.Description}");
            if (param.AllowedValues != null && param.AllowedValues.Count > 0)
            {
                systemPrompt.AppendLine($"  Allowed values: {string.Join(", ", param.AllowedValues)}");
            }
            systemPrompt.AppendLine($"  Data type: {param.DataType}");
        }
        
        systemPrompt.AppendLine();
        systemPrompt.AppendLine("Return a JSON object mapping parameter names to their values.");
        systemPrompt.AppendLine("Example: { \"Month\": \"January\", \"Region\": \"North\" }");
        
        // Use Semantic Kernel to extract parameter values
        var promptOptions = new PromptExecutionSettings()
        {
            ModelId = "gpt-4"
        };
        
        var extractFunction = _kernel.CreateFunctionFromPrompt(
            systemPrompt.ToString(),
            promptOptions);
        
        var result = await _kernel.InvokeAsync(extractFunction, new KernelArguments() { ["input"] = userMessage });
        
        try
        {
            var extractedParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(
                result.ToString(),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            if (extractedParameters != null)
            {
                foreach (var param in extractedParameters)
                {
                    // Check if this is a valid parameter
                    if (context.SelectedReport.Parameters.Any(p => p.Name.Equals(param.Key, StringComparison.OrdinalIgnoreCase)))
                    {
                        context.ParameterValues[param.Key] = param.Value;
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse extracted parameters as JSON");
        }
    }
}
