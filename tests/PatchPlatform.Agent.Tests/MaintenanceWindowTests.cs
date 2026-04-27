using PatchPlatform.Shared.Crypto;

namespace PatchPlatform.Agent.Tests;

public class MaintenanceWindowTests
{
    [Theory]
    [InlineData("02:00-04:00", "2024-01-15T03:00:00Z", true)]
    [InlineData("02:00-04:00", "2024-01-15T01:59:00Z", false)]
    [InlineData("02:00-04:00", "2024-01-15T04:00:00Z", false)]
    [InlineData("23:00-01:00", "2024-01-15T23:30:00Z", true)]
    [InlineData("23:00-01:00", "2024-01-15T00:30:00Z", true)]
    [InlineData("23:00-01:00", "2024-01-15T01:30:00Z", false)]
    [InlineData("Mon-Fri 02:00-04:00", "2024-01-15T03:00:00Z", true)]
    [InlineData("Mon-Fri 02:00-04:00", "2024-01-20T03:00:00Z", false)]
    [InlineData("Sat,Sun 01:00-05:00", "2024-01-20T03:00:00Z", true)]
    [InlineData("Sat,Sun 01:00-05:00", "2024-01-15T03:00:00Z", false)]
    public void IsInWindow_ReturnsExpected(string expression, string utcDateTimeStr, bool expected)
    {
        var utcNow = DateTimeOffset.Parse(utcDateTimeStr);
        var result = MaintenanceWindowHelper.IsInWindow(expression, utcNow);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsInWindow_EmptyExpression_ReturnsFalse()
    {
        var result = MaintenanceWindowHelper.IsInWindow("", DateTimeOffset.UtcNow);
        Assert.False(result);
    }

    [Fact]
    public void IsInWindow_NullExpression_ReturnsFalse()
    {
        var result = MaintenanceWindowHelper.IsInWindow(null!, DateTimeOffset.UtcNow);
        Assert.False(result);
    }
}
