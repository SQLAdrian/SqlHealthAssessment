using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Models;

public class DashboardConfigTests
{
    [Fact]
    public void DashboardDefinition_InitializesWithEmptyPanels()
    {
        var dashboard = new DashboardDefinition
        {
            Id = "test-dashboard",
            Title = "Test Dashboard"
        };
        
        Assert.Equal("test-dashboard", dashboard.Id);
        Assert.Equal("Test Dashboard", dashboard.Title);
        Assert.NotNull(dashboard.Panels);
    }

    [Fact]
    public void PanelDefinition_SetsPropertiesCorrectly()
    {
        var panel = new PanelDefinition
        {
            Id = "test.panel",
            Title = "Test Panel",
            PanelType = "StatCard",
            Height = 200
        };
        
        Assert.Equal("test.panel", panel.Id);
        Assert.Equal("Test Panel", panel.Title);
        Assert.Equal("StatCard", panel.PanelType);
        Assert.Equal(200, panel.Height);
    }

    [Fact]
    public void QueryPair_StoresMultipleQueryTypes()
    {
        var query = new QueryPair
        {
            SqlServer = "SELECT * FROM sys.dm_os_performance_counters",
            LiveQueries = "SELECT * FROM cache_table"
        };
        
        Assert.NotNull(query.SqlServer);
        Assert.NotNull(query.LiveQueries);
        Assert.Contains("sys.dm_os_performance_counters", query.SqlServer);
        Assert.Contains("cache_table", query.LiveQueries);
    }

    [Fact]
    public void DashboardConfigRoot_InitializesWithEmptyCollections()
    {
        var config = new DashboardConfigRoot();
        
        Assert.NotNull(config.Dashboards);
        Assert.NotNull(config.SupportQueries);
    }
}
