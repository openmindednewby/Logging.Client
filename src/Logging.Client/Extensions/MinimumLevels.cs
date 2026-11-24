using Logging.Client.Configuration;
using Serilog;
using Serilog.Events;

namespace Logging.Client.Extensions;

internal static class MinimumLevels
{
    internal static void Bind(MinimumLevelSectionOptions minimumLevels, LoggingOptions options)
    {
        if (TryParse(minimumLevels.Default ?? minimumLevels.Shorthand, out var level))
            options.MinimumLevel = level;

        foreach (var (source, value) in minimumLevels.Override)
        {
            if (TryParse(value, out var overrideLevel))
                options.MinimumLevelOverrides[source] = overrideLevel;
        }
    }

    internal static LoggerConfiguration Apply(LoggerConfiguration configuration, LoggingOptions options)
    {
        configuration.MinimumLevel.Is(options.MinimumLevel);
        foreach (var (source, level) in options.MinimumLevelOverrides)
            configuration.MinimumLevel.Override(source, level);
        return configuration;
    }

    private static bool TryParse(string? value, out LogEventLevel level) =>
        Enum.TryParse(value, true, out level) && Enum.IsDefined(level);
}
