using SqlHealthAssessment.Data;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Data;

public class DashboardConfigServiceTests
{
    [Fact]
    public void Config_IsNotNull_AfterInitialization()
    {
        var service = new DashboardConfigService();
        
        Assert.NotNull(service.Config);
        Assert.NotNull(service.Config.Dashboards);
    }

    [Fact]
    public void GetQuery_ReturnsCorrectQuery_ForSqlServer()
    {
        var service = new DashboardConfigService();
        var config = service.Config;
        
        if (config.Dashboards.Any() && config.Dashboards[0].Panels.Any())
        {
            var firstPanel = config.Dashboards[0].Panels[0];
            var query = service.GetQuery(firstPanel.Id, "SqlServer");
            
            Assert.NotNull(query);
            Assert.Equal(firstPanel.Query.SqlServer, query);
        }
    }

    [Fact]
    public void GetQuery_ThrowsKeyNotFoundException_ForInvalidQueryId()
    {
        var service = new DashboardConfigService();
        
        Assert.Throws<KeyNotFoundException>(() => 
            service.GetQuery("invalid.query.id", "SqlServer"));
    }

    [Fact]
    public void HasQuery_ReturnsTrue_ForExistingQuery()
    {
        var service = new DashboardConfigService();
        var config = service.Config;
        
        if (config.Dashboards.Any() && config.Dashboards[0].Panels.Any())
        {
            var firstPanel = config.Dashboards[0].Panels[0];
            var exists = service.HasQuery(firstPanel.Id);
            
            Assert.True(exists);
        }
    }

    [Fact]
    public void HasQuery_ReturnsFalse_ForNonExistingQuery()
    {
        var service = new DashboardConfigService();
        
        var exists = service.HasQuery("nonexistent.query");
        
        Assert.False(exists);
    }

    [Fact]
    public void GetPanelType_ReturnsCorrectType_ForExistingPanel()
    {
        var service = new DashboardConfigService();
        var config = service.Config;
        
        if (config.Dashboards.Any() && config.Dashboards[0].Panels.Any())
        {
            var firstPanel = config.Dashboards[0].Panels[0];
            var panelType = service.GetPanelType(firstPanel.Id);
            
            Assert.Equal(firstPanel.PanelType, panelType);
        }
    }

    [Fact]
    public void GetPanelType_ReturnsUnknown_ForNonExistingPanel()
    {
        var service = new DashboardConfigService();
        
        var panelType = service.GetPanelType("nonexistent.panel");
        
        Assert.Equal("Unknown", panelType);
    }

    [Fact]
    public void UpdateConfig_UpdatesConfigAndRaisesEvent()
    {
        var service = new DashboardConfigService();
        bool eventRaised = false;
        service.OnConfigChanged += () => eventRaised = true;
        
        var newConfig = service.Config;
        service.UpdateConfig(newConfig);
        
        Assert.True(eventRaised);
    }

    [Fact]
    public void NotifyChanged_RaisesOnConfigChangedEvent()
    {
        var service = new DashboardConfigService();
        bool eventRaised = false;
        service.OnConfigChanged += () => eventRaised = true;
        
        service.NotifyChanged();
        
        Assert.True(eventRaised);
    }
}
