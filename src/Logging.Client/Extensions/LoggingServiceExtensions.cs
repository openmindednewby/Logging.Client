using Logging.Client.Configuration;
using Logging.Client.Enrichers;
using Logging.Client.Masking;
using Logging.Client.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Grafana.Loki;

namespace Logging.Client.Extensions;

/// <summary>
/// Extension methods for configuring structured logging with Serilog, Grafana Loki,
/// PII masking, and contextual enrichers.
/// </summary>
public static class LoggingServiceExtensions
{
    /// <summary>
    /// Adds structured logging to the application with Serilog, enrichers, and the configured sink.
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

        Log.Logger = configuration.CreateLogger();
        builder.Host.UseSerilog();

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
                configuration.WriteTo.GrafanaLoki(
                    options.LokiUrl,
                    labels: new[]
                    {
                        new LokiLabel { Key = "ServiceName", Value = options.ServiceName },
                        new LokiLabel { Key = "Environment", Value = environment },
                    },
                    propertiesAsLabels: new[] { "TenantId", "Level" });
                break;

            case LogSinkType.Console:
                // Console sink already added above; no extra sink needed
                break;

            default:
                // Future sink types will be added here
                break;
        }
    }
}
