using Logging.Client.Configuration;
using Logging.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Logging.Client.Tests;

public class SentrySinkConfigurationTests
{
    [Fact]
    public void ConfigureSentrySink_WithEmptyDsn_DoesNotThrow()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = string.Empty,
        };

        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_WithNullDsn_DoesNotThrow()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = null!,
        };

        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_WithEmptyDsn_ProducesWorkingLogger()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = string.Empty,
        };

        LoggingServiceExtensions.ConfigureSentrySink(configuration, options);

        var logger = configuration.CreateLogger();

        var act = () => logger.Error("Test error without Sentry");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void ConfigureSentrySink_WithValidDsn_DoesNotThrow()
    {
        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console();

        var options = new LoggingOptions
        {
            SentryDsn = "https://examplePublicKey@o0.ingest.sentry.io/0",
            SentryEnvironment = "Test",
            SentryMinimumLevel = LogEventLevel.Error,
        };

        var act = () => LoggingServiceExtensions.ConfigureSentrySink(configuration, options);
        act.Should().NotThrow();
    }

    [Fact]
    public void ConfigureSentrySink_WithValidDsn_ProducesWorkingLogger()
    {
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

        var logger = configuration.CreateLogger();

        var act = () => logger.Error("Test error with Sentry configured");
        act.Should().NotThrow();

        logger.Dispose();
    }

    [Fact]
    public void LoggingOptions_WithSentryDefaults_AreCorrect()
    {
        var options = new LoggingOptions();

        options.SentryDsn.Should().Be(string.Empty);
        options.SentryEnvironment.Should().Be("Development");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Error);
        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void ConfigureSentrySink_WithCustomMinimumLevel_AcceptsAllLevels()
    {
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
    public void BindSentryConfiguration_WithTracesSampleRate_ParsesValidValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "0.5",
            })
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryTracesSampleRate.Should().Be(0.5);
    }

    [Fact]
    public void BindSentryConfiguration_WithTracesSampleRate_ClampsAboveOne()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "5.0",
            })
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryTracesSampleRate.Should().Be(1.0);
    }

    [Fact]
    public void BindSentryConfiguration_WithTracesSampleRate_ClampsBelowZero()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "-0.5",
            })
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindSentryConfiguration_WithTracesSampleRate_IgnoresInvalidValue()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = "not-a-number",
            })
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryTracesSampleRate.Should().Be(0.0);
    }

    [Fact]
    public void BindSentryConfiguration_WithAllProperties_ParsedCorrectly()
    {
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

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryDsn.Should().Be("https://test@sentry.io/123");
        options.SentryEnvironment.Should().Be("Production");
        options.SentryMinimumLevel.Should().Be(LogEventLevel.Warning);
        options.SentryTracesSampleRate.Should().Be(0.25);
    }

    [Fact]
    public void BindSentryConfiguration_WithEmptySection_DoesNotModifyDefaults()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

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
    public void BindSentryConfiguration_WithTracesSampleRate_AcceptsValidRange(string input, double expected)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sentry:TracesSampleRate"] = input,
            })
            .Build();

        var options = new LoggingOptions();

        LoggingServiceExtensions.BindSentryConfiguration(config.GetSection("Sentry"), options);

        options.SentryTracesSampleRate.Should().Be(expected);
    }
}
