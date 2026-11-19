using Logging.Client.Context;

namespace Logging.Client.Tests;

public class CorrelationIdContextTests
{
    [Fact]
    public void Current_DefaultValue_IsNull()
    {
        CorrelationIdContext.Current = null;

        CorrelationIdContext.Current.Should().BeNull();
    }

    [Fact]
    public void Current_SetValue_ReturnsSetValue()
    {
        const string expected = "test-correlation-id";

        CorrelationIdContext.Current = expected;

        CorrelationIdContext.Current.Should().Be(expected);

        CorrelationIdContext.Current = null;
    }

    [Fact]
    public async Task Current_AsyncLocalIsolation_EachTaskHasOwnValue()
    {
        const int taskCount = 50;
        var results = new string[taskCount];

        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            CorrelationIdContext.Current = $"corr-{i}";
            await Task.Delay(1);
            results[i] = CorrelationIdContext.Current!;
        });

        await Task.WhenAll(tasks);

        for (var i = 0; i < taskCount; i++)
        {
            results[i].Should().Be($"corr-{i}");
        }
    }
}
