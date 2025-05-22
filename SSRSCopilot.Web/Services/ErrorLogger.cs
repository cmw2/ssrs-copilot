using Microsoft.JSInterop;

namespace SSRSCopilot.Web.Services;

public class ErrorLogger
{
    private readonly ILogger<ErrorLogger> _logger;
    private readonly IJSRuntime _jsRuntime;

    public ErrorLogger(ILogger<ErrorLogger> logger, IJSRuntime jsRuntime)
    {
        _logger = logger;
        _jsRuntime = jsRuntime;
    }

    public void LogError(Exception ex, string context)
    {
        _logger.LogError(ex, "Error in {Context}: {Message}", context, ex.Message);
    }

    public async Task LogErrorToConsole(string message, string detail = "")
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("console.error", $"Error: {message}", detail);
        }
        catch
        {
            // Ignore errors in error logging
        }
    }

    public async Task<bool> TryExecuteJsAsync(Func<Task> action, string context)
    {
        try
        {
            await action();
            return true;
        }
        catch (Exception ex)
        {
            LogError(ex, context);
            await LogErrorToConsole($"Error in {context}: {ex.Message}");
            return false;
        }
    }
}
