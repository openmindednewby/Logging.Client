using Logging.Client.Context;
using Microsoft.AspNetCore.Http;
using Sentry;

namespace Logging.Client.Middleware;

/// <summary>Enriches the Sentry scope with the user ID, tenant ID and correlation ID.</summary>
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

    /// <summary>Sets the Sentry user scope from JWT claims and tags the correlation ID.</summary>
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
