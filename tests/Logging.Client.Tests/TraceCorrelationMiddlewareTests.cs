using System.Diagnostics;
using Logging.Client.Context;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Context;

namespace Logging.Client.Tests;

public class TraceCorrelationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithCurrentActivity_UsesTraceIdAsCorrelationId()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var traceId = activity.TraceId.ToHexString();
        string? captured = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            captured = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(new DefaultHttpContext());

        captured.Should().Be(traceId);
    }

    [Fact]
    public async Task InvokeAsync_WithCurrentActivityAndInboundHeader_KeepsTraceIdAsCorrelationId()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var traceId = activity.TraceId.ToHexString();
        string? captured = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            captured = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "partner-ref-42";

        await middleware.InvokeAsync(httpContext);

        captured.Should().Be(traceId);
    }

    [Fact]
    public async Task InvokeAsync_WithCurrentActivityAndInboundHeader_EchoesInboundHeaderInResponse()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "partner-ref-43";

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString().Should().Be("partner-ref-43");
    }

    [Fact]
    public async Task InvokeAsync_WithInboundHeader_LogsItAsCorrelationIdProperty()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "partner-ref-44";

        var logEvent = await LogInside(httpContext);

        ((ScalarValue)logEvent.Properties[CorrelationIdMiddleware.InboundCorrelationIdPropertyName]).Value
            .Should().Be("partner-ref-44");
    }

    [Fact]
    public async Task InvokeAsync_WithoutInboundHeader_LogsNoInboundCorrelationIdProperty()
    {
        using var activity = new Activity("request").SetIdFormat(ActivityIdFormat.W3C).Start();

        var logEvent = await LogInside(new DefaultHttpContext());

        logEvent.Properties.Should().NotContainKey(CorrelationIdMiddleware.InboundCorrelationIdPropertyName);
    }

    [Fact]
    public async Task InvokeAsync_WithoutActivity_FallsBackToInboundHeader()
    {
        Activity.Current = null;
        string? captured = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            captured = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = "partner-ref-45";

        await middleware.InvokeAsync(httpContext);

        captured.Should().Be("partner-ref-45");
    }

    [Fact]
    public void InboundCorrelationIdPropertyName_WhenRead_IsSnakeCase()
    {
        var name = CorrelationIdMiddleware.InboundCorrelationIdPropertyName;

        name.Should().Be("correlation_id");
    }

    private static async Task<LogEvent> LogInside(HttpContext httpContext)
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().Enrich.FromLogContext().WriteTo.Sink(sink).CreateLogger();
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            logger.Information("inside");
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext);

        return sink.Events.Single();
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = new();

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
