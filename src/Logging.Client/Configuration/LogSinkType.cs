namespace Logging.Client.Configuration;

/// <summary>
/// Determines which log sink is active. Only one sink is used at a time.
/// </summary>
public enum LogSinkType
{
    /// <summary>
    /// Stage 1 (current) - Lightweight, Grafana-native log aggregation.
    /// </summary>
    Loki = 0,

    /// <summary>
    /// Stage 1 (current) - Console-only output, no infrastructure required.
    /// </summary>
    Console = 1,

    /// <summary>
    /// Off: no sink at all, not even the console. For consumers that run without our
    /// observability stack (e.g. the ProovID white-label build). Correlation-id and PII
    /// masking stay in the pipeline; a sink added by the host still receives masked events.
    /// Select with <c>Logging__SinkType=None</c>.
    /// </summary>
    None = 2,

    // Stage 2 (future) - Async via RabbitMQ to PostgreSQL
    // LoggingService = 3,

    // Stage 3 (future) - Full-text search at scale
    // Elasticsearch = 4,
}
