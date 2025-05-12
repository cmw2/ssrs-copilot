using Azure;
using Azure.Search.Documents;
using Microsoft.SemanticKernel;
using SSRSCopilot.ApiService.Agents;
using SSRSCopilot.ApiService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Azure Search
builder.Services.AddSingleton<SearchClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    string searchServiceEndpoint = config["AzureSearch:Endpoint"] ?? "https://example.search.windows.net";
    string indexName = config["AzureSearch:IndexName"] ?? "reports";
    string apiKey = config["AzureSearch:ApiKey"] ?? "demo-key";

    return new SearchClient(
        new Uri(searchServiceEndpoint),
        indexName,
        new AzureKeyCredential(apiKey));
});

// Configure Azure OpenAI services
builder.Services.AddSingleton<Kernel>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    string endpoint = config["AzureOpenAI:Endpoint"] ?? "https://example.openai.azure.com";
    string apiKey = config["AzureOpenAI:ApiKey"] ?? "demo-key";
    string deploymentName = config["AzureOpenAI:DeploymentName"] ?? "gpt-4";    // For development/testing without Azure OpenAI, we can use OpenAI directly
    // Remove this in production and use Azure OpenAI with proper authentication
    var kernel = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(
            modelId: "gpt-4-turbo", 
            apiKey: config["OpenAI:ApiKey"] ?? "sk-dummy-key")
        .Build();
    
    return kernel;

    // Azure OpenAI configuration for production
    // return new KernelBuilder()
    //     .AddAzureOpenAIChatCompletion(
    //         deploymentName: deploymentName,
    //         endpoint: endpoint,
    //         apiKey: apiKey)
    //     .Build();
});

// Register services
builder.Services.AddSingleton<IReportService, AzureSearchReportService>();
builder.Services.AddSingleton<IReportUrlService, SsrsReportUrlService>();

// Register agents
builder.Services.AddSingleton<ReportSelectorAgent>();
builder.Services.AddSingleton<ParameterFillerAgent>();
builder.Services.AddSingleton<ReportUrlCreatorAgent>();
builder.Services.AddSingleton<AgentOrchestrator>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
