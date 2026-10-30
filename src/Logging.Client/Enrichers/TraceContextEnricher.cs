using System.Diagnostics;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Enrichers;

/// <summary>
/// Adds TraceId and SpanId from the current <see cref="Activity"/> so a log line joins its trace.
/// </summary>
public sealed class TraceContextEnricher : ILogEventEnricher
{
    public const string TraceIdPropertyName = "TraceId";
    public const string SpanIdPropertyName = "SpanId";
    public const string W3CTraceIdPropertyName = "trace_id";
    public const string W3CSpanIdPropertyName = "span_id";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var activity = Activity.Current;
        if (activity is null) return;

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(TraceIdPropertyName, activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(SpanIdPropertyName, activity.SpanId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(W3CTraceIdPropertyName, activity.TraceId.ToHexString()));
        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(W3CSpanIdPropertyName, activity.SpanId.ToHexString()));
    }
}
