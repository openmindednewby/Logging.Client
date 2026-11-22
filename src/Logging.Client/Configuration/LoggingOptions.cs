using Serilog.Events;

namespace Logging.Client.Configuration;

/// <summary>
/// Configuration options for structured logging.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>The name of the service (e.g., "TenantService", "OnlineMenuService").</summary>
    public string ServiceName { get; set; } = "Unknown";

    /// <summary>The URL of the Grafana Loki instance for log ingestion.</summary>
    public string LokiUrl { get; set; } = "http://loki.monitoring.svc.cluster.local:3100";

    /// <summary>The active log sink type.</summary>
    public LogSinkType SinkType { get; set; } = LogSinkType.Console;

    /// <summary>Maximum number of log events buffered in memory while awaiting delivery to Loki.</summary>
    public int LokiQueueLimit { get; set; } = 10_000;

    /// <summary>Whether to enable PII masking in log output.</summary>
    public bool EnablePiiMasking { get; set; } = true;

    /// <summary>The output template for console logging.</summary>
    public string ConsoleTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    /// <summary>
    /// Console rendering; null means Text in Development and Json elsewhere. Override via <c>Logging:ConsoleFormat</c>.
    /// </summary>
    public ConsoleLogFormat? ConsoleFormat { get; set; }

    /// <summary>
    /// The Sentry DSN for error monitoring. When empty, Sentry is disabled.
    /// </summary>
    public string SentryDsn { get; set; } = string.Empty;

    /// <summary>Kill switch for Sentry.</summary>
    public bool SentryEnabled { get; set; } = true;

    /// <summary>
    /// The Sentry environment name (e.g., "Development", "Staging", "Production").
    /// </summary>
    public string SentryEnvironment { get; set; } = "Development";

    /// <summary>The minimum log event level required to send events to Sentry.</summary>
    public LogEventLevel SentryMinimumLevel { get; set; } = LogEventLevel.Error;

    /// <summary>The sample rate for Sentry performance monitoring traces (0.0 to 1.0).</summary>
    public double SentryTracesSampleRate { get; set; }

    /// <summary>
    /// The default minimum level. Override via <c>Serilog:MinimumLevel:Default</c>.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// Per-source minimum levels. Config <c>Serilog:MinimumLevel:Override:&lt;source&gt;</c> adds or replaces entries.
    /// </summary>
    public IDictionary<string, LogEventLevel> MinimumLevelOverrides { get; } = new Dictionary<string, LogEventLevel>
    {
        ["Microsoft"] = LogEventLevel.Warning,
        ["Microsoft.Hosting.Lifetime"] = LogEventLevel.Information,
        ["Microsoft.AspNetCore.Hosting.Diagnostics"] = LogEventLevel.Warning,
        ["System"] = LogEventLevel.Warning,
    };
}
