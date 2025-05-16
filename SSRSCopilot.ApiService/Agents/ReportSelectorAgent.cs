using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
//using Microsoft.SemanticKernel.Connectors.OpenAI.AzureOpenAI;
//using Microsoft.SemanticKernel.Connectors.OpenAI.ChatCompletionWithData;
using SSRSCopilot.ApiService.Models;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Azure;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Azure.AI.OpenAI.Chat;

namespace SSRSCopilot.ApiService.Agents;



/// <summary>
/// Agent responsible for helping users select a report using Azure OpenAI with "Use Your Own Data"
/// </summary>
public class ReportSelectorAgent : IAgent
{
    private readonly Kernel _kernel;
    private readonly ILogger<ReportSelectorAgent> _logger;
    private readonly string _searchEndpoint;
    private readonly string _searchKey;
    private readonly string _searchIndex;
    private readonly string _deploymentName;
    
    public ReportSelectorAgent(
        Kernel kernel,
        IConfiguration configuration,
        ILogger<ReportSelectorAgent> logger)
    {
        _kernel = kernel;
        _logger = logger;
        
        // Load configuration values
        _deploymentName = configuration["AzureOpenAI:DeploymentName"] ?? "gpt-4";
        _searchEndpoint = configuration["AzureSearch:Endpoint"] ?? 
            throw new ArgumentNullException("AzureSearch:Endpoint configuration is required");
        
        // In production, use Key Vault reference or managed identity
        _searchKey = configuration["AzureSearch:ApiKey"] ?? 
            throw new ArgumentNullException("AzureSearch:ApiKey configuration is required");

        _searchIndex = configuration["AzureSearch:IndexName"] ??
            throw new ArgumentNullException("AzureSearch:IndexName configuration is required");
    }
    
