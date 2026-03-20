using Logging.Client.Context;
using Microsoft.AspNetCore.Http;
using Sentry;

namespace Logging.Client.Middleware;

/// <summary>
/// Middleware that enriches the Sentry scope with the authenticated user's ID, tenant ID,
/// and the current correlation ID. Reads the "sub" claim (user ID) and "tenant_id" claim
/// from the JWT. Only opaque GUIDs are sent to Sentry; no PII (emails, names) is ever included.
/// Must be placed after UseAuthentication() and UseAuthorization() in the pipeline.
/// </summary>
public class SentryUserContextMiddleware
{
    private const string SubClaimType = "sub";
    private const string TenantClaimType = "tenant_id";

    private readonly RequestDelegate _next;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentryUserContextMiddleware"/> class.
    /// </summary>
    public SentryUserContextMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Processes the HTTP request, setting the Sentry user scope from JWT claims
    /// and attaching the correlation ID as a Sentry tag for cross-service tracing.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var user = context.User;
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;

        if (isAuthenticated)
        {
            var userId = user!.FindFirst(SubClaimType)?.Value;
            var tenantId = user.FindFirst(TenantClaimType)?.Value;

            SentrySdk.ConfigureScope(scope =>
            {
                if (!string.IsNullOrEmpty(userId))
                    scope.User = new SentryUser { Id = userId };

                if (!string.IsNullOrEmpty(tenantId))
                    scope.SetTag("tenantId", tenantId);

                SetCorrelationIdTag(scope);
            });
        }
        else
        {
            // Clear previous user context for unauthenticated requests
            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new SentryUser();
                scope.UnsetTag("tenantId");
                SetCorrelationIdTag(scope);
            });
        }

        await _next(context);
    }

    private static void SetCorrelationIdTag(Scope scope)
    {
        var correlationId = CorrelationIdContext.Current;
        if (!string.IsNullOrEmpty(correlationId))
            scope.SetTag("correlationId", correlationId);
    }
}
