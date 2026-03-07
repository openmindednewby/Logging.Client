using Microsoft.AspNetCore.Http;
using Serilog.Core;
using Serilog.Events;

namespace Logging.Client.Enrichers;

/// <summary>
/// Enriches log events with the current user ID from the JWT "sub" claim.
/// </summary>
public class UserContextEnricher : ILogEventEnricher
{
    /// <summary>
    /// The property name used for user ID in log events.
    /// </summary>
    public const string PropertyName = "UserId";

    private const string SubjectClaimType = "sub";

    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserContextEnricher"/> class.
    /// </summary>
    public UserContextEnricher(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Adds the UserId property to the log event.
    /// </summary>
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(SubjectClaimType)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var property = propertyFactory.CreateProperty(PropertyName, userId);
        logEvent.AddPropertyIfAbsent(property);
    }
}
