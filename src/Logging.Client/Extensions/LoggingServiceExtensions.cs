using Logging.Client.Configuration;
using Logging.Client.Context;
using Logging.Client.Enrichers;
using Logging.Client.Masking;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sentry;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace Logging.Client.Extensions;

/// <summary>
/// Extension methods for configuring structured logging with Serilog, Grafana Loki,
/// PII masking, Sentry error monitoring, and contextual enrichers.
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// Adds structured logging to the application with Serilog, enrichers, and the configured sink.
    /// When a Sentry DSN is provided (via config or environment variable), the full Sentry SDK is
    /// initialized with performance monitoring (tracing) and the Serilog sink for error capture.
    /// </summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configure">Action to configure logging options.</param>
    /// <returns>The builder for chaining.</returns>
    public static WebApplicationBuilder AddStructuredLogging(
        this WebApplicationBuilder builder,
        Action<LoggingOptions> configure)
    {
        var options = new LoggingOptions();
        configure(options);

        // Allow operators to tune the Loki sink queue depth from appsettings
        // without recompiling.
        BindLokiQueueLimit(builder.Configuration, options);

        // Bind Sentry config from appsettings if present
        var sentrySection = builder.Configuration.GetSection("Sentry");
        BindSentryConfiguration(sentrySection, options);

        var environment = builder.Environment.EnvironmentName;

        var configuration = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("ServiceName", options.ServiceName)
            .Enrich.WithProperty("Environment", environment)
            .Enrich.With<CorrelationIdEnricher>();

        // PII masking
        if (options.EnablePiiMasking)
            configuration.Destructure.With<PiiMaskingPolicy>();

        // Console sink (always enabled for local visibility)
        configuration.WriteTo.Console(outputTemplate: options.ConsoleTemplate);

        // Configure the primary sink based on SinkType
        ConfigureSink(configuration, options, environment);

        // Configure Sentry Serilog sink (runs alongside the primary sink, not instead of it)
        ConfigureSentrySink(configuration, options);

        Log.Logger = configuration.CreateLogger();
        builder.Host.UseSerilog();

        // Initialize full Sentry SDK for performance monitoring and breadcrumbs
        ConfigureSentrySdk(builder, options, environment);

        // Register HTTP context accessor for enrichers
        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    /// <summary>
    /// Adds the correlation ID middleware to the application pipeline.
    /// Should be called early in the middleware pipeline, before UseRequestLogging().
    /// </summary>
    public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>
    /// Adds the Sentry user context middleware to the application pipeline.
    /// Should be called after UseAuthentication() and UseAuthorization().
    /// </summary>
    public static IApplicationBuilder UseSentryUserContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SentryUserContextMiddleware>();
    }

    /// <summary>
    /// Configures the Serilog log sink based on the LogSinkType option.
    /// </summary>
    internal static void ConfigureSink(
        LoggerConfiguration configuration,
        LoggingOptions options,
        string environment)
    {
        switch (options.SinkType)
        {
            case LogSinkType.Loki:
                // queueLimit bounds the in-memory buffer so a stuck Loki endpoint
                // (DNS failure, rate limit, downtime) cannot leak the heap.
                configuration.WriteTo.GrafanaLoki(
                    options.LokiUrl,
                    labels: new[]
                    {
                        new LokiLabel { Key = "ServiceName", Value = options.ServiceName },
                        new LokiLabel { Key = "Environment", Value = environment },
                    },
                    propertiesAsLabels: new[] { "TenantId", "Level" },
                    queueLimit: options.LokiQueueLimit);
                break;

            case LogSinkType.Console:
                // Console sink already added above; no extra sink needed
                break;

            default:
                // Future sink types will be added here
                break;
        }
    }

    /// <summary>
    /// Configures the Sentry Serilog sink when a DSN is provided.
    /// Sentry only receives events at or above the configured minimum level (default: Error).
    /// GDPR: SendDefaultPii is always false; only opaque IDs are sent.
    /// </summary>
    internal static void ConfigureSentrySink(
        LoggerConfiguration configuration,
        LoggingOptions options)
    {
        if (string.IsNullOrEmpty(options.SentryDsn)) return;

        configuration.WriteTo.Sentry(o =>
        {
            o.Dsn = options.SentryDsn;
            o.Environment = options.SentryEnvironment;
            o.MinimumEventLevel = options.SentryMinimumLevel;
            o.SendDefaultPii = false;
            o.Release = options.ServiceName;
        });
    }

    /// <summary>
    /// Initializes the full Sentry SDK via <c>UseSentry()</c> on the web host builder.
    /// This enables performance monitoring (transaction tracing), automatic breadcrumb capture,
    /// and request/response context. The Serilog sink handles error-level events separately.
    /// No-op when <see cref="LoggingOptions.SentryDsn"/> is empty.
    /// </summary>
    internal static void ConfigureSentrySdk(
        WebApplicationBuilder builder,
        LoggingOptions options,
        string environment)
    {
        if (string.IsNullOrEmpty(options.SentryDsn)) return;

        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = options.SentryDsn;
            o.Environment = options.SentryEnvironment ?? environment;
            o.Release = options.ServiceName;
            o.SendDefaultPii = false;
            o.TracesSampleRate = options.SentryTracesSampleRate;
            o.AutoSessionTracking = true;

            // Attach correlation ID to every Sentry event as a tag
            o.SetBeforeSend((sentryEvent, _) =>
            {
                var correlationId = CorrelationIdContext.Current;
                if (!string.IsNullOrEmpty(correlationId))
                    sentryEvent.SetTag("correlationId", correlationId);

                return sentryEvent;
            });
        });
    }

    /// <summary>
    /// Binds Sentry configuration values from the <c>Sentry</c> configuration section.
    /// Reads Dsn, Environment, MinimumEventLevel, and TracesSampleRate.
    /// </summary>
    internal static void BindSentryConfiguration(
        IConfigurationSection sentrySection,
        LoggingOptions options)
    {
        if (!sentrySection.Exists()) return;

        var dsn = sentrySection["Dsn"];
        if (!string.IsNullOrEmpty(dsn)) options.SentryDsn = dsn;

        var env = sentrySection["Environment"];
        if (!string.IsNullOrEmpty(env)) options.SentryEnvironment = env;

        var minLevel = sentrySection["MinimumEventLevel"];
        if (!string.IsNullOrEmpty(minLevel) && Enum.TryParse<LogEventLevel>(minLevel, true, out var parsed))
            options.SentryMinimumLevel = parsed;

        var tracesSampleRate = sentrySection["TracesSampleRate"];
        if (!string.IsNullOrEmpty(tracesSampleRate) && double.TryParse(tracesSampleRate, out var rate))
            options.SentryTracesSampleRate = Math.Clamp(rate, 0.0, 1.0);
    }

    /// <summary>
    /// Reads <c>Logging:LokiQueueLimit</c> from configuration and applies it to
    /// the options when the value is a positive integer. Invalid or missing
    /// values leave the existing default in place.
    /// </summary>
    internal static void BindLokiQueueLimit(
        IConfiguration configuration,
        LoggingOptions options)
    {
        var value = configuration["Logging:LokiQueueLimit"];
        if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var parsed) && parsed > 0)
            options.LokiQueueLimit = parsed;
    }
}
