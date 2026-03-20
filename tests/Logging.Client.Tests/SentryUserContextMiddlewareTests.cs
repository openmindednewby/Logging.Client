using System.Security.Claims;
using Logging.Client.Context;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Http;

namespace Logging.Client.Tests;

public class SentryUserContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new SentryUserContextMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        // Act
        await middleware.InvokeAsync(httpContext);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_CallsNextMiddleware()
    {
        // Arrange
        var nextCalled = false;
        var middleware = new SentryUserContextMiddleware(next: _ =>
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
    public async Task InvokeAsync_AuthenticatedUserWithClaims_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-guid-abc", "tenant-guid-xyz");

        // Act & Assert - should not throw even without Sentry SDK initialized
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithoutTenantClaim_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-guid-abc", tenantId: null);

        // Act & Assert
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUserWithoutSubClaim_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext(userId: null, tenantId: "tenant-456");

        // Act & Assert
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        // Act & Assert - clears scope without throwing
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_ConcurrentRequests_EachProcessedIndependently()
    {
        // Arrange
        const int requestCount = 50;
        var processedCount = 0;

        var middleware = new SentryUserContextMiddleware(next: _ =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.CompletedTask;
        });

        // Act
        var tasks = Enumerable.Range(0, requestCount).Select(i =>
        {
            var httpContext = CreateAuthenticatedContext($"user-{i}", $"tenant-{i}");
            return middleware.InvokeAsync(httpContext);
        });

        await Task.WhenAll(tasks);

        // Assert
        processedCount.Should().Be(requestCount);
    }

    [Fact]
    public async Task InvokeAsync_NullUserIdentity_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();
        // DefaultHttpContext has User with non-authenticated identity by default

        // Act & Assert
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithCorrelationId_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        // Set a correlation ID in the async-local context
        CorrelationIdContext.Current = "test-correlation-id-abc";

        try
        {
            // Act & Assert - should set correlation ID tag without throwing
            var act = async () => await middleware.InvokeAsync(httpContext);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            CorrelationIdContext.Current = null;
        }
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationId_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        // Ensure no correlation ID is set
        CorrelationIdContext.Current = null;

        // Act & Assert - should handle null correlation ID gracefully
        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_UnauthenticatedWithCorrelationId_DoesNotThrow()
    {
        // Arrange
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        // Set a correlation ID even for unauthenticated requests
        CorrelationIdContext.Current = "anon-correlation-id";

        try
        {
            // Act & Assert - correlation ID should be set regardless of auth state
            var act = async () => await middleware.InvokeAsync(httpContext);
            await act.Should().NotThrowAsync();
        }
        finally
        {
            CorrelationIdContext.Current = null;
        }
    }

    private static DefaultHttpContext CreateAuthenticatedContext(string? userId, string? tenantId)
    {
        var claims = new List<Claim>();

        if (!string.IsNullOrEmpty(userId))
            claims.Add(new Claim("sub", userId));

        if (!string.IsNullOrEmpty(tenantId))
            claims.Add(new Claim("tenant_id", tenantId));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        return httpContext;
    }
}
