using System.Collections.Generic;
using System.Linq;

namespace SQLTriage.Data.Models;

public class PanelTrace
{
    public string DashboardId { get; set; } = "";
    public string PanelId { get; set; } = "";
    public string QueryId { get; set; } = "";
    public string ServerName { get; set; } = "";
    public DateTime EnqueuedAt { get; set; }
    public DateTime DequeuedAt { get; set; }
    public DateTime SqlStarted { get; set; }
    public DateTime SqlCompleted { get; set; }
    public string CacheHitTier { get; set; } = ""; // None, Hot, SQLite, Fresh
    public int RowCount { get; set; }
    public long BytesReturned { get; set; }
    public DateTime DataReadyAt { get; set; }
    public DateTime RenderedAt { get; set; }

    // Computed properties
    public TimeSpan QueueWait => DequeuedAt - EnqueuedAt;
    public TimeSpan SqlDuration => SqlCompleted - SqlStarted;
    public TimeSpan CacheWrite => DataReadyAt - SqlCompleted;
    public TimeSpan RenderDuration => RenderedAt - DataReadyAt;
    public TimeSpan TotalDuration => RenderedAt - EnqueuedAt;
}