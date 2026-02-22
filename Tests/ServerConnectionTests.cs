using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Models;

public class ServerConnectionTests
{
    [Fact]
    public void GetServerList_ParsesMultipleServers_WithNewlines()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "server1\nserver2\nserver3"
        };
        
        // Act
        var servers = connection.GetServerList();
        
        // Assert
        Assert.Equal(3, servers.Count);
        Assert.Contains("server1", servers);
        Assert.Contains("server2", servers);
        Assert.Contains("server3", servers);
    }

    [Fact]
    public void GetServerList_ParsesMultipleServers_WithCommas()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "server1,server2,server3"
        };
        
        // Act
        var servers = connection.GetServerList();
        
        // Assert
        Assert.Equal(3, servers.Count);
    }

    [Fact]
    public void GetServerList_TrimsWhitespace()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "  server1  ,  server2  "
        };
        
        // Act
        var servers = connection.GetServerList();
        
        // Assert
        Assert.Equal(2, servers.Count);
        Assert.Equal("server1", servers[0]);
        Assert.Equal("server2", servers[1]);
    }

    [Fact]
    public void GetServerCount_ReturnsCorrectCount()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "server1\nserver2\nserver3"
        };
        
        // Act
        var count = connection.GetServerCount();
        
        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void GetConnectionString_UsesWindowsAuth_WhenConfigured()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "localhost",
            Database = "master",
            UseWindowsAuthentication = true
        };
        
        // Act
        var connStr = connection.GetConnectionString("localhost");
        
        // Assert
        Assert.Contains("Integrated Security=True", connStr);
        Assert.Contains("Data Source=localhost", connStr);
        Assert.Contains("Initial Catalog=master", connStr);
    }

    [Fact]
    public void GetConnectionString_UsesSqlAuth_WhenConfigured()
    {
        // Arrange
        var connection = new ServerConnection
        {
            ServerNames = "localhost",
            Database = "master",
            AuthenticationType = "SqlServer",
            Username = "sa"
        };
        connection.SetPassword("testpass");
        
        // Act
        var connStr = connection.GetConnectionString("localhost");
        
        // Assert
        Assert.Contains("User ID=sa", connStr);
        Assert.Contains("Integrated Security=False", connStr);
    }

    [Fact]
    public void SetPassword_EncryptsPassword()
    {
        // Arrange
        var connection = new ServerConnection();
        
        // Act
        connection.SetPassword("mypassword");
        
        // Assert
        Assert.NotNull(connection.Password);
        Assert.NotEqual("mypassword", connection.Password);
        Assert.StartsWith("enc:", connection.Password);
    }

    [Fact]
    public void GetDecryptedPassword_ReturnsOriginalPassword()
    {
        // Arrange
        var connection = new ServerConnection();
        connection.SetPassword("mypassword");
        
        // Act
        var decrypted = connection.GetDecryptedPassword();
        
        // Assert
        Assert.Equal("mypassword", decrypted);
    }

    [Fact]
    public void EffectiveAuthType_UsesAuthenticationType_WhenSet()
    {
        // Arrange
        var connection = new ServerConnection
        {
            AuthenticationType = "EntraMFA",
            UseWindowsAuthentication = true // Should be ignored
        };
        
        // Act
        var authType = connection.EffectiveAuthType;
        
        // Assert
        Assert.Equal("EntraMFA", authType);
    }

    [Fact]
    public void EffectiveAuthType_FallsBackToLegacyFlag_WhenAuthenticationTypeIsNull()
    {
        // Arrange
        var connection = new ServerConnection
        {
            AuthenticationType = null,
            UseWindowsAuthentication = false
        };
        
        // Act
        var authType = connection.EffectiveAuthType;
        
        // Assert
        Assert.Equal("SqlServer", authType);
    }
}
