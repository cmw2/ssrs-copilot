using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.SemanticKernel;
using SSRSCopilot.Agent.Models;
using SSRSCopilot.Agent.Plugins;
using SSRSCopilot.Agent.Services;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.Net;
using System.Net.Security;


var builder = WebApplication.CreateBuilder(args);

// Add service defaults (logging, telemetry, etc.)
builder.AddServiceDefaults();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Azure OpenAI Chat Completion at the service level
builder.Services.AddAzureOpenAIChatCompletion(
    deploymentName: builder.Configuration["AzureOpenAI:DeploymentName"] 
        ?? throw new InvalidOperationException("AzureOpenAI:DeploymentName configuration is required"),
    endpoint: builder.Configuration["AzureOpenAI:Endpoint"] 
        ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is required"),
    apiKey: builder.Configuration["AzureOpenAI:ApiKey"] 
        ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is required"),
    apiVersion: builder.Configuration["AzureOpenAI:ApiVersion"]);

// Add Azure OpenAI Embedding service if vector search is enabled
if (bool.TryParse(builder.Configuration["AzureSearch:VectorSearchEnabled"], out bool vectorSearchEnabled) && vectorSearchEnabled)
{
    // Get embedding deployment name, endpoint, and key (fallback to AzureOpenAI settings if not specified)
    string embeddingDeploymentName = !string.IsNullOrEmpty(builder.Configuration["AzureSearch:EmbeddingDeploymentName"]) 
        ? builder.Configuration["AzureSearch:EmbeddingDeploymentName"]! 
        : "text-embedding-large-003";
        
    string embeddingEndpoint = !string.IsNullOrEmpty(builder.Configuration["AzureSearch:EmbeddingEndpoint"]) 
        ? builder.Configuration["AzureSearch:EmbeddingEndpoint"]! 
        : builder.Configuration["AzureOpenAI:Endpoint"] 
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint configuration is required");
            
    string embeddingApiKey = !string.IsNullOrEmpty(builder.Configuration["AzureSearch:EmbeddingApiKey"])
        ? builder.Configuration["AzureSearch:EmbeddingApiKey"]!
        : builder.Configuration["AzureOpenAI:ApiKey"] 
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey configuration is required");

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    builder.Services.AddAzureOpenAIEmbeddingGenerator(
        deploymentName: embeddingDeploymentName,
        endpoint: embeddingEndpoint,
        apiKey: embeddingApiKey);
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}

// Register services
builder.Services.AddSingleton<IAzureSearchService, AzureSearchService>();

// Configure the SSRS HttpClient with NTLM authentication
builder.Services.AddHttpClient<SsrsHttpClient>()
    .ConfigurePrimaryHttpMessageHandler(sp => 
    {
        // Get authentication settings from configuration
        var username = builder.Configuration["Ssrs:Username"];
        var password = builder.Configuration["Ssrs:Password"];
        var domain = builder.Configuration["Ssrs:Domain"];
        
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var configLogger = loggerFactory.CreateLogger("SSRS.Config");
        configLogger.LogInformation("Configuring NTLM authentication with username: {Username}, domain: {Domain}", 
            !string.IsNullOrEmpty(username) ? username : "(not set)", 
            !string.IsNullOrEmpty(domain) ? domain : "(not set)");
        
        // Create an HttpClientHandler for NTLM authentication
        var httpClientHandler = new HttpClientHandler
        {
            PreAuthenticate = true,
            UseDefaultCredentials = false,
            AllowAutoRedirect = true,
            UseCookies = true
        };
        
        // Configure NTLM authentication if credentials are provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            // Get the SSRS API URL from configuration
            var apiBaseUrl = builder.Configuration["Ssrs:ApiBaseUrl"] ?? "http://localhost";
            var uri = new Uri(apiBaseUrl);
            
            // Create credential cache specifically for NTLM authentication
            var credentialCache = new CredentialCache();
            
            // Create the network credential
            var credentials = string.IsNullOrEmpty(domain)
                ? new NetworkCredential(username, password)
                : new NetworkCredential(username, password, domain);
                
            // Add to credential cache with NTLM authentication type
            credentialCache.Add(uri, "NTLM", credentials);
            
            // Set the credentials on the handler
            httpClientHandler.Credentials = credentialCache;
            
            configLogger.LogInformation("NTLM authentication configured with explicit credentials");
        }
        else
        {
            // Try with default credentials if no explicit credentials provided
            httpClientHandler.UseDefaultCredentials = true;
            configLogger.LogWarning("NTLM authentication falling back to default credentials. This may not work in all environments.");
        }
        
        return httpClientHandler;
    })
    .AddHttpMessageHandler(sp => 
    {
        // Add our logging handler to the pipeline
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("SSRS.HttpClient");
        
        // Set to true to see authentication headers (CAUTION: sensitive information)
        bool logAuthHeaders = builder.Environment.IsDevelopment() && 
                             (builder.Configuration["Logging:LogAuthenticationHeaders"]?.ToLower() == "true" ||
                              builder.Configuration["LogAuthenticationHeaders"]?.ToLower() == "true");
        
        return new LoggingHttpMessageHandler(logger, logAuthHeaders);
    });

