using Logging.Client.Configuration;
using Logging.Client.Extensions;

namespace Logging.Client.Tests;

/// <summary>
/// Regression: PII masking must hold through the REAL configured Serilog pipeline, not only
/// when the masker is called directly. Serilog converts strings with its built-in scalar
/// policy before any IDestructuringPolicy runs, so a destructuring-policy-only masker never
/// saw <c>{@Email}</c> or <c>{Email}</c>.
/// </summary>
public class PiiMaskingPipelineTests
{
    private const string Email = "longusername@company.org";
    private const string Phone = "+1-234-567-8901";

    [Theory]
    [InlineData(LogSinkType.Console, "{@Email}")]
    [InlineData(LogSinkType.Console, "{Email}")]
    [InlineData(LogSinkType.None, "{@Email}")]
    [InlineData(LogSinkType.None, "{Email}")]
    public void Log_EmailProperty_IsMaskedInPropertyAndRenderedMessage(LogSinkType sinkType, string template)
    {
        var logEvent = Capture(sinkType, l => l.Information("Contact " + template, Email));

        logEvent.Properties["Email"].ToString().Should().NotContain("longusername");
        logEvent.Properties["Email"].ToString().Should().Contain("@company.org");
        logEvent.RenderMessage().Should().NotContain("longusername");
    }

    [Fact]
    public void Log_DestructuredObject_MasksNestedPiiAndSensitiveNames()
    {
        var user = new { Email, Phone, Password = "hunter2", Name = "Ann" };

        var logEvent = Capture(LogSinkType.Console, l => l.Information("{@User}", user));

        var rendered = logEvent.Properties["User"].ToString();
        rendered.Should().NotContain("longusername")
            .And.NotContain("234-567")
            .And.NotContain("hunter2")
            .And.Contain("***REDACTED***")
            .And.Contain("Ann");
        logEvent.RenderMessage().Should().NotContain("longusername");
    }

    [Fact]
    public void Log_SensitiveScalarName_IsRedacted()
    {
        var logEvent = Capture(LogSinkType.Console, l => l.Information("{Token}", "abc123"));

        logEvent.Properties["Token"].ToString().Should().Contain("***REDACTED***");
    }

    [Fact]
    public void Log_MaskingDisabled_LeavesValueUntouched()
    {
        var logEvent = Capture(
            LogSinkType.Console,
            l => l.Information("{Email}", Email),
            enableMasking: false);

        logEvent.Properties["Email"].ToString().Should().Contain("longusername");
    }

    private static LogEvent Capture(LogSinkType sinkType, Action<Serilog.ILogger> log, bool enableMasking = true)
    {
        var options = new LoggingOptions
        {
            ServiceName = "Svc",
            SinkType = sinkType,
            EnablePiiMasking = enableMasking,
        };
        var sink = new CapturingSink();
        using var logger = LoggingServiceExtensions
            .CreateLoggerConfiguration(options, "Test")
            .WriteTo.Sink(sink)
            .CreateLogger();

        log(logger);

        return sink.Events.Should().ContainSingle().Subject;
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
