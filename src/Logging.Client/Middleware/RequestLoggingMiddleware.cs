using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Logging.Client.Middleware;

/// <summary>
/// Provides extension methods for adding Serilog request logging middleware
/// with tenant and user context enrichment.
/// </summary>
public static class RequestLoggingMiddleware
{
    /// <summary>
    /// Adds Serilog request logging with TenantId and UserId enrichment.
    /// Call this after UseCorrelationIdMiddleware() in the middleware pipeline.
    /// </summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                // Add TenantId from claims
                var tenantId = httpContext.User?.FindFirst("tenant_id")?.Value;
                if (!string.IsNullOrEmpty(tenantId))
                    diagnosticContext.Set("TenantId", tenantId);

                // Add UserId from claims
                var userId = httpContext.User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                    diagnosticContext.Set("UserId", userId);

                // Add request-specific context
                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown");
            };
        });
    }
}
