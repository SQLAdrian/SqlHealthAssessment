# Testing Guide for SqlHealthAssessment

## Overview
This guide will help you understand and write tests for the SqlHealthAssessment application.

## Test Project Structure

```
Tests/
├── ConnectionManagerTests.cs      # Tests for connection management
├── ServerConnectionTests.cs       # Tests for connection model
└── (more test files as you add them)
```

## Running Tests

### From Command Line
```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ConnectionManagerTests"

# Run tests and generate coverage report
dotnet test /p:CollectCoverage=true
```

### From Visual Studio
1. Open Test Explorer (Test > Test Explorer)
2. Click "Run All" or right-click specific tests

## Test Anatomy

### Basic Test Structure
```csharp
[Fact]  // Marks this as a test method
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var manager = new ServerConnectionManager();
    var connection = new ServerConnection { ServerNames = "localhost" };
    
    // Act - Execute the method being tested
    manager.AddConnection(connection);
    var result = manager.GetConnections();
    
    // Assert - Verify the results
    Assert.Single(result);
    Assert.Equal("localhost", result[0].ServerNames);
}
```

### Test Naming Convention
`MethodName_Scenario_ExpectedBehavior`

Examples:
- `AddConnection_IncreasesConnectionCount`
- `GetEnabledConnections_ReturnsOnlyEnabledConnections`
- `SetPassword_EncryptsPassword`

## Common xUnit Assertions

```csharp
// Equality
Assert.Equal(expected, actual);
Assert.NotEqual(expected, actual);

// Null checks
Assert.Null(value);
Assert.NotNull(value);

// Boolean
Assert.True(condition);
Assert.False(condition);

// Collections
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Single(collection);
Assert.Contains(item, collection);
Assert.All(collection, item => Assert.True(item.IsEnabled));

// Exceptions
Assert.Throws<ArgumentException>(() => method());

// Strings
Assert.StartsWith("prefix", text);
Assert.EndsWith("suffix", text);
Assert.Contains("substring", text);
```

## Testing Patterns

### 1. Testing Simple Methods
```csharp
[Fact]
public void GetServerCount_ReturnsCorrectCount()
{
    var connection = new ServerConnection { ServerNames = "s1\ns2\ns3" };
    var count = connection.GetServerCount();
    Assert.Equal(3, count);
}
```

### 2. Testing with Multiple Scenarios (Theory)
```csharp
[Theory]
[InlineData("server1,server2", 2)]
[InlineData("server1\nserver2\nserver3", 3)]
[InlineData("server1", 1)]
public void GetServerList_ParsesCorrectly(string input, int expectedCount)
{
    var connection = new ServerConnection { ServerNames = input };
    var servers = connection.GetServerList();
    Assert.Equal(expectedCount, servers.Count);
}
```

### 3. Testing Events
```csharp
[Fact]
public void SetCurrentServer_RaisesEvent()
{
    var manager = new ServerConnectionManager();
    bool eventRaised = false;
    manager.OnConnectionChanged += () => eventRaised = true;
    
    manager.SetCurrentServer("some-id");
    
    Assert.True(eventRaised);
}
```

### 4. Testing with Mocks (for dependencies)
```csharp
using Moq;

[Fact]
public void LoadData_CallsQueryExecutor()
{
    // Arrange
    var mockExecutor = new Mock<IQueryExecutor>();
    mockExecutor.Setup(x => x.ExecuteQuery(It.IsAny<string>()))
                .Returns(new DataTable());
    
    var service = new DataService(mockExecutor.Object);
    
    // Act
    service.LoadData();
    
    // Assert
    mockExecutor.Verify(x => x.ExecuteQuery(It.IsAny<string>()), Times.Once);
}
```

## What to Test

### ✅ DO Test:
1. **Business Logic** - Core functionality and rules
2. **Edge Cases** - Empty strings, null values, boundary conditions
3. **Error Handling** - Exceptions are thrown when expected
4. **State Changes** - Objects are modified correctly
5. **Public APIs** - Methods that other code depends on

### ❌ DON'T Test:
1. **Framework Code** - .NET framework methods
2. **Third-party Libraries** - Already tested by their authors
3. **Simple Properties** - Auto-properties with no logic
4. **UI Code** - Blazor components (use integration tests instead)

## Example: Adding Tests for a New Service

Let's say you want to test `CredentialProtector`:

```csharp
namespace SqlHealthAssessment.Tests.Data;

public class CredentialProtectorTests
{
    [Fact]
    public void Encrypt_ReturnsEncryptedString()
    {
        var plainText = "mypassword";
        var encrypted = CredentialProtector.Encrypt(plainText);
        
        Assert.NotNull(encrypted);
        Assert.NotEqual(plainText, encrypted);
        Assert.StartsWith("enc:", encrypted);
    }

    [Fact]
    public void Decrypt_ReturnsOriginalString()
    {
        var plainText = "mypassword";
        var encrypted = CredentialProtector.Encrypt(plainText);
        var decrypted = CredentialProtector.Decrypt(encrypted);
        
        Assert.Equal(plainText, decrypted);
    }

    [Fact]
    public void Decrypt_HandlesNullInput()
    {
        var result = CredentialProtector.Decrypt(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void IsEncrypted_ReturnsTrueForEncryptedString()
    {
        var encrypted = CredentialProtector.Encrypt("test");
        Assert.True(CredentialProtector.IsEncrypted(encrypted));
    }

    [Fact]
    public void IsEncrypted_ReturnsFalseForPlainText()
    {
        Assert.False(CredentialProtector.IsEncrypted("plaintext"));
    }
}
```

## Test Coverage Goals

- **Critical Code**: 80-100% coverage (authentication, data access)
- **Business Logic**: 70-90% coverage (services, managers)
- **Models**: 50-70% coverage (simple classes)
- **UI Components**: 0-30% coverage (use integration tests)

## Next Steps

1. **Run existing tests**: `dotnet test`
2. **Add tests for your changes**: When you modify code, add tests
3. **Test-Driven Development (TDD)**: Write tests before code
4. **Continuous Integration**: Run tests automatically on commit

## Resources

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [.NET Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)

## Common Issues

### Issue: Tests fail with "file in use"
**Solution**: Ensure ConnectionManager doesn't lock files during tests. Use in-memory storage for tests.

### Issue: Tests are slow
**Solution**: Mock database connections. Don't hit real SQL Server in unit tests.

### Issue: Tests pass locally but fail in CI
**Solution**: Avoid file system dependencies. Use relative paths or in-memory storage.
