# Tests

Unit tests for SQL Health Assessment, built with [xUnit](https://xunit.net/) and [Moq](https://github.com/moq/moq4).

## Running Tests

```bash
cd Tests
dotnet test
```

With verbose output:

```bash
dotnet test --logger "console;verbosity=detailed"
```

Run a specific test class:

```bash
dotnet test --filter "FullyQualifiedName~ConnectionManagerTests"
```

## Test Coverage

| File | Tests | Coverage |
|---|---|---|
| `ConnectionManagerTests.cs` | 6 | ConnectionManager — add, remove, update, events |
| `ServerConnectionTests.cs` | 11 | ServerConnection — parsing, auth, encryption |

## Adding Tests

Follow the Arrange / Act / Assert pattern:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var service = new MyService();

    // Act
    var result = service.DoSomething();

    // Assert
    Assert.NotNull(result);
}
```

See [TESTING_GUIDE.md](TESTING_GUIDE.md) for patterns, mocking examples, and guidelines on what to test.

## Notes

- Tests target `net8.0-windows` to match the main project.
- Run from the `Tests/` folder directly to isolate from the WPF build process.
- Integration tests (requiring a live SQL Server) are tagged `[Trait("Category", "Integration")]` and skipped in CI by default.
