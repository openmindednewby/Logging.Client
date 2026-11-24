using Logging.Client.Configuration;
using Microsoft.Extensions.Configuration;

namespace Logging.Client.Tests;

public class SectionOptionsBindingTests
{
    [Fact]
    public void LoggingSectionOptions_WithLoggingKeys_BindsEveryValue()
    {
        var configuration = Configuration(new()
        {
            ["Logging:SinkType"] = "Loki",
            ["Logging:LokiUrl"] = "http://loki:3100",
            ["Logging:LokiQueueLimit"] = "500",
            ["Logging:ConsoleFormat"] = "Json",
        });

        var bound = configuration.GetSection(LoggingSectionOptions.SectionName).Get<LoggingSectionOptions>();

        bound.Should().BeEquivalentTo(new LoggingSectionOptions
        {
            SinkType = "Loki",
            LokiUrl = "http://loki:3100",
            LokiQueueLimit = "500",
            ConsoleFormat = "Json",
        });
    }

    [Fact]
    public void LoggingSectionOptions_WhenCreated_HasNoValues()
    {
        var options = new LoggingSectionOptions();

        options.Should().BeEquivalentTo(new { SinkType = (string?)null, LokiUrl = (string?)null, LokiQueueLimit = (string?)null, ConsoleFormat = (string?)null });
    }

    [Fact]
    public void SentrySectionOptions_WithSentryKeys_BindsEveryValue()
    {
        var configuration = Configuration(new()
        {
            ["Sentry:Enabled"] = "true",
            ["Sentry:Dsn"] = "https://public@example.invalid/1",
            ["Sentry:Environment"] = "staging",
            ["Sentry:MinimumEventLevel"] = "Error",
            ["Sentry:TracesSampleRate"] = "0.2",
        });

        var bound = configuration.GetSection(SentrySectionOptions.SectionName).Get<SentrySectionOptions>();

        bound.Should().BeEquivalentTo(new SentrySectionOptions
        {
            Enabled = "true",
            Dsn = "https://public@example.invalid/1",
            Environment = "staging",
            MinimumEventLevel = "Error",
            TracesSampleRate = "0.2",
        });
    }

    [Fact]
    public void SentrySectionOptions_WithoutSentrySection_BindsNull()
    {
        var configuration = Configuration(new() { ["Logging:SinkType"] = "Console" });

        var bound = configuration.GetSection(SentrySectionOptions.SectionName).Get<SentrySectionOptions>();

        bound.Should().BeNull();
    }

    [Fact]
    public void MinimumLevelSectionOptions_WithDefaultAndOverrides_BindsEveryValue()
    {
        var configuration = Configuration(new()
        {
            ["Serilog:MinimumLevel:Default"] = "Debug",
            ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning",
        });

        var bound = configuration.GetSection(MinimumLevelSectionOptions.SectionName).Get<MinimumLevelSectionOptions>();

        bound!.Default.Should().Be("Debug");
        bound.Override.Should().Equal(new Dictionary<string, string?> { ["Microsoft.AspNetCore"] = "Warning" });
    }

    [Fact]
    public void MinimumLevelSectionOptions_WhenCreated_HasNoDefaultAndNoOverrides()
    {
        var options = new MinimumLevelSectionOptions();

        options.Should().BeEquivalentTo(new { Default = (string?)null, Shorthand = (string?)null, Override = new Dictionary<string, string?>() });
    }

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
