using Logging.Client.Configuration;
using Logging.Client.Diagnostics;

namespace Logging.Client.Tests;

/// <summary>Decision-logic tests for the Loki startup guard.</summary>
public class LokiEndpointGuardTests
{
    private static readonly Func<string, bool> AlwaysResolves = _ => true;
    private static readonly Func<string, bool> NeverResolves = _ => false;

    [Fact]
    public void ResolveEffectiveSinkType_LokiHostUnresolvable_FallsBackToConsole()
    {
        var options = new LoggingOptions
        {
            SinkType = LogSinkType.Loki,
            LokiUrl = "http://loki.monitoring.svc.cluster.local:3100",
        };
        var warnings = new List<string>();

        var effective = LokiEndpointGuard.ResolveEffectiveSinkType(options, NeverResolves, warnings.Add);

        effective.Should().Be(LogSinkType.Console);
        warnings.Should().ContainSingle();
        warnings[0].Should().Contain("LOKI SINK DISABLED");
        warnings[0].Should().Contain("loki.monitoring.svc.cluster.local");
    }

    [Fact]
    public void ResolveEffectiveSinkType_LokiHostResolves_KeepsLokiAndDoesNotWarn()
    {
        var options = new LoggingOptions
        {
            SinkType = LogSinkType.Loki,
            LokiUrl = "http://loki.example.internal:3100",
        };
        var warnings = new List<string>();

        var effective = LokiEndpointGuard.ResolveEffectiveSinkType(options, AlwaysResolves, warnings.Add);

        effective.Should().Be(LogSinkType.Loki);
        warnings.Should().BeEmpty();
    }

    [Fact]
    public void ResolveEffectiveSinkType_ConsoleSinkType_NeverProbesDns()
    {
        var options = new LoggingOptions { SinkType = LogSinkType.Console };
        var probedHosts = new List<string>();
        var warnings = new List<string>();

        var effective = LokiEndpointGuard.ResolveEffectiveSinkType(
            options,
            host =>
            {
                probedHosts.Add(host);
                return true;
            },
            warnings.Add);

        effective.Should().Be(LogSinkType.Console);
        probedHosts.Should().BeEmpty();
        warnings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-url")]
    [InlineData("loki.monitoring.svc.cluster.local:3100")]
    public void ResolveEffectiveSinkType_MalformedLokiUrl_FallsBackToConsole(string lokiUrl)
    {
        var options = new LoggingOptions { SinkType = LogSinkType.Loki, LokiUrl = lokiUrl };
        var warnings = new List<string>();

        var effective = LokiEndpointGuard.ResolveEffectiveSinkType(options, AlwaysResolves, warnings.Add);

        effective.Should().Be(LogSinkType.Console);
        warnings.Should().ContainSingle();
    }

    [Fact]
    public void ResolveEffectiveSinkType_ResolverThrows_FallsBackToConsoleWithoutThrowing()
    {
        var options = new LoggingOptions { SinkType = LogSinkType.Loki, LokiUrl = "http://loki:3100" };
        var warnings = new List<string>();
        Func<string, bool> throwingResolver = _ => throw new InvalidOperationException("resolver exploded");

        var act = () => LokiEndpointGuard.ResolveEffectiveSinkType(options, throwingResolver, warnings.Add);

        act.Should().NotThrow().Which.Should().Be(LogSinkType.Console);
    }

    [Theory]
    [InlineData("http://loki.monitoring.svc.cluster.local:3100", "loki.monitoring.svc.cluster.local")]
    [InlineData("https://logs.example.com/loki/api/v1/push", "logs.example.com")]
    [InlineData("http://10.0.0.2:31300", "10.0.0.2")]
    public void TryGetHost_AbsoluteUrl_ReturnsHost(string lokiUrl, string expectedHost)
    {
        LokiEndpointGuard.TryGetHost(lokiUrl).Should().Be(expectedHost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("loki:3100")]
    [InlineData("/loki/api/v1/push")]
    public void TryGetHost_NotAnAbsoluteUrl_ReturnsNull(string lokiUrl)
    {
        LokiEndpointGuard.TryGetHost(lokiUrl).Should().BeNull();
    }

    [Fact]
    public void HostResolves_IpLiteral_ShortCircuitsWithoutDnsLookup()
    {
        LokiEndpointGuard.HostResolves("10.0.0.2", TimeSpan.Zero).Should().BeTrue();
    }

    [Fact]
    public void ResolveTimeout_IsBoundedSoStartupCannotHang()
    {
        LokiEndpointGuard.ResolveTimeout.Should().BeGreaterThan(TimeSpan.Zero);
        LokiEndpointGuard.ResolveTimeout.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(5));
    }
}