// Configure the SSRS Report Client with NTLM authentication for report rendering
builder.Services.AddHttpClient<SsrsReportClient>()
    .ConfigurePrimaryHttpMessageHandler(sp => 
    {
        // Reuse the same authentication settings
        var username = builder.Configuration["Ssrs:Username"];
        var password = builder.Configuration["Ssrs:Password"];
        var domain = builder.Configuration["Ssrs:Domain"];
        
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var configLogger = loggerFactory.CreateLogger("SSRS.ReportClient.Config");
        configLogger.LogInformation("Configuring NTLM authentication for report client with username: {Username}, domain: {Domain}", 
            !string.IsNullOrEmpty(username) ? username : "(not set)", 
            !string.IsNullOrEmpty(domain) ? domain : "(not set)");
        
        // Create an HttpClientHandler for NTLM authentication
        var httpClientHandler = new HttpClientHandler
        {
            PreAuthenticate = true,
            UseDefaultCredentials = false,
            AllowAutoRedirect = true,
            UseCookies = true
        };
        
        // Configure NTLM authentication if credentials are provided
        if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
        {
            // Get the Report Viewer URL from configuration for authentication
            var reportViewerUrl = builder.Configuration["Ssrs:ReportViewerUrl"] ?? "http://localhost";
            var uri = new Uri(reportViewerUrl);
            
            // Create credential cache specifically for NTLM authentication
            var credentialCache = new CredentialCache();
            
            // Create the network credential
            var credentials = string.IsNullOrEmpty(domain)
                ? new NetworkCredential(username, password)
                : new NetworkCredential(username, password, domain);
                
            // Add to credential cache with NTLM authentication type
            credentialCache.Add(uri, "NTLM", credentials);
            
            // Set the credentials on the handler
            httpClientHandler.Credentials = credentialCache;
            
            configLogger.LogInformation("NTLM authentication configured for report client with explicit credentials");
        }
        else
        {
            // Try with default credentials if no explicit credentials provided
            httpClientHandler.UseDefaultCredentials = true;
            configLogger.LogWarning("NTLM authentication for report client falling back to default credentials. This may not work in all environments.");
        }
        
        return httpClientHandler;
    })
    .AddHttpMessageHandler(sp => 
    {
        // Add our logging handler to the pipeline
        var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("SSRS.ReportClient");
        
        // Set to true to see authentication headers (CAUTION: sensitive information)
        bool logAuthHeaders = builder.Environment.IsDevelopment() && 
                             (builder.Configuration["Logging:LogAuthenticationHeaders"]?.ToLower() == "true" ||
                              builder.Configuration["LogAuthenticationHeaders"]?.ToLower() == "true");
        
        return new LoggingHttpMessageHandler(logger, logAuthHeaders);
    });

builder.Services.AddSingleton<ISsrsService, SsrsService>();
builder.Services.AddSingleton<IReportContentService, ReportContentService>();

// Register plugins as singletons
builder.Services.AddSingleton<ReportSearchPlugin>();
builder.Services.AddSingleton<SsrsPlugin>();

// Create the plugin collection (using the KernelPluginFactory to create plugins from objects)
builder.Services.AddSingleton<KernelPluginCollection>((serviceProvider) => 
    [
        KernelPluginFactory.CreateFromObject(serviceProvider.GetRequiredService<ReportSearchPlugin>()),
        KernelPluginFactory.CreateFromObject(serviceProvider.GetRequiredService<SsrsPlugin>())
    ]
);

// Finally, create the Kernel service with the service provider and plugin collection
builder.Services.AddTransient((serviceProvider) =>
{
    KernelPluginCollection pluginCollection = serviceProvider.GetRequiredService<KernelPluginCollection>();

    return new Kernel(serviceProvider, pluginCollection);
});

// Register the ChatService
builder.Services.AddSingleton<ChatService>();

// Add controllers
builder.Services.AddControllers();

// Add health checks
builder.Services.AddHealthChecks();

// Add API Explorer and Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

// Map default endpoints including health checks
app.MapDefaultEndpoints();

// Run the application
app.Run();
