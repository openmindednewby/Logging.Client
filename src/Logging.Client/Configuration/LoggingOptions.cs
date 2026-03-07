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
    public string LokiUrl { get; set; } = "http://loki:3100";

    /// <summary>
    /// The active log sink type. Only one sink is active at a time.
    /// </summary>
    public LogSinkType SinkType { get; set; } = LogSinkType.Loki;

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
}
