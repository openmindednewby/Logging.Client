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
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            LokiUrl = "http://localhost:3100",
            SinkType = LogSinkType.Loki,
        };

        // Act & Assert - should not throw
        var act = () => LoggingServiceExtensions.ConfigureSink(configuration, options, "Development");
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSink_ConsoleSinkType_ConfiguresWithoutException()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            SinkType = LogSinkType.Console,
        };

        // Act & Assert
        var act = () => LoggingServiceExtensions.ConfigureSink(configuration, options, "Development");
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSink_LokiType_ProducesWorkingLogger()
    {
        // Arrange
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

        // Act
        var logger = configuration.CreateLogger();

        // Assert - logger should write without exception
        var act = () => logger.Information("Test log message");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void ConfigureSink_ConsoleType_ProducesWorkingLogger()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            ServiceName = "TestService",
            SinkType = LogSinkType.Console,
        };

        LoggingServiceExtensions.ConfigureSink(configuration, options, "Test");

        // Act
        var logger = configuration.CreateLogger();

        // Assert
        var act = () => logger.Information("Test log message");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void LoggingOptions_Defaults_AreCorrect()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.ServiceName.Should().Be("Unknown");
        options.LokiUrl.Should().Be("http://loki.monitoring.svc.cluster.local:3100");
        // The direct Loki sink is OPT-IN. It defaults to Console because Promtail already
        // ships stdout to the real Loki on both clusters, and a Loki sink pointed at an
        // endpoint that does not resolve buffers to OOM rather than failing loudly.
        options.SinkType.Should().Be(LogSinkType.Console);
        options.LokiQueueLimit.Should().Be(10_000);
        options.EnablePiiMasking.Should().BeTrue();
        options.ConsoleTemplate.Should().Contain("{ServiceName}");
        // CorrelationId must be in the console line so Promtail-based clusters
        // (stdout scraping, no direct Loki sink) can match a `|= <id>` filter.
        options.ConsoleTemplate.Should().Contain("{CorrelationId}");
        options.SentryDsn.Should().Be(string.Empty);
        options.SentryEnvironment.Should().Be("Development");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Error);
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindLokiQueueLimit_ValidValue_UpdatesOption()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiQueueLimit"] = "25000" })
            .Build();
        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindLokiQueueLimit(config, options);

        // Assert
        options.LokiQueueLimit.Should().Be(25_000);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-50")]
    public void BindLokiQueueLimit_InvalidOrMissing_KeepsDefault(string raw)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiQueueLimit"] = raw })
            .Build();
        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindLokiQueueLimit(config, options);

        // Assert
        options.LokiQueueLimit.Should().Be(10_000);
    }

    [Theory]
    [InlineData("Loki", LogSinkType.Loki)]
    [InlineData("loki", LogSinkType.Loki)]
    [InlineData("Console", LogSinkType.Console)]
    public void BindSinkType_KnownValue_OptsIntoThatSink(string raw, LogSinkType expected)
    {
        // Arrange — this is the documented Logging__SinkType opt-in path
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:SinkType"] = raw })
            .Build();
        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSinkType(config, options);

        // Assert
        options.SinkType.Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Elasticsearch")]
    [InlineData("7")]
    public void BindSinkType_UnknownOrMissing_KeepsConsoleDefault(string raw)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:SinkType"] = raw })
            .Build();
        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSinkType(config, options);

        // Assert
        options.SinkType.Should().Be(LogSinkType.Console);
    }

    [Fact]
    public void BindLokiUrl_ValueProvided_OverridesDefault()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LokiUrl"] = "http://10.0.0.2:31300",
            })
            .Build();
        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindLokiUrl(config, options);

        // Assert
        options.LokiUrl.Should().Be("http://10.0.0.2:31300");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BindLokiUrl_EmptyOrMissing_KeepsExistingValue(string raw)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Logging:LokiUrl"] = raw })
            .Build();
        var options = new LoggingOptions { LokiUrl = "http://existing:3100" };

        // Act
        LoggingServiceExtensions.BindLokiUrl(config, options);

        // Assert
        options.LokiUrl.Should().Be("http://existing:3100");
    }

    [Fact]
    public void LogSinkType_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<LogSinkType>().Should().HaveCount(2);
        ((int)LogSinkType.Loki).Should().Be(0);
        ((int)LogSinkType.Console).Should().Be(1);
    }
}
