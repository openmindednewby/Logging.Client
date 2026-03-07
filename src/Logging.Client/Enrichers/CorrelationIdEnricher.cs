using Logging.Client.Context;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Enrichers;

/// <summary>
/// Enriches log events with the current correlation ID from <see cref="CorrelationIdContext"/>.
/// </summary>
public class CorrelationIdEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name used for correlation ID in log events.
    /// </summary>
    public const string PropertyName = "CorrelationId";

    /// <summary>
    /// Adds the CorrelationId property to the log event if a correlation ID is available.
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationIdContext.Current;
        if (string.IsNullOrEmpty(correlationId)) return;

        var property = propertyFactory.CreateProperty(PropertyName, correlationId);
        logEvent.AddPropertyIfAbsent(property);
    }
}
