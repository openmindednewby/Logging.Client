using Logging.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Logging.Client.Extensions;

internal static class MinimumLevels
{
    private const string MinimumLevelKey = "Serilog:MinimumLevel";
    private const string DefaultKey = "Serilog:MinimumLevel:Default";
    private const string OverrideSection = "Serilog:MinimumLevel:Override";

    internal static void Bind(IConfiguration configuration, LoggingOptions options)
    {
        if (TryParse(configuration[DefaultKey] ?? configuration[MinimumLevelKey], out var level))
            options.MinimumLevel = level;

        foreach (var entry in configuration.GetSection(OverrideSection).GetChildren())
        {
            if (TryParse(entry.Value, out var overrideLevel))
                options.MinimumLevelOverrides[entry.Key] = overrideLevel;
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
