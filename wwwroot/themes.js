/* In the name of God, the Merciful, the Compassionate */

// Fixed VS Code-inspired dark theme for SqlHealthAssessment
// Themes have been disabled to ensure consistent readability

// Apply fixed dark theme on startup
function loadSavedTheme() {
    // Fixed VS Code-inspired theme - no user selection
    const root = document.documentElement;
    root.style.setProperty('--bg-primary', '#1e1e1e');
    root.style.setProperty('--bg-secondary', '#252526');
    root.style.setProperty('--bg-panel', '#2d2d30');
    root.style.setProperty('--bg-hover', '#37373d');
    root.style.setProperty('--text-primary', '#cccccc');
    root.style.setProperty('--text-secondary', '#9d9d9d');
    root.style.setProperty('--text-muted', '#6a6a6a');
    root.style.setProperty('--border', '#3e3e42');
    root.style.setProperty('--accent', '#007acc');
    root.style.setProperty('--green', '#4ec9b0');
    root.style.setProperty('--orange', '#ce9178');
    root.style.setProperty('--red', '#f44747');
    root.style.setProperty('--purple', '#c586c0');
    root.style.setProperty('--yellow', '#dcdcaa');
    
    // Set body background
    document.body.style.background = '#1e1e1e';
    document.body.style.backgroundImage = 'none';
}

// Disabled theme functions (kept for compatibility)
function applyTheme(themeName) {
    // Themes disabled - always use fixed dark theme
    loadSavedTheme();
}

// Make available globally
window.loadSavedTheme = loadSavedTheme;
window.applyTheme = applyTheme;