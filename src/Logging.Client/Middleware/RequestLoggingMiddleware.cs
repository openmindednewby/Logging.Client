using Microsoft.AspNetCore.Builder;
using Serilog;

namespace Logging.Client.Middleware;

/// <summary>Extension methods for Serilog request logging with tenant and user enrichment.</summary>
public static class RequestLoggingMiddleware
{
    /// <summary>Adds Serilog request logging with TenantId and UserId enrichment.</summary>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                var tenantId = httpContext.User?.FindFirst("tenant_id")?.Value
                    ?? httpContext.User?.FindFirst("tenantId")?.Value;
                if (!string.IsNullOrEmpty(tenantId))
                    diagnosticContext.Set("TenantId", tenantId);

                var userId = httpContext.User?.FindFirst("sub")?.Value;
                if (!string.IsNullOrEmpty(userId))
                    diagnosticContext.Set("UserId", userId);

                diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
                diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.FirstOrDefault() ?? "unknown");
            };
        });
    }
}
