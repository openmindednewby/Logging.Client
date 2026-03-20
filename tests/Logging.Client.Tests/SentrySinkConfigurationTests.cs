using Logging.Client.Configuration;
using Logging.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Logging.Client.Tests;

public class SentrySinkConfigurationTests
{
    [Fact]
    public void ConfigureSentrySink_EmptyDsn_DoesNotThrow()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = string.Empty,
        };

        // Act & Assert - should not add Sentry sink when DSN is empty
        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_NullDsn_DoesNotThrow()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = null!,
        };

        // Act & Assert
        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_EmptyDsn_ProducesWorkingLogger()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = string.Empty,
        };

        LoggingServiceExtensions.ConfigureSentrySink(configuration, options);

        // Act
        var logger = configuration.CreateLogger();

        // Assert - logger works without Sentry
        var act = () => logger.Error("Test error without Sentry");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void ConfigureSentrySink_ValidDsn_DoesNotThrow()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = "https://examplePublicKey@o0.ingest.sentry.io/0",
            SentryEnvironment = "Test",
            SentryMinimumLevel = LogEventLevel.Error,
        };

        // Act & Assert - should configure Sentry sink without throwing
        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_ValidDsn_ProducesWorkingLogger()
    {
        // Arrange
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = "https://examplePublicKey@o0.ingest.sentry.io/0",
            SentryEnvironment = "Test",
            SentryMinimumLevel = LogEventLevel.Error,
        };

        LoggingServiceExtensions.ConfigureSentrySink(configuration, options);

        // Act
        var logger = configuration.CreateLogger();

        // Assert - logger should write without exception (Sentry won't connect but won't block)
        var act = () => logger.Error("Test error with Sentry configured");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void LoggingOptions_SentryDefaults_AreCorrect()
    {
        // Arrange & Act
        var options = new LoggingOptions();

        // Assert
        options.SentryDsn.Should().Be(string.Empty);
        options.SentryEnvironment.Should().Be("Development");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Error);
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void ConfigureSentrySink_CustomMinimumLevel_AcceptsAllLevels()
    {
        // Arrange & Act & Assert - verify all log levels can be set
        foreach (var level in Enum.GetValues<LogEventLevel>())
        {
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console();

            var options = new LoggingOptions
            {
                SentryDsn = "https://examplePublicKey@o0.ingest.sentry.io/0",
                SentryMinimumLevel = level,
            };

            var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
            act.Should().NotThrow($"level {level} should be accepted");
        }
    }

    [Fact]
    public void BindSentryConfiguration_TracesSampleRate_ParsesValidValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "0.5",
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert
        options.SentryTracesSampleRate.Should().Be(0.5);
    }

    [Fact]
    public void BindSentryConfiguration_TracesSampleRate_ClampsAboveOne()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "5.0",
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert
        options.SentryTracesSampleRate.Should().Be(1.0);
    }

    [Fact]
    public void BindSentryConfiguration_TracesSampleRate_ClampsBelowZero()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "-0.5",
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindSentryConfiguration_TracesSampleRate_IgnoresInvalidValue()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "not-a-number",
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert - should keep the default value
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindSentryConfiguration_AllProperties_ParsedCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:Dsn"] = "https://test@sentry.io/123",
                ["Sentry:Environment"] = "Production",
                ["Sentry:MinimumEventLevel"] = "Warning",
                ["Sentry:TracesSampleRate"] = "0.25",
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert
        options.SentryDsn.Should().Be("https://test@sentry.io/123");
        options.SentryEnvironment.Should().Be("Production");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Warning);
        options.SentryTracesSampleRate.Should().Be(0.25);
    }

    [Fact]
    public void BindSentryConfiguration_EmptySection_DoesNotModifyDefaults()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new LoggingOptions();

        // Act - non-existent section
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert - all defaults preserved
        options.SentryDsn.Should().Be(string.Empty);
        options.SentryEnvironment.Should().Be("Development");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Error);
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Theory]
    [InlineData("0.0", 0.0)]
    [InlineData("0.1", 0.1)]
    [InlineData("0.5", 0.5)]
    [InlineData("1.0", 1.0)]
    public void BindSentryConfiguration_TracesSampleRate_AcceptsValidRange(string input, double expected)
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = input,
            })
            .Build();

        var options = new LoggingOptions();

        // Act
        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        // Assert
        options.SentryTracesSampleRate.Should().Be(expected);
    }
}
