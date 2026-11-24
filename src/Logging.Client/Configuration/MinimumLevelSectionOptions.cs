namespace Logging.Client.Configuration;

internal sealed class MinimumLevelSectionOptions
{
    internal const string SectionName = "Serilog:MinimumLevel";

    public string? Default { get; set; }

    public Dictionary<string, string?> Override { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Shorthand { get; set; }
}
