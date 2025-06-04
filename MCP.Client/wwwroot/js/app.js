// Chat message interactions and formatting
window.scrollToElement = function (element) {
    if (element) {
        element.scrollIntoView({ behavior: "smooth", block: "end" });
    }
};

window.focusElement = function (element) {
    if (element) {
        element.focus();
    }
};

// Function to format JSON for display
window.formatJson = function (jsonString) {
    try {
        // Parse and re-stringify to ensure proper format
        const obj = JSON.parse(jsonString);
        return JSON.stringify(obj, null, 2);
    } catch (e) {
        // If it's not valid JSON, return as is
        return jsonString;
    }
};

// Initialize all JSON blocks on the page
window.initJsonFormatting = function() {
    document.querySelectorAll('.json-output').forEach(function(block) {
        try {
            const text = block.textContent || block.innerText;
            const formatted = window.formatJson(text);
            block.textContent = formatted;
        } catch (e) {
            // Keep original if formatting fails
        }
    });
};

// Function to be called after the chat is updated
window.updateChatDisplay = function() {
    window.initJsonFormatting();
};
