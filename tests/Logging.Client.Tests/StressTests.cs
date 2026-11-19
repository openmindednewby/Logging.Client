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
        const int entryCount = 1000;
        var completedCount = 0;

        using var logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Sink(new CountingSink(() => Interlocked.Increment(ref completedCount)))
            .CreateLogger();

        Parallel.For(0, entryCount, i =>
        {
            logger.Information("Stress test entry {Index}", i);
        });

        Thread.Sleep(100);

        completedCount.Should().Be(entryCount);
    }

    [Fact]
    public void PiiMasking_Under1000ConcurrentCalls_AllMaskedCorrectly()
    {
        const int iterations = 1000;
        var emails = Enumerable.Range(0, iterations)
            .Select(i => $"user{i:D4}@domain{i % 10}.com")
            .ToArray();
        var phones = Enumerable.Range(0, iterations)
            .Select(i => $"{i:D3}-{(i * 7) % 1000:D3}-{(i * 13) % 10000:D4}")
            .ToArray();

        var emailResults = new string[iterations];
        var phoneResults = new string[iterations];

        Parallel.For(0, iterations, i =>
        {
            emailResults[i] = PiiMaskingPolicy.MaskIfPii(emails[i]);
            phoneResults[i] = PiiMaskingPolicy.MaskIfPii(phones[i]);
        });

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
        const int taskCount = 1000;
        var results = new string[taskCount];

        var tasks = Enumerable.Range(0, taskCount).Select(async i =>
        {
            CorrelationIdContext.Current = $"stress-{i}";
            await Task.Yield();
            results[i] = CorrelationIdContext.Current!;
        });

        await Task.WhenAll(tasks);

        for (var i = 0; i < taskCount; i++)
        {
            results[i].Should().Be($"stress-{i}");
        }
    }

    [Fact]
    public void CorrelationIdEnricher_HighThroughput_EnrichesCorrectly()
    {
        const int iterations = 1000;
        var enricher = new CorrelationIdEnricher();
        var factory = new TestPropertyFactory();

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
