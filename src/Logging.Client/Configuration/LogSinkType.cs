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

    /// <summary>Off: no sink at all, not even the console.</summary>
    None = 2,
}
