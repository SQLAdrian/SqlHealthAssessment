using System.Collections.Generic;
using System.Linq;
using SQLTriage.Data.Models;

namespace SQLTriage.Data.Services;

public class PerformanceInspectorService
{
    private readonly List<PanelTrace> _traces = new();
    private readonly int _maxTraces = 500;
    private bool _enabled;

    public void SetEnabled(bool enabled) => _enabled = enabled;

    public void AddTrace(PanelTrace trace)
    {
        if (!_enabled) return;

        lock (_traces)
        {
            _traces.Add(trace);
            if (_traces.Count > _maxTraces)
            {
                _traces.RemoveAt(0);
            }
        }
    }

    public IReadOnlyList<PanelTrace> GetRecentTraces() => _traces.AsReadOnly();

    public PanelTrace[] GetLastLoadTraces(string dashboardId)
    {
        lock (_traces)
        {
            return _traces.Where(t => t.DashboardId == dashboardId)
                         .OrderByDescending(t => t.EnqueuedAt)
                         .TakeWhile((t, i) => i == 0 || t.EnqueuedAt >= _traces.First().EnqueuedAt.AddSeconds(-5)) // Same load within 5s
                         .ToArray();
        }
    }
}