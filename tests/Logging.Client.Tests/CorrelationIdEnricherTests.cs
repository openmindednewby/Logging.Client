using Logging.Client.Context;
using Logging.Client.Enrichers;
using Serilog.Events;
using Serilog.Parsing;

namespace Logging.Client.Tests;

public class CorrelationIdEnricherTests
{
    private static readonly MessageTemplate EmptyTemplate =
        new MessageTemplateParser().Parse("");

    private readonly CorrelationIdEnricher _enricher = new();

    [Fact]
    public void Enrich_WithCorrelationId_AddsProperty()
    {
        // Arrange
        const string correlationId = "test-corr-123";
        CorrelationIdContext.Current = correlationId;
        var logEvent = CreateLogEvent();

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("CorrelationId");
        logEvent.Properties["CorrelationId"].ToString().Should().Contain(correlationId);

        // Cleanup
        CorrelationIdContext.Current = null;
    }

    [Fact]
    public void Enrich_WithoutCorrelationId_DoesNotAddProperty()
    {
        // Arrange
        CorrelationIdContext.Current = null;
        var logEvent = CreateLogEvent();

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("CorrelationId");
    }

    [Fact]
    public void Enrich_EmptyCorrelationId_DoesNotAddProperty()
    {
        // Arrange
        CorrelationIdContext.Current = "";
        var logEvent = CreateLogEvent();

        // Act
        _enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("CorrelationId");

        // Cleanup
        CorrelationIdContext.Current = null;
    }

    [Fact]
    public void PropertyName_IsCorrect()
    {
        CorrelationIdEnricher.PropertyName.Should().Be("CorrelationId");
    }

    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            EmptyTemplate,
            Array.Empty<LogEventProperty>());
    }

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
