using Logging.Client.Context;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Http;

namespace Logging.Client.Tests;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_NoHeaderProvided_GeneratesNewCorrelationId()
    {
        // Arrange
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context =>
        {
            capturedCorrelationId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        capturedCorrelationId.Should().NotBeNullOrEmpty();
        capturedCorrelationId!.Length.Should().Be(32); // Guid.ToString("N") = 32 hex chars
    }

    [Fact]
    public async Task InvokeAsync_HeaderProvided_UsesExistingCorrelationId()
    {
        // Arrange
        const string existingId = "test-corr-1234567890";
        string? capturedCorrelationId = null;
        var middleware = new CorrelationIdMiddleware(next: context =>
        {
            capturedCorrelationId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingId;

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        capturedCorrelationId.Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_Always_AddsCorrelationIdToResponseHeader()
    {
        // Arrange
        const string existingId = "resp-corr-id-test";
        var middleware = new CorrelationIdMiddleware(next: _ => Task.CompletedTask);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName] = existingId;

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert - header set directly on response
        httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault()
            .Should().Be(existingId);
    }

    [Fact]
    public async Task InvokeAsync_GeneratedId_IsAlsoInResponseHeader()
    {
        // Arrange
        string? capturedId = null;
        var middleware = new CorrelationIdMiddleware(next: _ =>
        {
            capturedId = CorrelationIdContext.Current;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        var responseHeaderValue = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].FirstOrDefault();
        responseHeaderValue.Should().Be(capturedId);
    }

    [Fact]
    public async Task InvokeAsync_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new CorrelationIdMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_ConcurrentRequests_EachHasOwnCorrelationId()
    {
        // Arrange
        const int requestCount = 100;
        var capturedIds = new string[requestCount];

        // Act
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

        // Assert - all IDs should be unique
        capturedIds.Distinct().Count().Should().Be(requestCount);
    }

    [Fact]
    public void HeaderName_IsCorrectValue()
    {
        CorrelationIdMiddleware.HeaderName.Should().Be("X-Correlation-ID");
    }
}
