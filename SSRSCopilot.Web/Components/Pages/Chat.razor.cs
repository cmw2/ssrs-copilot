using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SSRSCopilot.Web.Models;
using SSRSCopilot.Web.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SSRSCopilot.Web.Components.Pages
{    public partial class Chat
    {
        [Inject]
        public required ChatApiClient ChatClient { get; set; }

        [Inject]
        public required IJSRuntime JSRuntime { get; set; }

        [Inject]
        public required ErrorLogger ErrorLogger { get; set; }

        private List<ChatMessage> messages = new List<ChatMessage>
        {
            new ChatMessage { IsUser = false, Content = "Hello! I can help you run SSRS reports. What kind of report are you looking for today?" }
        };
        
        private string currentMessage = string.Empty;
        private string currentReportUrl = string.Empty;
        private string? sessionId = null;
        private bool isLoading = false;
        private bool _jsModuleLoaded = false;
        private ElementReference messageInputRef;
        
        private async Task ReloadPage()
        {
            await JSRuntime.InvokeVoidAsync("location.reload");
        }
        
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                // Generate a unique session ID for this chat session
                sessionId = Guid.NewGuid().ToString();
                // Initialize the chat
                await ScrollToBottom();
                await EnsureJsModuleLoaded();
            }
        }
        
        private async Task SendMessage()
        {
            if (string.IsNullOrWhiteSpace(currentMessage) || isLoading)
                return;
                
            var userMessage = currentMessage;
            
            // Add user message to the chat
            messages.Add(new ChatMessage { IsUser = true, Content = userMessage });
            
            // Clear the input field
            currentMessage = string.Empty;
            
            // Show loading indicator
            isLoading = true;
            
            // Force UI update
            StateHasChanged();
            
            // Scroll to bottom
            await ScrollToBottom();
            
            try
            {
                // Send message to API
                var response = await ChatClient.SendMessageAsync(userMessage, sessionId);
                
                // Update session ID if it was generated by the server
                if (!string.IsNullOrEmpty(response.SessionId))
                {
                    sessionId = response.SessionId;
                }
                  // Add system response to the chat
                messages.Add(new ChatMessage { IsUser = false, Content = response.Message });
                
                // Log whether we have a report URL
                if (string.IsNullOrEmpty(response.ReportUrl))
                {
                    await ErrorLogger.LogInfoToConsole("No report URL found in response");
                }
                else
                {
                    await ErrorLogger.LogInfoToConsole($"Report URL found: {response.ReportUrl}");
                }
                
                // Update report URL if provided
                if (!string.IsNullOrEmpty(response.ReportUrl))
                {
                    bool shouldUpdateUrl = response.ReportUrl != currentReportUrl;
                    
                    // Log whether the URL is different from the current one
                    if (shouldUpdateUrl)
                    {
                        await ErrorLogger.LogInfoToConsole($"Report URL changed from '{currentReportUrl}' to '{response.ReportUrl}'");
                    }
                    else
                    {
                        await ErrorLogger.LogInfoToConsole($"Report URL is the same as current URL: '{response.ReportUrl}'");
                    }
                    
                    // Always update the current report URL
                    currentReportUrl = response.ReportUrl;
                    
                    // If the URL is different, we need to force a UI update first
                    if (shouldUpdateUrl)
                    {
                        // Force UI update before displaying report
                        StateHasChanged();
                        await Task.Delay(100); // Give the DOM time to update
                    }
                    
                    // Always display the report
                    await DisplayReport(response.ReportUrl);
                }
            }
            catch (Exception ex)
            {
                // Add error message to chat
                messages.Add(new ChatMessage 
                { 
                    IsUser = false, 
                    Content = $"I'm sorry, but I encountered an error: {ex.Message}. Please try again." 
                });
            }
            finally
            {
                // Hide loading indicator
                isLoading = false;
                // Force UI update
                StateHasChanged();
                
                // Scroll to bottom
                await ScrollToBottom();
                
                // Focus the input field
                await FocusInput();
            }
        }
        
        private async Task EnsureJsModuleLoaded()
        {
            if (!_jsModuleLoaded)
            {
                try
                {
                    await ErrorLogger.LogInfoToConsole("Attempting to load reportUtils.js module");
                    await JSRuntime.InvokeVoidAsync("import", "./js/reportUtils.js");
                    _jsModuleLoaded = true;
                    await ErrorLogger.LogInfoToConsole("reportUtils.js module loaded successfully");
                    
                    // Verify that the module is actually loaded
                    var moduleExists = await JSRuntime.InvokeAsync<bool>("eval", "(typeof window.reportUtils !== 'undefined')");
                    if (!moduleExists)
                    {
                        await ErrorLogger.LogErrorToConsole("WARNING: reportUtils global object not found after loading module");
                        _jsModuleLoaded = false;
                    }
                }
                catch (Exception ex)
                {
                    // If module loading fails, log the error and continue
                    ErrorLogger.LogError(ex, "Loading reportUtils.js module");
                    await ErrorLogger.LogErrorToConsole($"Error loading JS module: {ex.Message}");
                    _jsModuleLoaded = false;
                }
            }
        }
        
        private async Task DisplayReport(string reportUrl)
        {
            try
            {                // Log that we're attempting to display a report
                await ErrorLogger.LogInfoToConsole($"Attempting to display report: {reportUrl}");
                
                // Make sure JS module is loaded
                await EnsureJsModuleLoaded();
                
                // First clear the container - do this regardless of JS module loading status
                await JSRuntime.InvokeVoidAsync("eval", "const container = document.getElementById('report-iframe-container'); if(container) { container.innerHTML = ''; }" );
                
                // Force a UI update
                StateHasChanged();
                
                if (_jsModuleLoaded)
                {
                    // Use the reportUtils.js module
                    await ErrorLogger.TryExecuteJsAsync(
                        async () => {
                            // Log before calling displayReport
                            await ErrorLogger.LogInfoToConsole($"Calling reportUtils.displayReport with URL: {reportUrl}");
                            
                            // Call the displayReport function
                            await JSRuntime.InvokeVoidAsync("reportUtils.displayReport", "report-iframe-container", reportUrl);
                            
                            // Log after calling displayReport
                            await ErrorLogger.LogInfoToConsole("Called reportUtils.displayReport successfully");
                        },
                        "Displaying report with reportUtils"
                    );
                }
                else
                {                    // Fallback if JS module loading failed - use direct DOM manipulation
                    await ErrorLogger.TryExecuteJsAsync(
                        async () => await JSRuntime.InvokeVoidAsync("eval", $@"
                            const container = document.getElementById('report-iframe-container');
                            if (container) {{
                                // Safely clear the container
                                container.innerHTML = '';
                                
                                // Add a cache-busting parameter to the proxy URL, not the report URL
                                const timestamp = new Date().getTime();
                                const proxyUrl = `/api/ReportProxy?url=${{encodeURIComponent('{reportUrl}')}}&_proxyts=${{timestamp}}`;
                                
                                // Add parameters to ensure proper scaling of PDF content
                                const url = proxyUrl + '#view=Fit&zoom=page-fit';
                                
                                const iframe = document.createElement('iframe');
                                iframe.setAttribute('src', url);
                                iframe.setAttribute('class', 'report-iframe');
                                iframe.setAttribute('allowfullscreen', 'true');
                                iframe.setAttribute('scrolling', 'auto');
                                
                                // Set inline styles for better iframe sizing
                                iframe.style.width = '100%';
                                iframe.style.height = '100%';
                                iframe.style.position = 'absolute';
                                iframe.style.top = '0';
                                iframe.style.left = '0';
                                iframe.style.right = '0';
                                iframe.style.bottom = '0';
                                iframe.style.border = 'none';
                                
                                // Add event listener to adjust iframe size when content loads
                                iframe.onload = function() {{
                                    console.log('PDF iframe loaded via fallback');
                                    // Force a resize
                                    setTimeout(() => {{
                                        iframe.style.height = '100%';
                                    }}, 100);
                                }};
                                
                                // Log before appending the iframe
                                console.log('About to append iframe to container');
                                
                                container.appendChild(iframe);
                                
                                // Log after appending the iframe
                                console.log('Iframe appended to container');
                            }} else {{
                                console.error('Report container not found!');
                            }}
                        "),
                        "Displaying report with fallback"
                    );
                }
            }
            catch (Exception ex)
            {
                // Handle any errors that occur during report display
                ErrorLogger.LogError(ex, "DisplayReport");
                
                // Add error message to chat
                messages.Add(new ChatMessage 
                { 
                    IsUser = false, 
                    Content = $"I'm sorry, but I encountered an error displaying the report. Please try again." 
                });
                StateHasChanged();
            }
        }
        
        private string FormatMessage(string content)
        {
            // Convert line breaks to HTML breaks
            return content.Replace("\n", "<br>");
        }
        
        private async Task ScrollToBottom()
        {
            try
            {
                // JavaScript interop to scroll the chat to the bottom
                // First try to use the reportUtils module if loaded
                if (_jsModuleLoaded)
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("reportUtils.scrollToBottom", "chat-messages");
                        return;
                    }
                    catch (Exception ex)
                    {
                        // Fall back to using the global function if module method fails
                        ErrorLogger.LogError(ex, "ScrollToBottom with reportUtils");
                    }
                }
                
                // Fall back to the global scrollToBottom function
                await JSRuntime.InvokeVoidAsync("scrollToBottom", "chat-messages");
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application for scrolling issues
                ErrorLogger.LogError(ex, "ScrollToBottom fallback");
                await ErrorLogger.LogErrorToConsole("Failed to scroll chat to bottom");
            }
        }
        
        private async Task FocusInput()
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("eval", $"document.getElementById('{messageInputRef.Id}')?.focus()");
            }
            catch (Exception ex)
            {
                // Log the error but don't crash the application
                ErrorLogger.LogError(ex, "FocusInput");
                await ErrorLogger.LogErrorToConsole("Failed to set focus to input field");
            }
        }
    }
}
