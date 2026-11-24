namespace Logging.Client.Configuration;

internal sealed class SentrySectionOptions
{
    internal const string SectionName = "Sentry";

    public string? Enabled { get; set; }

    public string? Dsn { get; set; }

    public string? Environment { get; set; }

    public string? MinimumEventLevel { get; set; }

    public string? TracesSampleRate { get; set; }
}
