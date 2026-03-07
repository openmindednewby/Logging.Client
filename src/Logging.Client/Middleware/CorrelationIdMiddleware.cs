using Logging.Client.Context;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Logging.Client.Middleware;

/// <summary>
/// Middleware that reads or generates a correlation ID from the X-Correlation-ID header
/// and pushes it into <see cref="CorrelationIdContext"/> and Serilog's <see cref="LogContext"/>.
/// </summary>
public class CorrelationIdMiddleware
{
    /// <summary>
    /// The HTTP header name used for correlation ID propagation.
    /// </summary>
    public const string HeaderName = "X-Correlation-ID";

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
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
                            ?? Guid.NewGuid().ToString("N");

        // Store in AsyncLocal for access anywhere in the request pipeline
        CorrelationIdContext.Current = correlationId;

        // Add to response headers for downstream correlation
        context.Response.Headers[HeaderName] = correlationId;

        // Push to Serilog LogContext so all log entries include it
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
