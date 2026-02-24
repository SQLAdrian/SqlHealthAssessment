/* In the name of God, the Merciful, the Compassionate */

// Query plan viewer helpers for Blazor JS interop
window.queryPlanInterop = {
    showPlan: function (containerId, xml) {
        var container = document.getElementById(containerId);
        if (!container) return;
        container.innerHTML = '';
        if (window.QP && xml) {
            try {
                QP.showPlan(container, xml, { jsTooltips: true });
            } catch (e) {
                container.innerHTML = '<p style="color:#f44336;padding:16px;">Failed to render query plan: ' + e.message + '</p>';
            }
        }
    },
    clearPlan: function (containerId) {
        var container = document.getElementById(containerId);
        if (container) container.innerHTML = '';
    }
};
