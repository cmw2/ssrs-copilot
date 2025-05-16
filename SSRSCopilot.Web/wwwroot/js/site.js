// JavaScript functions for SSRS Copilot

// Function to scroll an element to the bottom
window.scrollToBottom = function (elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.scrollTop = element.scrollHeight;
    }
};
