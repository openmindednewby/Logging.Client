using System.Security.Claims;
using Logging.Client.Context;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Http;

namespace Logging.Client.Tests;

public class SentryUserContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUser_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new SentryUserContextMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_CallsNextMiddleware()
    {
        var nextCalled = false;
        var middleware = new SentryUserContextMiddleware(next: _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        var httpContext = new DefaultHttpContext();

        await middleware.InvokeAsync(httpContext);

        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUserWithClaims_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-guid-abc", "tenant-guid-xyz");

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUserWithoutTenantClaim_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-guid-abc", tenantId: null);

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithAuthenticatedUserWithoutSubClaim_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext(userId: null, tenantId: "tenant-456");

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedUser_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithConcurrentRequests_EachProcessedIndependently()
    {
        const int requestCount = 50;
        var processedCount = 0;

        var middleware = new SentryUserContextMiddleware(next: _ =>
        {
            Interlocked.Increment(ref processedCount);
            return Task.CompletedTask;
        });

        var tasks = Enumerable.Range(0, requestCount).Select(i =>
        {
            var httpContext = CreateAuthenticatedContext($"user-{i}", $"tenant-{i}");
            return middleware.InvokeAsync(httpContext);
        });

        await Task.WhenAll(tasks);

        processedCount.Should().Be(requestCount);
    }

    [Fact]
    public async Task InvokeAsync_WithNullUserIdentity_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithCorrelationId_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        CorrelationIdContext.Current = "test-correlation-id-abc";

        try
        {
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
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = CreateAuthenticatedContext("user-123", "tenant-456");

        CorrelationIdContext.Current = null;

        var act = async () => await middleware.InvokeAsync(httpContext);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task InvokeAsync_WithUnauthenticatedWithCorrelationId_DoesNotThrow()
    {
        var middleware = new SentryUserContextMiddleware(next: _ => Task.CompletedTask);
        var httpContext = new DefaultHttpContext();

        CorrelationIdContext.Current = "anon-correlation-id";

        try
        {
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
