using Logging.Client.Context;

namespace Logging.Client.Tests;

public class CorrelationIdContextTests
{
    [Fact]
    public void Current_DefaultValue_IsNull()
    {
        // Reset to known state
        CorrelationIdContext.Current = null;

        CorrelationIdContext.Current.Should().BeNull();
    }

    [Fact]
    public void Current_SetValue_ReturnsSetValue()
    {
        // Arrange
        const string expected = "test-correlation-id";

        // Act
        CorrelationIdContext.Current = expected;

        // Assert
        CorrelationIdContext.Current.Should().Be(expected);

        // Cleanup
        CorrelationIdContext.Current = null;
    }

    [Fact]
    public async Task Current_AsyncLocalIsolation_EachTaskHasOwnValue()
    {
        // Arrange
        const int taskCount = 50;
        var results = new string[taskCount];

        // Act
        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            CorrelationIdContext.Current = $"corr-{i}";
            await Task.Delay(1); // force async continuation
            results[i] = CorrelationIdContext.Current!;
        });

        await Task.WhenAll(tasks);

        // Assert - each task should have its own value
        for (var i = 0; i < taskCount; i++)
        {
            results[i].Should().Be($"corr-{i}");
        }
    }
}
