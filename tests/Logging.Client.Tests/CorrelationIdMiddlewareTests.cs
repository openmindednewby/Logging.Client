using Logging.Client.Context;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Http;

namespace Logging.Client.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithoutHeaderProvided_GeneratesNewCorrelationId()
    {
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context =>
        {
            capturedCorrelationId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        capturedCorrelationId.Should().NotBeNullOrEmpty();
        capturedCorrelationId!.Length.Should().Be(32);
    }

    [Fact]
    public async Task InvokeAsync_WithHeaderProvided_UsesExistingCorrelationId()
    {
        const string existingId = "test-corr-1234567890";
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context =>
        {
            capturedCorrelationId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingId;

        await middleware.InvokeAsync(httpContext);

        capturedCorrelationId.Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_WithAnyRequest_AddsCorrelationIdToResponseHeader()
    {
        const string existingId = "resp-corr-id-test";
        var middleware = new CorrelationIdMiddleware(next: _ => Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingId;

        await middleware.InvokeAsync(httpContext);

        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault()
            .Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_WithGeneratedId_IsAlsoInResponseHeader()
    {
        string? capturedId = null;
        var middleware = new CorrelationIdMiddleware(next: _ =>
        {
            capturedId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        var responseHeaderValue = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();
        responseHeaderValue.Should().Be(capturedId);
    }

    [Fact]
    public async Task InvokeAsync_WithAnyRequest_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithConcurrentRequests_EachHasOwnCorrelationId()
    {
        const int requestCount = 100;
        var capturedIds = new string[requestCount];

        var tasks = Enumerable.Range(0, requestCount).Select(async i =>
        {
            var middleware = new CorrelationIdMiddleware(next: _ =>
            {
                capturedIds[i] = CorrelationIdContext.Current!;
                return Task.CompletedTask;
            });

            var httpContext = new DefaultHttpContext();
            await middleware.InvokeAsync(httpContext);
        });

        await Task.WhenAll(tasks);

        capturedIds.Distinct().Count().Should().Be(requestCount);
    }

    [Fact]
    public void HeaderName_WhenRead_IsCorrectValue()
    {
        CorrelationIdMiddleware.HeaderName.Should().Be("X-Correlation-ID");
    }
}
