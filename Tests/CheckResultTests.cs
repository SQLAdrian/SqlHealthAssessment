using SqlHealthAssessment.Data.Models;

namespace SqlHealthAssessment.Tests.Models;

public class CheckResultTests
{
    [Fact]
    public void CheckResult_InitializesWithDefaultValues()
    {
        var result = new CheckResult();
        
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.CheckName);
        Assert.Equal(string.Empty, result.Category);
        Assert.False(result.Passed);
    }

    [Fact]
    public void CheckResult_SetsPropertiesCorrectly()
    {
        var result = new CheckResult
        {
            CheckName = "CPU Check",
            Category = "Performance",
            Severity = "Medium",
            Message = "CPU usage at 80%",
            InstanceName = "localhost",
            Passed = false
        };
        
        Assert.Equal("CPU Check", result.CheckName);
        Assert.Equal("Performance", result.Category);
        Assert.Equal("Medium", result.Severity);
        Assert.Equal("CPU usage at 80%", result.Message);
        Assert.Equal("localhost", result.InstanceName);
        Assert.False(result.Passed);
    }
}
