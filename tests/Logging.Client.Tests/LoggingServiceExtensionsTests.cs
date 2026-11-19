using Logging.Client.Configuration;
using Logging.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

namespace Logging.Client.Tests;

public class LoggingServiceExtensionsTests
{
    [Fact]
    public void ConfigureSink_LokiSinkType_ConfiguresWithoutException()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            LokiUrl = "http://localhost:3100",
            SinkType = LogSinkType.Loki,
        };

        var act = () => LoggingServiceExtensions.ConfigureSink(configuration, options, "Development");
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSink_ConsoleSinkType_ConfiguresWithoutException()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            SinkType = LogSinkType.Console,
        };

        var act = () => LoggingServiceExtensions.ConfigureSink(configuration, options, "Development");
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSink_LokiType_ProducesWorkingLogger()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            LokiUrl = "http://localhost:3100",
            SinkType = LogSinkType.Loki,
        };

        LoggingServiceExtensions.ConfigureSink(configuration, options, "Test");

        var logger = configuration.CreateLogger();

        var act = () => logger.Information("Test log message");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void ConfigureSink_ConsoleType_ProducesWorkingLogger()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            SinkType = LogSinkType.Console,
        };

        LoggingServiceExtensions.ConfigureSink(configuration, options, "Test");

        var logger = configuration.CreateLogger();

        var act = () => logger.Information("Test log message");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void LoggingOptions_Defaults_AreCorrect()
    {
        var options = new LoggingOptions();

        options.ServiceName.Should().Be("Unknown");
        options.LokiUrl.Should().Be("http://loki.monitoring.svc.cluster.local:3100");
        options.SinkType.Should().Be(LogSinkType.Console);
        options.LokiQueueLimit.Should().Be(10_000);
        options.EnablePiiMasking.Should().BeTrue();
        options.ConsoleTemplate.Should().Contain("{ServiceName}");
        options.ConsoleTemplate.Should().Contain("{CorrelationId}");
        options.SentryDsn.Should().Be(string.Empty);
        options.SentryEnvironment.Should().Be("Development");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Error);
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindLokiQueueLimit_ValidValue_UpdatesOption()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiQueueLimit"] = "25000" })
            .Build();
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindLokiQueueLimit(config, options);

        options.LokiQueueLimit.Should().Be(25_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-50")]
    public void BindLokiQueueLimit_InvalidOrMissing_KeepsDefault(string raw)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiQueueLimit"] = raw })
            .Build();
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindLokiQueueLimit(config, options);

        options.LokiQueueLimit.Should().Be(10_000);
    }

    [Theory]
    [InlineData("Loki", LogSinkType.Loki)]
    [InlineData("loki", LogSinkType.Loki)]
    [InlineData("Console", LogSinkType.Console)]
    public void BindSinkType_KnownValue_OptsIntoThatSink(string raw, LogSinkType expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:SinkType"] = raw })
            .Build();
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSinkType(config, options);

        options.SinkType.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Elasticsearch")]
    [InlineData("7")]
    public void BindSinkType_UnknownOrMissing_KeepsConsoleDefault(string raw)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:SinkType"] = raw })
            .Build();
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSinkType(config, options);

        options.SinkType.Should().Be(LogSinkType.Console);
    }

    [Fact]
    public void BindLokiUrl_ValueProvided_OverridesDefault()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LokiUrl"] = "http://10.0.0.2:31300",
            })
            .Build();
        var options = new LoggingOptions();

        LoggingServiceExtensions.BindLokiUrl(config, options);

        options.LokiUrl.Should().Be("http://10.0.0.2:31300");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindLokiUrl_EmptyOrMissing_KeepsExistingValue(string raw)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiUrl"] = raw })
            .Build();
        var options = new LoggingOptions { LokiUrl = "http://existing:3100" };

        LoggingServiceExtensions.BindLokiUrl(config, options);

        options.LokiUrl.Should().Be("http://existing:3100");
    }

    [Fact]
    public void LogSinkType_HasExpectedValues()
    {
        Enum.GetValues<LogSinkType>().Should().HaveCount(3);
        ((int)LogSinkType.Loki).Should().Be(0);
        ((int)LogSinkType.Console).Should().Be(1);
        ((int)LogSinkType.None).Should().Be(2);
    }
}
