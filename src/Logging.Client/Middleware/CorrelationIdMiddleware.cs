using System.Diagnostics;
using Logging.Client.Context;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Logging.Client.Middleware;

/// <summary>
/// Sets the correlation ID to the W3C trace id, else the X-Correlation-ID header or a new id, for logs and responses.
/// </summary>
public class CorrelationIdMiddleware
{
    /// <summary>
    /// The HTTP header name used for correlation ID propagation.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <summary>
    /// Log property holding an inbound X-Correlation-ID; the trace id stays the correlation id.
    /// </summary>
    public const string InboundCorrelationIdPropertyName = "correlation_id";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, extracting or generating a correlation ID.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var inboundId = context.Request.Headers[HeaderName].FirstOrDefault();
        var correlationId = CurrentTraceId() ?? inboundId ?? Guid.NewGuid().ToString("N");

        CorrelationIdContext.Current = correlationId;
        context.Response.Headers[HeaderName] = inboundId ?? correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (PushInboundId(inboundId))
        {
            await _next(context);
        }
    }

    private static string? CurrentTraceId() =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.TraceId.ToHexString() : null;

    private static IDisposable? PushInboundId(string? inboundId) =>
        inboundId is null ? null : LogContext.PushProperty(InboundCorrelationIdPropertyName, inboundId);
}
