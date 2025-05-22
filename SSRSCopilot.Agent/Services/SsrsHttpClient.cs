using System.Net;
using System.Net.Http.Headers;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// Typed HttpClient for SSRS API communication
/// </summary>
public class SsrsHttpClient
{
    public HttpClient Client { get; }
    private readonly ILogger<SsrsHttpClient> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SsrsHttpClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HttpClient provided by DI</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="logger">The logger</param>
    public SsrsHttpClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<SsrsHttpClient> logger)
    {
        _logger = logger;
        
        // Get base URL from configuration
        var apiBaseUrl = configuration["Ssrs:ApiBaseUrl"] 
            ?? throw new InvalidOperationException("Ssrs:ApiBaseUrl configuration is required");
        
        // Configure the base address
        httpClient.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
        
        // Configure timeout
        httpClient.Timeout = TimeSpan.FromMinutes(2);
        
        // Configure content type headers
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        
        // Verify NTLM authentication is set up
        var handlerField = httpClient.GetType().GetField("_handler", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (handlerField != null)
        {
            var handler = handlerField.GetValue(httpClient);
            if (handler != null)
            {
                _logger.LogDebug("HTTP handler type: {HandlerType}", handler.GetType().FullName);
                
                // Force PreAuthenticate if not already set
                var handlerType = handler.GetType();
                var preAuthProperty = handlerType.GetProperty("PreAuthenticate");
                if (preAuthProperty != null)
                {
                    preAuthProperty.SetValue(handler, true);
                    _logger.LogDebug("Forced PreAuthenticate to true");
                }
                
                // Log credentials configuration
                var credentialsProperty = handlerType.GetProperty("Credentials");
                if (credentialsProperty != null)
                {
                    var credentials = credentialsProperty.GetValue(handler);
                    if (credentials != null)
                    {
                        _logger.LogDebug("Credentials type: {CredentialsType}", credentials.GetType().FullName);
                        
                        if (credentials is NetworkCredential networkCredential)
                        {
                            _logger.LogDebug("NetworkCredential configured with username: {Username}, domain: {Domain}",
                                networkCredential.UserName, 
                                string.IsNullOrEmpty(networkCredential.Domain) ? "(not set)" : networkCredential.Domain);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No credentials configured for HTTP handler");
                    }
                }
            }
        }
        
        Client = httpClient;
        
        logger.LogInformation("SsrsHttpClient initialized with base URL: {BaseUrl}", httpClient.BaseAddress);
    }
}
