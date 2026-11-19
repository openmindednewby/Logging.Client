using Logging.Client.Configuration;
using Logging.Client.Context;
using Logging.Client.Diagnostics;
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

/// <summary>Extension methods for structured logging with Serilog, Loki, PII masking and Sentry.</summary>
public static class LoggingServiceExtensions
{
    /// <summary>Adds structured logging to the application with Serilog, enrichers, and the configured sink.</summary>
    /// <param name="builder">The web application builder.</param>
    /// <param name="configure">Action to configure logging options.</param>
    public static WebApplicationBuilder AddStructuredLogging(
        this WebApplicationBuilder builder,
        Action<LoggingOptions> configure)
    {
        var options = new LoggingOptions();
        configure(options);

        BindSinkType(builder.Configuration, options);
        BindLokiUrl(builder.Configuration, options);
        ConsoleSink.BindFormat(builder.Configuration, options);
        MinimumLevels.Bind(builder.Configuration, options);

        BindLokiQueueLimit(builder.Configuration, options);

        options.SinkType = LokiEndpointGuard.ResolveEffectiveSinkType(
            options,
            LokiEndpointGuard.DnsHostResolver,
            LokiEndpointGuard.WriteStartupWarning);

        var sentrySection = builder.Configuration.GetSection("Sentry");
        BindSentryConfiguration(sentrySection, options);

        var environment = builder.Environment.EnvironmentName;

        var configuration = CreateLoggerConfiguration(options, environment);

        Log.Logger = configuration.CreateLogger();
        builder.Host.UseSerilog();

        ConfigureSentrySdk(builder, options, environment);

        builder.Services.AddHttpContextAccessor();

        return builder;
    }

    internal static LoggerConfiguration CreateLoggerConfiguration(
        LoggingOptions options,
        string environment)
    {
        var configuration = MinimumLevels.Apply(new LoggerConfiguration(), options)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("ServiceName", options.ServiceName)
            .Enrich.WithProperty("Environment", environment)
            .Enrich.With<CorrelationIdEnricher>()
            .Enrich.With<TraceContextEnricher>();

        if (options.EnablePiiMasking)
            configuration.Enrich.With<PiiMaskingEnricher>();

        ConsoleSink.Configure(configuration, options, environment);

        ConfigureSink(configuration, options, environment);

        ConfigureSentrySink(configuration, options);

        return configuration;
    }

    internal static bool WritesToConsole(LoggingOptions options) =>
        options.SinkType != LogSinkType.None;

    internal static bool IsSentryActive(LoggingOptions options) =>
        options.SentryEnabled && !string.IsNullOrEmpty(options.SentryDsn);

    /// <summary>Adds the correlation ID middleware to the application pipeline.</summary>
    public static IApplicationBuilder UseCorrelationIdMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }

    /// <summary>Adds the Sentry user context middleware to the application pipeline.</summary>
    public static IApplicationBuilder UseSentryUserContext(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SentryUserContextMiddleware>();
    }

    internal static void ConfigureSink(
        LoggerConfiguration configuration,
        LoggingOptions options,
        string environment)
    {
        switch (options.SinkType)
        {
            case LogSinkType.Loki:
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
            case LogSinkType.None:
                break;

            default:
                break;
        }
    }

    internal static void ConfigureSentrySink(
        LoggerConfiguration configuration,
        LoggingOptions options)
    {
        if (!IsSentryActive(options)) return;

        configuration.WriteTo.Sentry(o =>
        {
            o.Dsn = options.SentryDsn;
            o.Environment = options.SentryEnvironment;
            o.MinimumEventLevel = options.SentryMinimumLevel;
            o.SendDefaultPii = false;
            o.Release = options.ServiceName;
        });
    }

    internal static void ConfigureSentrySdk(
        WebApplicationBuilder builder,
        LoggingOptions options,
        string environment)
    {
        if (!IsSentryActive(options)) return;

        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = options.SentryDsn;
            o.Environment = options.SentryEnvironment ?? environment;
            o.Release = options.ServiceName;
            o.SendDefaultPii = false;
            o.TracesSampleRate = options.SentryTracesSampleRate;
            o.AutoSessionTracking = true;

            o.SetBeforeSend((sentryEvent, _) =>
            {
                var correlationId = CorrelationIdContext.Current;
                if (!string.IsNullOrEmpty(correlationId))
                    sentryEvent.SetTag("correlationId", correlationId);

                return sentryEvent;
            });
        });
    }

    internal static void BindSentryConfiguration(
        IConfigurationSection sentrySection,
        LoggingOptions options)
    {
        if (!sentrySection.Exists()) return;

        var enabled = sentrySection["Enabled"];
        if (!string.IsNullOrEmpty(enabled) && bool.TryParse(enabled, out var isEnabled))
            options.SentryEnabled = isEnabled;

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

    internal static void BindSinkType(
        IConfiguration configuration,
        LoggingOptions options)
    {
        var value = configuration["Logging:SinkType"];
        if (!string.IsNullOrWhiteSpace(value) && Enum.TryParse<LogSinkType>(value, true, out var parsed)
            && Enum.IsDefined(parsed))
            options.SinkType = parsed;
    }

    internal static void BindLokiUrl(
        IConfiguration configuration,
        LoggingOptions options)
    {
        var value = configuration["Logging:LokiUrl"];
        if (!string.IsNullOrWhiteSpace(value))
            options.LokiUrl = value;
    }

    internal static void BindLokiQueueLimit(
        IConfiguration configuration,
        LoggingOptions options)
    {
        var value = configuration["Logging:LokiQueueLimit"];
        if (!string.IsNullOrEmpty(value) && int.TryParse(value, out var parsed) && parsed > 0)
            options.LokiQueueLimit = parsed;
    }
}
