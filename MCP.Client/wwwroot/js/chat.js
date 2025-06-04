// Scroll to element
function scrollToElement(element) {
    if (element instanceof HTMLElement) {
        element.scrollIntoView({ behavior: 'smooth', block: 'end' });
    }
}

// Focus on an element
function focusElement(element) {
    if (element instanceof HTMLElement) {
        element.focus();
    }
}
