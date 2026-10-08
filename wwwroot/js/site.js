// Enforce uppercase on inputs
document.addEventListener('DOMContentLoaded', function() {
    document.addEventListener('input', function(e) {
        // Only apply to text-based inputs that support selection
        var textTypes = ['text', 'search', 'url', 'tel', 'email'];
        var isTextInput = e.target.tagName === 'INPUT' && textTypes.includes(e.target.type);
        var isTextArea = e.target.tagName === 'TEXTAREA';
        
        if (isTextInput || isTextArea) {
            var start = e.target.selectionStart;
            var end = e.target.selectionEnd;
            e.target.value = e.target.value.toUpperCase();
            e.target.setSelectionRange(start, end);
        }
    });
});
