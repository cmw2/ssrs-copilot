// Utility functions for handling reports in iframes
window.reportUtils = {    // Display a report in an iframe, optimized for PDF reports
    displayReport: function (containerId, reportUrl) {
        try {
            console.log(`displayReport called for container: ${containerId}, URL: ${reportUrl}`);
            
            const container = document.getElementById(containerId);
            if (!container) {
                console.error('Container not found:', containerId);
                return false;
            }
            
            console.log('Container found, dimensions:', container.offsetWidth, 'x', container.offsetHeight);
            
            // Clear container first - simple and reliable method
            container.innerHTML = '';
            
            // Create a unique timestamp for cache-busting
            const timestamp = new Date().getTime();
            
            // Use our proxy endpoint to ensure proper display
            // Generate a unique timestamp for the proxy URL (not the SSRS URL)
            const proxyUrl = `/api/ReportProxy?url=${encodeURIComponent(reportUrl)}&_proxyts=${timestamp}`;
            
            // Create an iframe for the proxied PDF with embedded viewer parameters
            // Add parameters to ensure proper scaling of PDF content
            const enhancedUrl = `${proxyUrl}#view=Fit&zoom=page-fit`;
              
            let iframe = document.createElement('iframe');
            iframe.setAttribute('src', enhancedUrl);
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
            iframe.onload = function() {
                console.log('PDF iframe loaded');
                // Force a resize
                setTimeout(() => {
                    iframe.style.height = '100%';
                    console.log('PDF iframe resized');
                }, 100);
            };
            
            // Add error handling for the iframe
            iframe.onerror = function(error) {
                console.error('Error loading iframe:', error);
                container.innerHTML = '<div class="alert alert-danger">Error loading report. Please try again.</div>';
            };
            
            console.log('About to append iframe to container');
            container.appendChild(iframe);
            console.log('iframe appended to container');
            
            return true;
        } catch (error) {
            console.error('Error in displayReport:', error);
            return false;
        }
    },
    
    // Initialize scrolling in chat messages
    scrollToBottom: function (elementId) {
        const element = document.getElementById(elementId);
        if (element) {
            element.scrollTop = element.scrollHeight;
        }
    },    // Helper function to resize iframes to fit content
    resizeIframe: function(iframeId) {
        const iframe = document.getElementById(iframeId);
        if (!iframe) return;
        
        try {
            // Try to resize iframe based on content
            if (iframe.contentWindow.document.body) {
                // Get the height of the iframe content
                const height = iframe.contentWindow.document.body.scrollHeight;
                // Set the iframe height
                iframe.style.height = (height + 50) + 'px'; // Add padding
                console.log('Resized iframe to', height + 50);
            }
        } catch (e) {
            // Cross-origin issues might prevent access to iframe content
            console.error('Could not resize iframe:', e);
        }
    }
};
