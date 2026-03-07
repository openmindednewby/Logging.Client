using Logging.Client.Configuration;
using Logging.Client.Extensions;
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
        options.LokiUrl.Should().Be("http://loki:3100");
        options.SinkType.Should().Be(LogSinkType.Loki);
        options.EnablePiiMasking.Should().BeTrue();
        options.ConsoleTemplate.Should().Contain("{ServiceName}");
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
