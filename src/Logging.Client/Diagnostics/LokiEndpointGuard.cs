using System.Net;
using Logging.Client.Configuration;

namespace Logging.Client.Diagnostics;

/// <summary>
/// Startup guard that refuses to register the direct Serilog Loki sink when its configured
/// host cannot be resolved.
///
/// The Grafana Loki sink is a buffering sink: every event it cannot deliver stays on the
/// heap. Pointed at a hostname that does not exist (the in-cluster
/// <c>loki.monitoring.svc.cluster.local</c> default is NXDOMAIN in production), delivery can
/// never succeed, so the buffer only ever grows and the pod is eventually OOMKilled.
/// <see cref="LoggingOptions.LokiQueueLimit"/> bounds that growth — it does not notice that
/// the sink is dead. This guard notices, shouts, and falls back to the console sink, whose
/// stdout is scraped by Promtail and reaches the real Loki anyway.
///
/// Non-negotiable properties: it never throws and it never hangs startup. A logging package
/// must not be able to stop a service from booting, so every failure mode — NXDOMAIN,
/// malformed URL, DNS timeout, anything unexpected — resolves to "fall back to console".
/// </summary>
internal static class LokiEndpointGuard
{
    /// <summary>
    /// Hard upper bound on how long the startup DNS probe may delay boot. Cheap enough to run
    /// inline; short enough that a black-holed resolver costs seconds, not minutes.
    /// </summary>
    internal static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(2);

    /// <summary>
    /// The production host resolver: a DNS lookup bounded by <see cref="ResolveTimeout"/>.
    /// Swapped for a fake in tests so the decision logic is verifiable without a network.
    /// </summary>
    internal static readonly Func<string, bool> DnsHostResolver = host => HostResolves(host, ResolveTimeout);

    /// <summary>
    /// Decides which sink should actually be registered.
    ///
    /// Returns <see cref="LoggingOptions.SinkType"/> unchanged for every sink type except
    /// <see cref="LogSinkType.Loki"/>. For Loki, the configured URL must be well formed and
    /// its host must resolve; otherwise a warning is emitted through <paramref name="warn"/>
    /// and <see cref="LogSinkType.Console"/> is returned.
    /// </summary>
    /// <param name="options">The logging options carrying the requested sink and Loki URL.</param>
    /// <param name="hostResolver">Predicate that reports whether a host resolves.</param>
    /// <param name="warn">Sink for the loud startup warning (stderr in production).</param>
    /// <returns>The sink type that should be registered.</returns>
    internal static LogSinkType ResolveEffectiveSinkType(
        LoggingOptions options,
        Func<string, bool> hostResolver,
        Action<string> warn)
    {
        if (options.SinkType != LogSinkType.Loki)
            return options.SinkType;

        var host = TryGetHost(options.LokiUrl);
        if (host is null)
        {
            warn(FormatWarning($"'{options.LokiUrl}' is not a valid absolute URL"));
            return LogSinkType.Console;
        }

        if (SafeResolve(hostResolver, host))
            return LogSinkType.Loki;

        warn(FormatWarning($"host '{host}' does not resolve"));
        return LogSinkType.Console;
    }

    /// <summary>
    /// Extracts the host component of a Loki URL, or <c>null</c> when the value is empty,
    /// relative, or otherwise unparseable.
    /// </summary>
    internal static string? TryGetHost(string lokiUrl)
    {
        if (string.IsNullOrWhiteSpace(lokiUrl))
            return null;

        if (!Uri.TryCreate(lokiUrl, UriKind.Absolute, out var uri))
            return null;

        return string.IsNullOrEmpty(uri.Host) ? null : uri.Host;
    }

    /// <summary>
    /// Performs the real DNS lookup. IP literals short-circuit to <c>true</c> (nothing to
    /// resolve, and a reverse lookup would be both pointless and slow). Any failure —
    /// NXDOMAIN, timeout, no resolver configured — is reported as "does not resolve".
    /// </summary>
    internal static bool HostResolves(string host, TimeSpan timeout)
    {
        if (IPAddress.TryParse(host, out _))
            return true;

        try
        {
            var lookup = Dns.GetHostAddressesAsync(host);

            // WaitAsync is what actually bounds startup: getaddrinfo is not reliably
            // cancellable, so we stop WAITING on it rather than trying to cancel it. The
            // orphaned task's exception is observed below so it cannot surface later.
            lookup.ContinueWith(
                static t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            var addresses = lookup.WaitAsync(timeout).GetAwaiter().GetResult();
            return addresses.Length > 0;
        }
        catch
        {
            // Deliberately swallow everything: an unresolvable Loki host is a fallback
            // condition, never a startup failure.
            return false;
        }
    }

    /// <summary>
    /// Writes the startup warning to stderr. Serilog is not yet configured at guard time, so
    /// this cannot go through <c>Log.Logger</c> — and stderr is scraped by Promtail anyway.
    /// </summary>
    internal static void WriteStartupWarning(string message) => Console.Error.WriteLine(message);

    /// <summary>
    /// Formats the warning banner. Deliberately loud and self-explaining: the failure it
    /// describes is otherwise invisible until a pod is OOMKilled hours later.
    /// </summary>
    private static string FormatWarning(string reason) =>
        "[Logging.Client] *** LOKI SINK DISABLED *** SinkType=Loki was requested but " +
        reason + ". The Loki sink buffers undelivered events in memory, so registering it " +
        "against an unreachable endpoint leaks the heap until the process is OOMKilled. " +
        "Falling back to the Console sink — Promtail ships stdout to Loki, so no logs are " +
        "lost. Fix Logging:LokiUrl or leave Logging:SinkType at Console.";

    /// <summary>
    /// Runs the supplied resolver defensively; a caller-supplied resolver that throws must
    /// not take the process down.
    /// </summary>
    private static bool SafeResolve(Func<string, bool> hostResolver, string host)
    {
        try
        {
            return hostResolver(host);
        }
        catch
        {
            return false;
        }
    }
}