    /// <inheritdoc/>
    public async Task<ChatResponse> ProcessMessageAsync(string userMessage, ChatContext context)
    {
        try
        {
            // Add the user message to the history
            context.History.Add(new ChatMessage { Role = "user", Content = userMessage });

            // If we already have a selected report, move to the next agent
            if (context.SelectedReport != null)
            {
                context.State = AgentState.ParameterFilling;
                return new ChatResponse
                {
                    Message = $"I see you've already selected the '{context.SelectedReport.Name}' report. Let's fill in the required parameters.",
                    State = AgentState.ParameterFilling
                };
            }

            // Create a chat history from the context for the LLM
            var chatHistory = new ChatHistory();

            // System message with instructions
            chatHistory.AddSystemMessage(@"
You are a helpful assistant that helps users find SSRS (SQL Server Reporting Services) reports.
Your task is to understand the user's request and search for relevant reports.
When you find reports, return them in a structured JSON format with these fields:
- Name: The name of the report
- Id: The report identifier
- Description: A brief description of what the report contains
- Parameters: Array of parameter objects with Name and Type fields
");

            // Add previous conversation for context
            foreach (var message in context.History)
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

            // Create the search query from user message
            var searchQuery = $"Find reports related to: {userMessage}";
            chatHistory.AddUserMessage(searchQuery);

            // var azureSearchExtensionConfiguration = new AzureSearchChatExtensionConfiguration
            // {
            //     SearchEndpoint = new Uri(Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_ENDPOINT")),
            //     Authentication = new OnYourDataApiKeyAuthenticationOptions(Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_API_KEY")),
            //     IndexName = Environment.GetEnvironmentVariable("AZURE_AI_SEARCH_INDEX")
            // };

            // var chatExtensionsOptions = new AzureChatExtensionsOptions { Extensions = { azureSearchExtensionConfiguration } };
            // var executionSettings = new OpenAIPromptExecutionSettings { AzureChatExtensionsOptions = chatExtensionsOptions };

            // var result = await kernel.InvokePromptAsync("What are my available health plans?", new(executionSettings));

            // Set up Azure AI Search as a data source
#pragma warning disable AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                AzureChatDataSource = new AzureSearchChatDataSource
                {
                    Endpoint = new Uri(_searchEndpoint),
                    Authentication = DataSourceAuthentication.FromApiKey(_searchKey),
                    IndexName = _searchIndex,
                    QueryType = DataSourceQueryType.VectorSemanticHybrid,
                    SemanticConfiguration = "azureml-default", // TODO: Move to config
                    VectorizationSource = DataSourceVectorizer.FromDeploymentName("text-embedding-3-large") //TODO: Move to config
                }
            };
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
#pragma warning restore AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

            // Get chat completion service from kernel
            var chatCompletionService = _kernel.Services.GetRequiredService<IChatCompletionService>();

            // Call the service with the configured settings
            var result = await chatCompletionService.GetChatMessageContentAsync(
                chatHistory, 
            executionSettings);

            var reportSearchResponse = result.Content;

            // Process the result to extract reports
            var reports = await ParseReportsFromResponseAsync(result.ToString());

            if (reports.Count == 0)
            {
                // No reports found
                var noReportsMessage = "I couldn't find any reports matching your request. Could you provide more details about the type of report you're looking for?";
                context.History.Add(new ChatMessage { Role = "assistant", Content = noReportsMessage });

                return new ChatResponse
                {
                    Message = noReportsMessage,
                    State = AgentState.ReportSelection
                };
            }

            if (reports.Count == 1)
            {
                // Only one report found, select it automatically
                context.SelectedReport = reports[0];
                context.State = AgentState.ParameterFilling;

                var singleReportMessage = $"I found the '{reports[0].Name}' report which matches your request. This report {reports[0].Description}. Shall we use this report?";
                context.History.Add(new ChatMessage { Role = "assistant", Content = singleReportMessage });

                return new ChatResponse
                {
                    Message = singleReportMessage,
                    State = AgentState.ParameterFilling
                };
            }

            // Multiple reports found, ask user to select one
            var promptBuilder = new StringBuilder();
            promptBuilder.AppendLine("I found several reports that might match what you're looking for:");
            promptBuilder.AppendLine();

            for (int i = 0; i < reports.Count; i++)
            {
                promptBuilder.AppendLine($"{i + 1}. {reports[i].Name}: {reports[i].Description}");
            }

            promptBuilder.AppendLine();
            promptBuilder.AppendLine("Which one would you like to use? You can select by number or name, or ask for more details about any of them.");

            var promptMessage = promptBuilder.ToString();
            context.History.Add(new ChatMessage { Role = "assistant", Content = promptMessage });

            // Add the reports to the context for later reference
            context.History.Add(new ChatMessage { Role = "system", Content = JsonSerializer.Serialize(reports) });

            return new ChatResponse
            {
                Message = promptMessage,
                State = AgentState.ReportSelection
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ReportSelectorAgent: {Message}", ex.Message);
            return new ChatResponse
            {
                Message = "I'm sorry, but I encountered an error while searching for reports. Please try again or provide more details about what you're looking for.",
                State = AgentState.ReportSelection
            };
        }
    }
    
    /// <summary>
    /// Parses the LLM response to extract report information
    /// </summary>
    private async Task<List<Report>> ParseReportsFromResponseAsync(string llmResponse)
    {
        try
        {
            // Create a function to extract structured report data
            var extractPrompt = @"
    You are a JSON parsing expert. Extract report information from the text and format it as a JSON array.
    Each report should have these properties:
    - Name: string (required)
    - Id: string (required)
    - Description: string (required)
    - Parameters: array of objects, each with Name (string), Description (string), IsRequired (bool), and DataType (string) properties if available.

    Return ONLY valid JSON without any explanation or markdown formatting.
    Example: {""reports"":[{ ""Name"": ""Report1"", ""Id"": ""id1"", ""Description"": ""desc"", ""Parameters"": [{""Name"": ""Troop"", ""IsRequired"": ""True"", ""Description"": ""Filters records by selected troop letter.""}] }]}

    TEXT TO PARSE:
    {{$input}}
    ";

            var extractFunction = _kernel.CreateFunctionFromPrompt(
                extractPrompt,
                new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.0,
                    ResponseFormat = "json_object",
                    MaxTokens = 1000
                });

            var result = await _kernel.InvokeAsync(
                extractFunction,
                new KernelArguments { ["input"] = llmResponse });

            // Parse the result into a list of reports
            string jsonContent = result.ToString();

            // Log the received JSON for debugging
            _logger.LogDebug("Received JSON response: {JsonContent}", jsonContent);

            // Try parsing the JSON - using multiple strategies
            List<Report> reports = null;

            try
            {
                // Try with a reports property wrapper
                var wrapper = JsonSerializer.Deserialize<ReportsWrapper>(
                    jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (wrapper?.Reports != null && wrapper.Reports.Count > 0)
                {
                    reports = wrapper.Reports;
                    _logger.LogInformation("Successfully parsed JSON using ReportsWrapper");
                }
            }
            catch (JsonException firstException)
            {
                _logger.LogWarning("Wrapper parsing failed: {Message}", firstException.Message);

                try
                {
                    // Try direct deserialization first (array of reports)
                    reports = JsonSerializer.Deserialize<List<Report>>(jsonContent) ?? new List<Report>();
                    _logger.LogInformation("Successfully parsed JSON as List<Report>");
                }
                catch (JsonException secondException)
                {
                    _logger.LogWarning("Direct JSON parsing failed: {Message}", secondException.Message);

                    // Try with a custom JsonDocument approach for more flexibility
                    try
                    {
                        using (JsonDocument doc = JsonDocument.Parse(jsonContent))
                        {
                            // Check for reports array
                            if (doc.RootElement.TryGetProperty("reports", out JsonElement reportsElement) &&
                                reportsElement.ValueKind == JsonValueKind.Array)
                            {
                                reports = new List<Report>();

                                foreach (JsonElement reportElement in reportsElement.EnumerateArray())
                                {
                                    try
                                    {
                                        var options = new JsonSerializerOptions
                                        {
                                            PropertyNameCaseInsensitive = true
                                        };

                                        var report = JsonSerializer.Deserialize<Report>(reportElement.GetRawText(), options);
                                        if (report != null)
                                        {
                                            reports.Add(report);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning("Failed to parse individual report: {Message}", ex.Message);
                                    }
                                }

                                _logger.LogInformation("Successfully parsed {Count} reports using JsonDocument", reports.Count);
                            }
                        }
                    }
                    catch (Exception jsonDocException)
                    {
                        _logger.LogError("All JSON parsing attempts failed: {Message}", jsonDocException.Message);
                    }
                }
            }

            return reports ?? new List<Report>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing reports from LLM response: {Message}", ex.Message);
            return new List<Report>();
        }
    }

    /// <summary>
    /// Helper class for JSON deserialization
    /// </summary>
    private class ReportsWrapper
    {
        public List<Report> Reports { get; set; } = new List<Report>();
    }
}