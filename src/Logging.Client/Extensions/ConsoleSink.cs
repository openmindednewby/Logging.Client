using Logging.Client.Configuration;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Compact;

namespace Logging.Client.Extensions;

internal static class ConsoleSink
{
    private const string DevelopmentEnvironment = "Development";

    internal static void Configure(LoggerConfiguration configuration, LoggingOptions options, string environment)
    {
        if (!LoggingServiceExtensions.WritesToConsole(options)) return;

        if (ResolveFormat(options, environment) == ConsoleLogFormat.Json)
            configuration.WriteTo.Console(new RenderedCompactJsonFormatter());
        else
            configuration.WriteTo.Console(outputTemplate: options.ConsoleTemplate);
    }

    internal static ConsoleLogFormat ResolveFormat(LoggingOptions options, string environment)
    {
        var isDevelopment = string.Equals(environment, DevelopmentEnvironment, StringComparison.OrdinalIgnoreCase);
        return options.ConsoleFormat ?? (isDevelopment ? ConsoleLogFormat.Text : ConsoleLogFormat.Json);
    }

    internal static void BindFormat(IConfiguration configuration, LoggingOptions options)
    {
        var value = configuration["Logging:ConsoleFormat"];
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<ConsoleLogFormat>(value, true, out var parsed)
            && Enum.IsDefined(parsed))
            options.ConsoleFormat = parsed;
    }
}
