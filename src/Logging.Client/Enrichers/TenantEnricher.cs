using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Enrichers;

/// <summary>
/// Enriches log events with the current tenant ID.
/// Attempts to resolve from ICurrentTenantService first, then falls back to JWT "tenant_id" claim.
/// </summary>
public class TenantEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name used for tenant ID in log events.
    /// </summary>
    public const string PropertyName = "TenantId";

    private const string TenantClaimType = "tenant_id";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantEnricher"/> class.
    /// </summary>
    public TenantEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Adds the TenantId property to the log event.
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var tenantId = ResolveTenantId();
        if (string.IsNullOrEmpty(tenantId)) return;

        var property = propertyFactory.CreateProperty(PropertyName, tenantId);
        logEvent.AddPropertyIfAbsent(property);
    }

    private string? ResolveTenantId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return null;

        // Try to resolve from ICurrentTenantService via DI
        var serviceType = Type.GetType("MultiTenancy.Abstractions.ICurrentTenantService, MultiTenancy.EntityFrameworkCore");
        var tenantService = serviceType != null
            ? httpContext.RequestServices?.GetService(serviceType)
            : null;

        if (tenantService != null)
        {
            var tenantIdProp = tenantService.GetType().GetProperty("TenantId");
            var tenantIdValue = tenantIdProp?.GetValue(tenantService);
            if (tenantIdValue != null) return tenantIdValue.ToString();
        }

        // Fallback: read from JWT claims
        var tenantClaim = httpContext.User?.FindFirst(TenantClaimType);
        return tenantClaim?.Value;
    }
}
