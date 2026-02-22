using SqlHealthAssessment.Data;
using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Data;

public class ConnectionManagerTests
{
    [Fact]
    public void GetConnections_ReturnsEmptyList_WhenNoConnectionsExist()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        
        // Act
        var connections = manager.GetConnections();
        
        // Assert
        Assert.NotNull(connections);
        Assert.Empty(connections);
    }

    [Fact]
    public void AddConnection_IncreasesConnectionCount()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        var connection = new ServerConnection
        {
            ServerNames = "localhost",
            Database = "master"
        };
        
        // Act
        manager.AddConnection(connection);
        var connections = manager.GetConnections();
        
        // Assert
        Assert.Single(connections);
        Assert.Equal("localhost", connections[0].ServerNames);
    }

    [Fact]
    public void GetEnabledConnections_ReturnsOnlyEnabledConnections()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        manager.AddConnection(new ServerConnection { ServerNames = "server1", IsEnabled = true });
        manager.AddConnection(new ServerConnection { ServerNames = "server2", IsEnabled = false });
        manager.AddConnection(new ServerConnection { ServerNames = "server3", IsEnabled = true });
        
        // Act
        var enabled = manager.GetEnabledConnections();
        
        // Assert
        Assert.Equal(2, enabled.Count);
        Assert.All(enabled, c => Assert.True(c.IsEnabled));
    }

    [Fact]
    public void UpdateConnection_ModifiesExistingConnection()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        var connection = new ServerConnection { ServerNames = "localhost" };
        manager.AddConnection(connection);
        
        // Act
        connection.ServerNames = "newserver";
        manager.UpdateConnection(connection);
        var updated = manager.GetConnection(connection.Id);
        
        // Assert
        Assert.NotNull(updated);
        Assert.Equal("newserver", updated.ServerNames);
    }

    [Fact]
    public void RemoveConnection_DeletesConnection()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        var connection = new ServerConnection { ServerNames = "localhost" };
        manager.AddConnection(connection);
        
        // Act
        manager.RemoveConnection(connection.Id);
        var result = manager.GetConnection(connection.Id);
        
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SetCurrentServer_RaisesOnConnectionChangedEvent()
    {
        // Arrange
        var manager = new ServerConnectionManager();
        var connection = new ServerConnection { ServerNames = "localhost" };
        manager.AddConnection(connection);
        bool eventRaised = false;
        manager.OnConnectionChanged += () => eventRaised = true;
        
        // Act
        manager.SetCurrentServer(connection.Id);
        
        // Assert
        Assert.True(eventRaised);
        Assert.NotNull(manager.CurrentServer);
        Assert.Equal(connection.Id, manager.CurrentServer.Id);
    }
}
