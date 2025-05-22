using System.Diagnostics;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;

namespace SSRSCopilot.Agent.Services;

/// <summary>
/// A delegating handler that logs request and response details including authentication headers
/// </summary>
public class LoggingHttpMessageHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly bool _logAuthenticationHeaders;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingHttpMessageHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger</param>
    /// <param name="logAuthenticationHeaders">Whether to log authentication headers (caution: sensitive information)</param>
    public LoggingHttpMessageHandler(ILogger logger, bool logAuthenticationHeaders = false)
    {
        _logger = logger;
        _logAuthenticationHeaders = logAuthenticationHeaders;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var requestId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        // Log request
        await LogRequest(request, requestId);

        // Call the inner handler
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Request failed with exception", requestId);
            throw;
        }

        // Log response
        stopwatch.Stop();
        await LogResponse(response, requestId, stopwatch.ElapsedMilliseconds);

        return response;
    }

    private async Task LogRequest(HttpRequestMessage request, string requestId)
    {
        _logger.LogInformation("[{RequestId}] HTTP Request: {Method} {Uri}", 
            requestId, request.Method, request.RequestUri);

        // Log headers (filtering sensitive ones unless explicitly enabled)
        foreach (var header in request.Headers)
        {
            // Skip authentication headers unless explicitly enabled
            if (!_logAuthenticationHeaders && 
                (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || 
                 header.Key.StartsWith("WWW-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                 header.Key.StartsWith("NTLM", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("[{RequestId}] Header: {HeaderKey}: [REDACTED]", 
                    requestId, header.Key);
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Header: {HeaderKey}: {HeaderValue}", 
                    requestId, header.Key, string.Join(", ", header.Value));
            }
        }            // Special logging for NTLM auth
        if (request.Headers.Authorization?.Scheme == "NTLM")
        {
            _logger.LogInformation("[{RequestId}] NTLM Authentication detected", requestId);
            if (_logAuthenticationHeaders)
            {
                _logger.LogInformation("[{RequestId}] NTLM Token: {Token}", 
                    requestId, request.Headers.Authorization.Parameter);
            }
        }
        else if (request.Headers.Authorization?.Scheme == "Negotiate")
        {
            _logger.LogInformation("[{RequestId}] Negotiate Authentication detected", requestId);
            if (_logAuthenticationHeaders)
            {
                _logger.LogInformation("[{RequestId}] Negotiate Token: {Token}", 
                    requestId, request.Headers.Authorization.Parameter);
            }
        }
        else if (request.Headers.Authorization != null)
        {
            _logger.LogInformation("[{RequestId}] Authentication detected: {Scheme}", 
                requestId, request.Headers.Authorization.Scheme);
        }
        else
        {
            _logger.LogWarning("[{RequestId}] No authentication header in request", requestId);
        }

        // Log content if present
        if (request.Content != null)
        {
            string? contentType = request.Content.Headers.ContentType?.ToString();
            _logger.LogInformation("[{RequestId}] Content-Type: {ContentType}", requestId, contentType);

            // Log content based on type
            if (contentType?.Contains("application/json") == true || 
                contentType?.Contains("text/") == true)
            {
                var content = await request.Content.ReadAsStringAsync();
                if (!string.IsNullOrEmpty(content))
                {
                    _logger.LogInformation("[{RequestId}] Content: {Content}", requestId, content);
                }
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Content: [Binary data not logged]", requestId);
            }
        }
    }

    private async Task LogResponse(HttpResponseMessage response, string requestId, long elapsedMs)
    {
        _logger.LogInformation("[{RequestId}] HTTP Response: {StatusCode} - {ReasonPhrase} (took {ElapsedMs}ms)", 
            requestId, (int)response.StatusCode, response.ReasonPhrase, elapsedMs);

        // Log headers (filtering sensitive ones unless explicitly enabled)
        foreach (var header in response.Headers)
        {
            if (!_logAuthenticationHeaders && 
                (header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) || 
                 header.Key.StartsWith("WWW-Authenticate", StringComparison.OrdinalIgnoreCase) ||
                 header.Key.StartsWith("NTLM", StringComparison.OrdinalIgnoreCase)))
            {
                _logger.LogInformation("[{RequestId}] Response Header: {HeaderKey}: [REDACTED]", 
                    requestId, header.Key);
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Response Header: {HeaderKey}: {HeaderValue}", 
                    requestId, header.Key, string.Join(", ", header.Value));
            }
        }

        // Special logging for NTLM authentication challenges
        var wwwAuthenticateHeader = response.Headers.WwwAuthenticate;
        if (wwwAuthenticateHeader.Any())
        {
            _logger.LogInformation("[{RequestId}] Authentication challenge detected", requestId);
            foreach (var authHeader in wwwAuthenticateHeader)
            {
                if (authHeader.Scheme.Equals("NTLM", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("[{RequestId}] NTLM Authentication challenge", requestId);
                    if (_logAuthenticationHeaders && !string.IsNullOrEmpty(authHeader.Parameter))
                    {
                        _logger.LogInformation("[{RequestId}] NTLM Challenge Parameter: {Parameter}", 
                            requestId, authHeader.Parameter);
                    }
                }
                else
                {
                    _logger.LogInformation("[{RequestId}] Authentication challenge: {Scheme}", 
                        requestId, authHeader.Scheme);
                }
            }
        }

        // Log content if present
        if (response.Content != null)
        {
            string? contentType = response.Content.Headers.ContentType?.ToString();
            _logger.LogInformation("[{RequestId}] Response Content-Type: {ContentType}", 
                requestId, contentType);

            // Log content based on type
            if (contentType?.Contains("application/json") == true || 
                contentType?.Contains("text/") == true)
            {
                try
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrEmpty(content))
                    {
                        // Truncate very large responses
                        if (content.Length > 1000)
                        {
                            _logger.LogInformation("[{RequestId}] Response Content (truncated): {Content}", 
                                requestId, content.Substring(0, 1000) + "...");
                        }
                        else
                        {
                            _logger.LogInformation("[{RequestId}] Response Content: {Content}", 
                                requestId, content);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{RequestId}] Failed to read response content", requestId);
                }
            }
            else
            {
                _logger.LogInformation("[{RequestId}] Response Content: [Binary data not logged]", requestId);
            }
        }
    }
}