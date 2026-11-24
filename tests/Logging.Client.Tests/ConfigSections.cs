using Logging.Client.Configuration;
using Microsoft.Extensions.Configuration;

namespace Logging.Client.Tests;

internal static class ConfigSections
{
    internal static LoggingSectionOptions Logging(IConfiguration configuration) =>
        configuration.GetSection(LoggingSectionOptions.SectionName).Get<LoggingSectionOptions>() ?? new LoggingSectionOptions();

    internal static SentrySectionOptions? Sentry(IConfiguration configuration) =>
        configuration.GetSection(SentrySectionOptions.SectionName).Get<SentrySectionOptions>();

    internal static MinimumLevelSectionOptions MinimumLevel(IConfiguration configuration)
    {
        var section = configuration.GetSection(MinimumLevelSectionOptions.SectionName);
        var options = section.Get<MinimumLevelSectionOptions>() ?? new MinimumLevelSectionOptions();
        options.Shorthand = section.Value;
        return options;
    }
}
