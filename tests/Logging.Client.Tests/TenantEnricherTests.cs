using System.Security.Claims;
using Logging.Client.Enrichers;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Serilog.Parsing;

namespace Logging.Client.Tests;

public class TenantEnricherTests
{
    private static readonly MessageTemplate EmptyTemplate =
        new MessageTemplateParser().Parse("");

    [Fact]
    public void Enrich_WithTenantClaim_AddsTenantIdProperty()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var httpContext = CreateHttpContextWithClaims(("tenant_id", tenantId));
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new LogEventPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("TenantId");
        logEvent.Properties["TenantId"].ToString().Should().Contain(tenantId);
    }

    [Fact]
    public void Enrich_NoHttpContext_DoesNotAddProperty()
    {
        // Arrange
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);

        var enricher = new TenantEnricher(accessor.Object);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new LogEventPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_NoTenantClaim_DoesNotAddProperty()
    {
        // Arrange
        var httpContext = CreateHttpContextWithClaims(("sub", "user-123"));
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new LogEventPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_NullUser_DoesNotAddProperty()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        // User is null by default on DefaultHttpContext unless claims are set
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new LogEventPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_DoesNotOverwriteExistingProperty()
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        var httpContext = CreateHttpContextWithClaims(("tenant_id", tenantId));
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Pre-add a TenantId property
        var existingProp = new LogEventProperty("TenantId", new ScalarValue("existing-tenant"));
        logEvent.AddPropertyIfAbsent(existingProp);

        // Act
        enricher.Enrich(logEvent, new LogEventPropertyFactory());

        // Assert
        logEvent.Properties["TenantId"].ToString().Should().Contain("existing-tenant");
    }

    [Fact]
    public void PropertyName_IsCorrect()
    {
        TenantEnricher.PropertyName.Should().Be("TenantId");
    }

    private static TenantEnricher CreateEnricher(HttpContext httpContext)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new TenantEnricher(accessor.Object);
    }

    private static HttpContext CreateHttpContextWithClaims(params (string type, string value)[] claims)
    {
        var httpContext = new DefaultHttpContext();
        var claimsList = claims.Select(c => new Claim(c.type, c.value)).ToList();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claimsList, "test"));
        return httpContext;
    }

    private static LogEvent CreateLogEvent()
    {
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            EmptyTemplate,
            Array.Empty<LogEventProperty>());
    }

    /// <summary>
    /// Simple property factory for test purposes.
    /// </summary>
    private sealed class LogEventPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
