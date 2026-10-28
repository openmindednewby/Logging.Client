using Logging.Client.Configuration;
using Logging.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Tests;

public class MinimumLevelsTests
{
    [Fact]
    public void CreateLoggerConfiguration_WithDefaultDebugInConfiguration_EnablesDebug()
    {
        var logger = BuildLogger(new() { ["Serilog:MinimumLevel:Default"] = "Debug" });

        logger.IsEnabled(LogEventLevel.Debug).Should().BeTrue();
    }

    [Fact]
    public void CreateLoggerConfiguration_WithPollyOverrideWarning_DropsPollyInformation()
    {
        var logger = BuildLogger(new() { ["Serilog:MinimumLevel:Override:Polly"] = "Warning" });

        var polly = logger.ForContext(Constants.SourceContextPropertyName, "Polly.Retry");

        polly.IsEnabled(LogEventLevel.Information).Should().BeFalse();
    }

    [Fact]
    public void CreateLoggerConfiguration_WithMicrosoftOverrideDebug_ReplacesBuiltInWarning()
    {
        var logger = BuildLogger(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Debug",
            ["Serilog:MinimumLevel:Override:Microsoft"] = "Debug",
        });

        var microsoft = logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.AspNetCore.Routing");

        microsoft.IsEnabled(LogEventLevel.Debug).Should().BeTrue();
    }

    [Fact]
    public void CreateLoggerConfiguration_WithoutSerilogSection_KeepsInformationAndMicrosoftWarning()
    {
        var logger = BuildLogger(new());

        var microsoft = logger.ForContext(Constants.SourceContextPropertyName, "Microsoft.EntityFrameworkCore");

        logger.IsEnabled(LogEventLevel.Debug).Should().BeFalse();
        microsoft.IsEnabled(LogEventLevel.Information).Should().BeFalse();
    }

    [Fact]
    public void Bind_WithShorthandMinimumLevel_SetsDefault()
    {
        var options = new LoggingOptions();
        var configuration = Configuration(new() { ["Serilog:MinimumLevel"] = "Error" });

        MinimumLevels.Bind(configuration, options);

        options.MinimumLevel.Should().Be(LogEventLevel.Error);
    }

    [Fact]
    public void Bind_WithUnknownLevelName_KeepsInformation()
    {
        var options = new LoggingOptions();
        var configuration = Configuration(new() { ["Serilog:MinimumLevel:Default"] = "Loud" });

        MinimumLevels.Bind(configuration, options);

        options.MinimumLevel.Should().Be(LogEventLevel.Information);
    }

    private static Logger BuildLogger(Dictionary<string, string?> settings)
    {
        var options = new LoggingOptions { ServiceName = "levels-test", SinkType = LogSinkType.None };
        MinimumLevels.Bind(Configuration(settings), options);
        return LoggingServiceExtensions.CreateLoggerConfiguration(options, "Production").CreateLogger();
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
