using System.Diagnostics;
using Logging.Client.Configuration;
using Logging.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Logging.Client.Tests;

public class TraceContextAndConsoleFormatTests
{
    [Fact]
    public void CreateLoggerConfiguration_WithCurrentActivity_AddsTraceIdAndSpanId()
    {
        using var activity = new Activity("probe").SetIdFormat(ActivityIdFormat.W3C).Start();
        var expectedTraceId = activity.TraceId.ToHexString();
        var expectedSpanId = activity.SpanId.ToHexString();

        var logEvent = Capture(l => l.Information("traced"));

        ((ScalarValue)logEvent.Properties["TraceId"]).Value.Should().Be(expectedTraceId);
        ((ScalarValue)logEvent.Properties["SpanId"]).Value.Should().Be(expectedSpanId);
    }

    [Fact]
    public void CreateLoggerConfiguration_WithoutCurrentActivity_AddsNoTraceId()
    {
        Activity.Current = null;

        var logEvent = Capture(l => l.Information("untraced"));

        logEvent.Properties.Should().NotContainKey("TraceId").And.NotContainKey("SpanId");
    }

    [Theory]
    [InlineData("Production", ConsoleLogFormat.Json)]
    [InlineData("Staging", ConsoleLogFormat.Json)]
    [InlineData("Development", ConsoleLogFormat.Text)]
    public void ResolveFormat_WithoutOverride_PicksJsonOutsideDevelopment(string environment, ConsoleLogFormat expected)
    {
        var options = new LoggingOptions();

        var format = ConsoleSink.ResolveFormat(options, environment);

        format.Should().Be(expected);
    }

    [Fact]
    public void ResolveFormat_WithExplicitTextInProduction_ReturnsText()
    {
        var options = new LoggingOptions { ConsoleFormat = ConsoleLogFormat.Text };

        var format = ConsoleSink.ResolveFormat(options, "Production");

        format.Should().Be(ConsoleLogFormat.Text);
    }

    [Fact]
    public void BindFormat_WithTextInConfiguration_SetsText()
    {
        var options = new LoggingOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:ConsoleFormat"] = "text" })
            .Build();

        ConsoleSink.BindFormat(configuration, options);

        options.ConsoleFormat.Should().Be(ConsoleLogFormat.Text);
    }

    private static LogEvent Capture(Action<ILogger> write)
    {
        var sink = new CollectingSink();
        var options = new LoggingOptions { SinkType = LogSinkType.None, EnablePiiMasking = false };
        using var logger = LoggingServiceExtensions.CreateLoggerConfiguration(options, "Production")
            .WriteTo.Sink(sink)
            .CreateLogger();

        write(logger);

        return sink.Events.Single();
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
