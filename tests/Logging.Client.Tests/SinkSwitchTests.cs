using Logging.Client.Configuration;
using Logging.Client.Context;
using Logging.Client.Diagnostics;
using Logging.Client.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;

namespace Logging.Client.Tests;

/// <summary>
/// OBS-1a "Make Metrics.Client / Logging.Client switchable": a consumer with no Loki and no
/// Sentry (ProovID, a client without our observability stack) must be able to run the
/// package console-only or fully off, selected by config, with correlation ids and PII
/// masking still applied.
/// </summary>
public class SinkSwitchTests
{
    private const string FakeDsn = "https://public@example.invalid/1";
    private const string Email = "longusername@company.org";

    [Fact]
    public void BindSinkType_None_FromConfiguration_SelectsNone()
    {
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSinkType(Config(("Logging:SinkType", "None")), options);

        options.SinkType.Should().Be(LogSinkType.None);
    }

    [Theory]
    [InlineData(LogSinkType.None, false)]
    [InlineData(LogSinkType.Console, true)]
    [InlineData(LogSinkType.Loki, true)]
    public void WritesToConsole_PerSinkType(LogSinkType sinkType, bool expected)
    {
        var options = new LoggingOptions { SinkType = sinkType };

        LoggingServiceExtensions.WritesToConsole(options).Should().Be(expected);
    }

    [Fact]
    public void ResolveEffectiveSinkType_None_IsLeftAloneAndNeverResolvesLoki()
    {
        var options = new LoggingOptions { SinkType = LogSinkType.None };

        var result = LokiEndpointGuard.ResolveEffectiveSinkType(
            options,
            _ => throw new InvalidOperationException("must not resolve"),
            _ => throw new InvalidOperationException("must not warn"));

        result.Should().Be(LogSinkType.None);
    }

    [Theory]
    [InlineData("", true, false)]
    [InlineData(FakeDsn, true, true)]
    [InlineData(FakeDsn, false, false)]
    public void IsSentryActive_RequiresDsnAndSwitch(string dsn, bool enabled, bool expected)
    {
        var options = new LoggingOptions { SentryDsn = dsn, SentryEnabled = enabled };

        LoggingServiceExtensions.IsSentryActive(options).Should().Be(expected);
    }

    [Fact]
    public void BindSentryConfiguration_EnabledFalse_TurnsSentryOff()
    {
        var options = new LoggingOptions();
        var config = Config(("Sentry:Dsn", FakeDsn), ("Sentry:Enabled", "false"));

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryEnabled.Should().BeFalse();
        LoggingServiceExtensions.IsSentryActive(options).Should().BeFalse();
    }

    [Fact]
    public void LoggingOptions_Defaults_AreUnchanged()
    {
        var options = new LoggingOptions();

        options.SinkType.Should().Be(LogSinkType.Console);
        options.SentryEnabled.Should().BeTrue();
    }

    [Fact]
    public void CreateLoggerConfiguration_NoneSink_EnrichesAndMasksExactlyLikeConsole()
    {
        // Masking and enrichment are sink-independent: the None pipeline must produce the
        // same event as the Console pipeline. (Whether the PII policy masks a destructured
        // string at all is a separate pre-existing question, logged in the OBS-1 task doc.)
        var none = Capture(LogSinkType.None);
        var console = Capture(LogSinkType.Console);

        none.Properties["Email"].ToString().Should().Be(console.Properties["Email"].ToString());
        none.Properties["CorrelationId"].ToString().Should().Contain("corr-123");
        none.Properties["ServiceName"].ToString().Should().Contain("Svc");
    }

    [Fact]
    public void AddStructuredLogging_NoneSinkAndSentryDisabled_BuildsWithoutSentry()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Logging:SinkType"] = "None",
            ["Sentry:Dsn"] = FakeDsn,
            ["Sentry:Enabled"] = "false",
        });

        builder.AddStructuredLogging(o => o.ServiceName = "Svc");
        using var app = builder.Build();

        app.Services.GetService<IHub>().Should().BeNull();
    }

    private static LogEvent Capture(LogSinkType sinkType)
    {
        var options = new LoggingOptions { ServiceName = "Svc", SinkType = sinkType };
        var sink = new CapturingSink();
        using var logger = LoggingServiceExtensions
            .CreateLoggerConfiguration(options, "Test")
            .WriteTo.Sink(sink)
            .CreateLogger();
        CorrelationIdContext.Current = "corr-123";
        try
        {
            logger.Information("{@Email}", Email);
        }
        finally
        {
            CorrelationIdContext.Current = null;
        }

        return sink.Events.Should().ContainSingle().Subject;
    }

    private static IConfiguration Config(params (string Key, string Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => (string?)p.Value))
            .Build();

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
