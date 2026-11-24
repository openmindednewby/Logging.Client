namespace Logging.Client.Configuration;

internal sealed class LoggingSectionOptions
{
    internal const string SectionName = "Logging";

    public string? SinkType { get; set; }

    public string? LokiUrl { get; set; }

    public string? LokiQueueLimit { get; set; }

    public string? ConsoleFormat { get; set; }
}
