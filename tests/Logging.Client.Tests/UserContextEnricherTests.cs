using System.Security.Claims;
using Logging.Client.Enrichers;
using Microsoft.AspNetCore.Http;
using Serilog.Events;
using Serilog.Parsing;

namespace Logging.Client.Tests;

public class UserContextEnricherTests
{
    private static readonly MessageTemplate EmptyTemplate =
        new MessageTemplateParser().Parse("");

    [Fact]
    public void Enrich_WithSubClaim_AddsUserIdProperty()
    {
        // Arrange
        const string userId = "user-abc-123";
        var httpContext = CreateHttpContextWithClaims(("sub", userId));
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().ContainKey("UserId");
        logEvent.Properties["UserId"].ToString().Should().Contain(userId);
    }

    [Fact]
    public void Enrich_NoSubClaim_DoesNotAddProperty()
    {
        // Arrange
        var httpContext = CreateHttpContextWithClaims(("tenant_id", "t-123"));
        var enricher = CreateEnricher(httpContext);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
    }

    [Fact]
    public void Enrich_NoHttpContext_DoesNotAddProperty()
    {
        // Arrange
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns((HttpContext?)null);
        var enricher = new UserContextEnricher(accessor.Object);
        var logEvent = CreateLogEvent();

        // Act
        enricher.Enrich(logEvent, new TestPropertyFactory());

        // Assert
        logEvent.Properties.Should().NotContainKey("UserId");
    }

    [Fact]
    public void PropertyName_IsCorrect()
    {
        UserContextEnricher.PropertyName.Should().Be("UserId");
    }

    private static UserContextEnricher CreateEnricher(HttpContext httpContext)
    {
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(a => a.HttpContext).Returns(httpContext);
        return new UserContextEnricher(accessor.Object);
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

    private sealed class TestPropertyFactory : ILogEventPropertyFactory
    {
        public LogEventProperty CreateProperty(string name, object? value, bool destructureObjects = false)
        {
            return new LogEventProperty(name, new ScalarValue(value));
        }
    }
}
