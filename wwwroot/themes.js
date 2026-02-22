/* In the name of God, the Merciful, the Compassionate */

// Theme Configuration for SqlHealthAssessment
// 10 unique themes with distinct color palettes and visual effects

const themes = {
    // 1. Japanese Cherry Blossom (Sakura)
    sakura: {
        name: "Japanese Cherry Blossom",
        primary: "#e8a4b8",
        secondary: "#f5d0e0",
        accent: "#c45c7e",
        background: "linear-gradient(135deg, #1a0a10 0%, #2d1520 50%, #1a0a10 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0MDAiIGhlaWdodD0iNDAwIj48ZGVmcz48cGF0dGVybiBpZD0iY2xvdWRzIiB3aWR0aD0iNDAiIGhlaWdodD0iNDAiIHBhdHRlcm5Ub3VjaFNpemg9IjQwIiBwYXR0ZXJuVW5pdHM9InVzZXJTcGFjZU9uVXNlIj48cGF0aCBkPSJNMTAgMTBoMjB2MjBIMTB6TTEwIDMwaDIwdjIwSDEweiIvPjwvcGF0dGVybj48L2RlZnM+PHJlY3Qgd2lkdGg9IjEwMCUiIGhlaWdodD0iMTAwJSIgZmlsbD0idXJsKCNjbG91ZHMpIiBvcGFjaXR5PSIwLjEiLz48L3N2Zz4=')",
        cardBg: "rgba(232, 164, 184, 0.08)",
        textPrimary: "#f5e6ea",
        textSecondary: "#c9a0ad",
        border: "rgba(196, 92, 126, 0.3)",
        success: "#90c695",
        warning: "#e8c547",
        error: "#d66a7c",
        info: "#7eb8d4",
        navBg: "rgba(45, 21, 32, 0.95)",
        shadow: "0 4px 20px rgba(196, 92, 126, 0.15)"
    },

    // 2. American Colonial
    colonial: {
        name: "American Colonial",
        primary: "#8b4513",
        secondary: "#d2691e",
        accent: "#cd853f",
        background: "linear-gradient(135deg, #1a1410 0%, #2a2015 50%, #1a1410 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI4MCIgaGVpZ2h0PSI4MCI+PHBhdGggZD0iTTAgMGg4VjhoMHYtOHptIDgwdjhoOHYtOGgtOHptLTggOHY4aC04di04aDh6IiBmaWxsPSIjMmEyMDE1IiBmaWxsLW9wYWNpdHk9IjAuMiIvPjwvc3ZnPg==')",
        cardBg: "rgba(139, 69, 19, 0.1)",
        textPrimary: "#f5e6d3",
        textSecondary: "#a89070",
        border: "rgba(205, 133, 63, 0.3)",
        success: "#6b8e23",
        warning: "#daa520",
        error: "#b22222",
        info: "#5b8db8",
        navBg: "rgba(42, 32, 21, 0.95)",
        shadow: "0 4px 20px rgba(139, 69, 19, 0.2)"
    },

    // 3. African Sunset
    africanSunset: {
        name: "African Sunset",
        primary: "#ff6b35",
        secondary: "#f7c59f",
        accent: "#efa00b",
        background: "linear-gradient(135deg, #1a0f05 0%, #2d1805 50%, #1a0f05 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MCIgaGVpZ2h0PSI2MCI+PGNpcmNsZSBjeD0iMzAiIGN5PSIzMCIgcj0iMTUiIGZpbGw9IiNmNzE2MzUiIGZpbGwtb3BhY2l0eT0iMC4xIi8+PC9zdmc+')",
        cardBg: "rgba(255, 107, 53, 0.08)",
        textPrimary: "#fff5e6",
        textSecondary: "#d4a574",
        border: "rgba(239, 160, 11, 0.3)",
        success: "#4ade80",
        warning: "#fbbf24",
        error: "#ef4444",
        info: "#f7c59f",
        navBg: "rgba(45, 24, 5, 0.95)",
        shadow: "0 4px 20px rgba(255, 107, 53, 0.2)"
    },

    // 4. Nordic Frost
    nordicFrost: {
        name: "Nordic Frost",
        primary: "#00d4ff",
        secondary: "#a8e6ff",
        accent: "#0077b6",
        background: "linear-gradient(135deg, #0a1628 0%, #0d2137 50%, #0a1628 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxMDAiIGhlaWdodD0iMTAwIj48cGF0aCBkPSJNMCAwaDk5Ljk5Tjk5Ljk5IDBoMHoiIGZpbGw9IiMwMDd3YjYiIGZpbGwtb3BhY2l0eT0iMC4xIi8+PC9zdmc+')",
        cardBg: "rgba(0, 212, 255, 0.06)",
        textPrimary: "#e0f7ff",
        textSecondary: "#7ec8e3",
        border: "rgba(0, 119, 182, 0.4)",
        success: "#22d3ee",
        warning: "#fbbf24",
        error: "#f87171",
        info: "#a8e6ff",
        navBg: "rgba(10, 22, 40, 0.95)",
        shadow: "0 4px 20px rgba(0, 212, 255, 0.15)"
    },

    // 5. Brazilian Carnival
    carnival: {
        name: "Brazilian Carnival",
        primary: "#ff1493",
        secondary: "#ffd700",
        accent: "#00ff7f",
        background: "linear-gradient(135deg, #1a0518 0%, #2d0a20 50%, #1a0518 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI4MCIgaGVpZ2h0PSI4MCI+PGNpcmNsZSBjeD0iNDAiIGN5PSI0MCIgcj0iMzUiIGZpbGw9IiNGRjE0OTMiIG9wYWNpdHk9IjAuMiIvPjwvc3ZnPg==')",
        cardBg: "rgba(255, 20, 147, 0.08)",
        textPrimary: "#fff0f5",
        textSecondary: "#d4a5b9",
        border: "rgba(0, 255, 127, 0.3)",
        success: "#00ff7f",
        warning: "#ffd700",
        error: "#ff1493",
        info: "#ff69b4",
        navBg: "rgba(45, 10, 32, 0.95)",
        shadow: "0 4px 20px rgba(255, 20, 147, 0.2)"
    },

    // 6. Hawaiian Paradise
    hawaiian: {
        name: "Hawaiian Paradise",
        primary: "#00bfa5",
        secondary: "#64ffda",
        accent: "#ff6d00",
        background: "linear-gradient(135deg, #0a1f1a 0%, #0d2925 50%, #0a1f1a 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MCIgaGVpZ2h0PSI2MCI+PHBhdGggZD0iTTMwIDBoMzB2MzBIMzB6IiBmaWxsPSIjNjBmZmRhIiBmaWxsLW9wYWNpdHk9IjAuMSIvPjwvc3ZnPg==')",
        cardBg: "rgba(0, 191, 165, 0.08)",
        textPrimary: "#e0f7f5",
        textSecondary: "#80cbc4",
        border: "rgba(255, 109, 0, 0.3)",
        success: "#4ade80",
        warning: "#fbbf24",
        error: "#ff5252",
        info: "#64ffda",
        navBg: "rgba(10, 31, 26, 0.95)",
        shadow: "0 4px 20px rgba(0, 191, 165, 0.2)"
    },

    // 7. Midnight Cyberpunk
    cyberpunk: {
        name: "Midnight Cyberpunk",
        primary: "#ff00ff",
        secondary: "#00ffff",
        accent: "#ffff00",
        background: "linear-gradient(135deg, #0d001a 0%, #1a0033 50%, #0d001a 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0MCIgaGVpZ2h0PSI0MCI+PHJlY3Qgd2lkdGg9IjQwIiBoZWlnaHQ9IjQwIiBmaWxsPSIjZmYwMGZmIiBmaWxsLW9wYWNpdHk9IjAuMSIvPjwvc3ZnPg==')",
        cardBg: "rgba(255, 0, 255, 0.06)",
        textPrimary: "#f0e6ff",
        textSecondary: "#b366ff",
        border: "rgba(0, 255, 255, 0.4)",
        success: "#00ff88",
        warning: "#ffff00",
        error: "#ff0055",
        info: "#00ffff",
        navBg: "rgba(13, 0, 26, 0.95)",
        shadow: "0 4px 30px rgba(255, 0, 255, 0.25)"
    },

    // 8. English Garden
    englishGarden: {
        name: "English Garden",
        primary: "#228b22",
        secondary: "#90ee90",
        accent: "#daa520",
        background: "linear-gradient(135deg, #0a1a0a 0%, #142814 50%, #0a1a0a 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MCIgaGVpZ2h0PSI2MCI+PGNpcmNsZSBjeD0iMzAiIGN5PSIzMCIgcj0iMjAiIGZpbGw9IiMyMjhiMjIiIGZpbGwtb3BhY2l0eT0iMC4xIi8+PC9zdmc+')",
        cardBg: "rgba(34, 139, 34, 0.08)",
        textPrimary: "#e8f5e9",
        textSecondary: "#81c784",
        border: "rgba(218, 165, 32, 0.3)",
        success: "#4caf50",
        warning: "#ffeb3b",
        error: "#f44336",
        info: "#90ee90",
        navBg: "rgba(20, 40, 20, 0.95)",
        shadow: "0 4px 20px rgba(34, 139, 34, 0.2)"
    },

    // 9. Indian Festival
    indianFestival: {
        name: "Indian Festival",
        primary: "#ff4500",
        secondary: "#ffd700",
        accent: "#9932cc",
        background: "linear-gradient(135deg, #1a0a05 0%, #2d1008 50%, #1a0a05 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI2MCIgaGVpZ2h0PSI2MCI+PHBhdGggZD0iTTMwIDBoMzB2MzBIMzB6IiBmaWxsPSIjOTkzMmNjIiBmaWxsLW9wYWNpdHk9IjAuMSIvPjwvc3ZnPg==')",
        cardBg: "rgba(255, 69, 0, 0.08)",
        textPrimary: "#fff8e7",
        textSecondary: "#d4a574",
        border: "rgba(153, 50, 204, 0.3)",
        success: "#4caf50",
        warning: "#ffa000",
        error: "#d32f2f",
        info: "#ffd700",
        navBg: "rgba(45, 16, 8, 0.95)",
        shadow: "0 4px 20px rgba(255, 69, 0, 0.2)"
    },

    // 10. Ocean Deep
    oceanDeep: {
        name: "Ocean Deep",
        primary: "#0077b6",
        secondary: "#48cae4",
        accent: "#90e0ef",
        background: "linear-gradient(135deg, #030a14 0%, #0a1d2e 50%, #030a14 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI4MCIgaGVpZ2h0PSI4MCI+PHBhdGggZD0iTTQwIDBoMzJ2MzJIMTBWMGgyMHoiIGZpbGw9IiMwMDc3YjYiIGZpbGwtb3BhY2l0eT0iMC4xIi8+PC9zdmc+')",
        cardBg: "rgba(0, 119, 182, 0.08)",
        textPrimary: "#e0f4ff",
        textSecondary: "#90b4c8",
        border: "rgba(72, 202, 228, 0.3)",
        success: "#22d3ee",
        warning: "#fbbf24",
        error: "#f87171",
        info: "#48cae4",
        navBg: "rgba(3, 10, 20, 0.95)",
        shadow: "0 4px 20px rgba(0, 119, 182, 0.2)"
    },

<<<<<<< HEAD
    // 11. Middle Eastern (Arabian Nights)
    middleEastern: {
        name: "Arabian Nights",
        primary: "#c9a84c",
        secondary: "#e8d5a3",
        accent: "#8b1a1a",
        background: "linear-gradient(135deg, #0d0a05 0%, #1a1205 50%, #0d0a05 100%)",
        backgroundImage: "url('arabicpattern.svg')",
        backgroundSize: "400px 400px",
        cardBg: "rgba(201, 168, 76, 0.07)",
        textPrimary: "#f5ead8",
        textSecondary: "#c4a96a",
        border: "rgba(201, 168, 76, 0.3)",
        success: "#4caf50",
        warning: "#e8a020",
        error: "#c0392b",
        navBg: "rgba(13, 10, 5, 0.97)",
        shadow: "0 4px 20px rgba(201, 168, 76, 0.2)"
=======
    middleEast: {
        name: "Middle East",
        primary: "#d4af37",
        secondary: "#c19a6b",
        accent: "#8b4513",
        background: "linear-gradient(135deg, #1a1410 0%, #2d2015 50%, #1a1410 100%)",
        backgroundImage: "url('data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIxMjAiIGhlaWdodD0iMTIwIj48ZGVmcz48cGF0dGVybiBpZD0iaXNsYW1pYyIgd2lkdGg9IjYwIiBoZWlnaHQ9IjYwIiBwYXR0ZXJuVW5pdHM9InVzZXJTcGFjZU9uVXNlIj48cGF0aCBkPSJNMzAgMGwxNSAzMC0xNSAzMC0xNS0zMHoiIGZpbGw9Im5vbmUiIHN0cm9rZT0iI2Q0YWYzNyIgc3Ryb2tlLXdpZHRoPSIwLjUiIG9wYWNpdHk9IjAuMiIvPjxjaXJjbGUgY3g9IjMwIiBjeT0iMzAiIHI9IjE1IiBmaWxsPSJub25lIiBzdHJva2U9IiNjMTlhNmIiIHN0cm9rZS13aWR0aD0iMC41IiBvcGFjaXR5PSIwLjE1Ii8+PHBhdGggZD0iTTAgMzBoNjBNMzAgMHY2MCIgc3Ryb2tlPSIjOGI0NTEzIiBzdHJva2Utd2lkdGg9IjAuMyIgb3BhY2l0eT0iMC4xIi8+PC9wYXR0ZXJuPjwvZGVmcz48cmVjdCB3aWR0aD0iMTAwJSIgaGVpZ2h0PSIxMDAlIiBmaWxsPSJ1cmwoI2lzbGFtaWMpIi8+PC9zdmc+')",
        cardBg: "rgba(212, 175, 55, 0.08)",
        textPrimary: "#f5e6d3",
        textSecondary: "#c9a876",
        border: "rgba(212, 175, 55, 0.3)",
        success: "#6b8e23",
        warning: "#daa520",
        error: "#cd5c5c",
        info: "#c19a6b",
        navBg: "rgba(45, 32, 21, 0.95)",
        shadow: "0 4px 20px rgba(212, 175, 55, 0.2)"
>>>>>>> origin/master
    }
};

// Function to apply theme
function applyTheme(themeName) {
    const theme = themes[themeName];
    if (!theme) {
        console.error(`Theme '${themeName}' not found`);
        return;
    }

    const root = document.documentElement;
    root.style.setProperty('--primary', theme.primary);
    root.style.setProperty('--secondary', theme.secondary);
    root.style.setProperty('--accent', theme.accent);
    root.style.setProperty('--bg-primary', theme.background);
    root.style.setProperty('--bg-card', theme.cardBg);
    root.style.setProperty('--text-primary', theme.textPrimary);
    root.style.setProperty('--text-secondary', theme.textSecondary);
    root.style.setProperty('--border', theme.border);
    root.style.setProperty('--green', theme.success);
    root.style.setProperty('--yellow', theme.warning);
    root.style.setProperty('--red', theme.error);
    root.style.setProperty('--blue', theme.info || theme.accent);
    root.style.setProperty('--nav-bg', theme.navBg);
    root.style.setProperty('--shadow', theme.shadow);

    // Apply background
    document.body.style.background = theme.background;
    document.body.style.backgroundImage = theme.backgroundImage;
    document.body.style.backgroundSize = theme.backgroundSize || 'cover';
    document.body.style.backgroundAttachment = 'fixed';

    // Store selected theme
    localStorage.setItem('SqlHealthAssessment-theme', themeName);
}

// Load saved theme on startup
function loadSavedTheme() {
    const savedTheme = localStorage.getItem('SqlHealthAssessment-theme');
    if (savedTheme && themes[savedTheme]) {
        applyTheme(savedTheme);
    } else {
        applyTheme('cyberpunk'); // Default theme
    }
}

// Make available globally
window.themes = themes;
window.applyTheme = applyTheme;
window.loadSavedTheme = loadSavedTheme;
