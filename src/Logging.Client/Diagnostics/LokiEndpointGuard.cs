using System.Net;
using Logging.Client.Configuration;

namespace Logging.Client.Diagnostics;

internal static class LokiEndpointGuard
{
    internal static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(2);

    internal static readonly Func<string, bool> DnsHostResolver = host => HostResolves(host, ResolveTimeout);

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

    internal static string? TryGetHost(string lokiUrl)
    {
        if (string.IsNullOrWhiteSpace(lokiUrl))
            return null;

        if (!Uri.TryCreate(lokiUrl, UriKind.Absolute, out var uri))
            return null;

        return string.IsNullOrEmpty(uri.Host) ? null : uri.Host;
    }

    internal static bool HostResolves(string host, TimeSpan timeout)
    {
        if (IPAddress.TryParse(host, out _))
            return true;

        try
        {
            var lookup = Dns.GetHostAddressesAsync(host);

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
            return false;
        }
    }

    internal static void WriteStartupWarning(string message) => Console.Error.WriteLine(message);

    private static string FormatWarning(string reason) =>
        "[Logging.Client] *** LOKI SINK DISABLED *** SinkType=Loki was requested but " +
        reason + ". The Loki sink buffers undelivered events in memory, so registering it " +
        "against an unreachable endpoint leaks the heap until the process is OOMKilled. " +
        "Falling back to the Console sink — Promtail ships stdout to Loki, so no logs are " +
        "lost. Fix Logging:LokiUrl or leave Logging:SinkType at Console.";

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
