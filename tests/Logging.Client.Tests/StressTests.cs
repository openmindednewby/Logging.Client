using Logging.Client.Context;
using Logging.Client.Enrichers;
using Logging.Client.Masking;
using Serilog;
using Serilog.Events;
using Serilog.Parsing;

namespace Logging.Client.Tests;

/// <summary>
/// Stress tests for logging components under concurrent load.
/// </summary>
public class StressTests
{
    private static readonly MessageTemplate TestTemplate =
        new MessageTemplateParser().Parse("Stress test message {Index}");

    [Fact]
    public void RapidConcurrentLogWrites_1000Entries_AllComplete()
    {
        // Arrange
        const int entryCount = 1000;
        var completedCount = 0;

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new CountingSink(() => Interlocked.Increment(ref completedCount)))
            .CreateLogger();

        // Act
        Parallel.For(0, entryCount, i =>
        {
            logger.Information("Stress test entry {Index}", i);
        });

        // Give a moment for async operations
        Thread.Sleep(100);

        // Assert
        completedCount.Should().Be(entryCount);
    }

    [Fact]
    public void PiiMasking_Under1000ConcurrentCalls_AllMaskedCorrectly()
    {
        // Arrange
        const int iterations = 1000;
        var emails = Enumerable.Range(0, iterations)
            .Select(i => $"user{i:D4}@domain{i % 10}.com")
            .ToArray();
        var phones = Enumerable.Range(0, iterations)
            .Select(i => $"{i:D3}-{(i * 7) % 1000:D3}-{(i * 13) % 10000:D4}")
            .ToArray();

        var emailResults = new string[iterations];
        var phoneResults = new string[iterations];

        // Act
        Parallel.For(0, iterations, i =>
        {
            emailResults[i] = PiiMaskingPolicy.MaskIfPii(emails[i]);
            phoneResults[i] = PiiMaskingPolicy.MaskIfPii(phones[i]);
        });

        // Assert
        for (var i = 0; i < iterations; i++)
        {
            emailResults[i].Should().NotBe(emails[i], $"email at index {i} should be masked");
            emailResults[i].Should().Contain("@", $"masked email at index {i} should preserve @");

            phoneResults[i].Should().Contain("***", $"phone at index {i} should be masked");
        }
    }

    [Fact]
    public async Task CorrelationIdContext_Under1000ConcurrentTasks_MaintainsIsolation()
    {
        // Arrange
        const int taskCount = 1000;
        var results = new string[taskCount];

        // Act
        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            CorrelationIdContext.Current = $"stress-{i}";
            await Task.Yield(); // Force async continuation
            results[i] = CorrelationIdContext.Current!;
        });

        await Task.WhenAll(tasks);

        // Assert - each task should see its own value
        for (var i = 0; i < taskCount; i++)
        {
            results[i].Should().Be($"stress-{i}");
        }
    }

    [Fact]
    public void CorrelationIdEnricher_HighThroughput_EnrichesCorrectly()
    {
        // Arrange
        const int iterations = 1000;
        var enricher = new CorrelationIdEnricher();
        var factory = new TestPropertyFactory();

        // Act & Assert
        Parallel.For(0, iterations, i =>
        {
            CorrelationIdContext.Current = $"htp-{i}";
            var logEvent = new LogEvent(
                DateTimeOffset.UtcNow,
                LogEventLevel.Information,
                null,
                TestTemplate,
                new[] { new LogEventProperty("Index", new ScalarValue(i)) });

            enricher.Enrich(logEvent, factory);

            logEvent.Properties.Should().ContainKey("CorrelationId");
        });
    }

    /// <summary>
    /// A sink that counts log events for throughput testing.
    /// </summary>
    private sealed class CountingSink : Serilog.Core.ILogEventSink
    {
        private readonly Action _onEmit;

        public CountingSink(Action onEmit)
        {
            _onEmit = onEmit;
        }

        public void Emit(LogEvent logEvent)
        {
            _onEmit();
        }
    }

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
