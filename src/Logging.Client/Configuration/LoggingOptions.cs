using Serilog.Events;

namespace Logging.Client.Configuration;

/// <summary>
/// Configuration options for structured logging.
/// </summary>
public sealed class LoggingOptions
{
    /// <summary>
    /// The name of the service (e.g., "IdentityService", "OnlineMenuService").
    /// Used as a Loki label for filtering logs by service.
    /// </summary>
    public string ServiceName { get; set; } = "Unknown";

    /// <summary>
    /// The URL of the Grafana Loki instance for log ingestion.
    /// </summary>
    public string LokiUrl { get; set; } = "http://loki.monitoring.svc.cluster.local:3100";

    /// <summary>
    /// The active log sink type. Only one sink is active at a time.
    /// </summary>
    public LogSinkType SinkType { get; set; } = LogSinkType.Loki;

    /// <summary>
    /// Maximum number of log events buffered in memory while awaiting delivery to Loki.
    /// When the queue is full, older events are dropped (and logged once to the console)
    /// rather than allowing unbounded heap growth when Loki is unreachable, slow, or
    /// rate-limiting. Set generously above peak burst rate; default is 10,000 events
    /// (~5–15 MB depending on payload size).
    /// </summary>
    public int LokiQueueLimit { get; set; } = 10_000;

    /// <summary>
    /// Whether to enable PII masking in log output.
    /// When enabled, emails, phone numbers, and sensitive property values are masked.
    /// </summary>
    public bool EnablePiiMasking { get; set; } = true;

    /// <summary>
    /// The output template for console logging.
    /// </summary>
    public string ConsoleTemplate { get; set; } =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {Message:lj}{NewLine}{Exception}";

    /// <summary>
    /// The Sentry DSN for error monitoring. When empty, Sentry is disabled.
    /// </summary>
    public string SentryDsn { get; set; } = string.Empty;

    /// <summary>
    /// The Sentry environment name (e.g., "Development", "Staging", "Production").
    /// </summary>
    public string SentryEnvironment { get; set; } = "Development";

    /// <summary>
    /// The minimum log event level required to send events to Sentry.
    /// Defaults to Error to avoid sending info/warning noise.
    /// </summary>
    public LogEventLevel SentryMinimumLevel { get; set; } = LogEventLevel.Error;

    /// <summary>
    /// The sample rate for Sentry performance monitoring traces (0.0 to 1.0).
    /// A value of 0.0 disables tracing; 1.0 captures every transaction.
    /// Defaults to 0.0 (disabled) to avoid overhead when not explicitly configured.
    /// Override via the <c>Sentry:TracesSampleRate</c> config key or <c>SENTRY_TRACES_SAMPLE_RATE</c> env var.
    /// </summary>
    public double SentryTracesSampleRate { get; set; }
}
