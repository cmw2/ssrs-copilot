using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using SSRSCopilot.Web;
using SSRSCopilot.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Add controllers for API endpoints
builder.Services.AddControllers();

// Add error logging service
builder.Services.AddScoped<SSRSCopilot.Web.Services.ErrorLogger>();

builder.Services.AddOutputCache();

// Configure Kestrel server options for increased timeout
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient<ChatApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        client.BaseAddress = new("https+http://agentservice");

        // Set a longer timeout directly on the client
        client.Timeout = TimeSpan.FromMinutes(2);
    });

// Add HTTP client for Agent service API
builder.Services.AddHttpClient("AgentService", client =>
{
    client.BaseAddress = new("https+http://agentservice");
    client.Timeout = TimeSpan.FromMinutes(2);
});

// Add HTTP client for report proxy
builder.Services.AddHttpClient("ReportProxy", client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    app.UseHttpsRedirection();
}


app.UseStaticFiles();
app.UseAntiforgery();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map controller endpoints
app.MapControllers();

app.MapDefaultEndpoints();

app.Run();
