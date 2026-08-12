# Logging.Client

[![NuGet](https://img.shields.io/nuget/v/Logging.Client.svg)](https://www.nuget.org/packages/Logging.Client)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

Shared structured logging configuration with Serilog, Grafana Loki sink, PII masking, correlation ID tracking, and tenant-aware enrichers for all SaaS services.

## Installation

```bash
dotnet add package Logging.Client
```

## Description

Shared structured logging configuration with Serilog, Grafana Loki sink, PII masking, correlation ID tracking, and tenant-aware enrichers for all SaaS services.

## Sink selection (read this before setting `SinkType`)

The **console sink is the default and the recommended sink everywhere.** Promtail runs on
both clusters and already ships container stdout to the real Loki (prod promtail →
`http://10.0.0.2:31300/loki/api/v1/push`, label `cluster: prod`), so the direct Serilog Loki
sink is redundant — it only adds a failure mode.

That failure mode is a memory leak, not an outage. The Grafana Loki sink buffers every event
it cannot deliver. Pointed at a host that does not resolve, delivery can never succeed, so the
buffer only grows until the pod is OOMKilled. `LokiQueueLimit` bounds how big that buffer gets;
it does not notice that the sink is dead.

Since **1.6.0** the package therefore:

- defaults `SinkType` to `Console`;
- runs a **startup guard** when `SinkType = Loki` — it resolves the configured Loki host in
  DNS (2 s hard bound, never throws, never blocks boot) and, if the host does not resolve or
  the URL is malformed, prints a loud `*** LOKI SINK DISABLED ***` banner to stderr and falls
  back to the console sink;
- binds `Logging:SinkType` and `Logging:LokiUrl` from configuration, so the opt-in below
  actually reaches the options.

```bash
# opt back into the direct sink (both required — the URL default is NXDOMAIN in prod)
Logging__SinkType=Loki
Logging__LokiUrl=http://loki.monitoring.svc.cluster.local:3100
```

Configuration wins over the values passed to `AddStructuredLogging(opts => ...)`.

## Documentation

See the [NuGet package page](https://www.nuget.org/packages/Logging.Client) for full documentation.

## Contributing

Contributions are welcome! Please open an issue or submit a pull request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Support

- Issues: [GitHub Issues](https://github.com/openmindednewby/Logging.Client/issues)
- Discussions: [GitHub Discussions](https://github.com/openmindednewby/Logging.Client/discussions)

## How to Publish

The API key is read automatically from `SaaS/.env.local` (`NUGET_API_KEY`) — never pass or
paste `-ApiKey`.

```powershell
cd C:\desktopContents\projects\SaaS\NuGetPackages\Logging.Client
.\publish.ps1 -Bump patch   # Bug fixes
.\publish.ps1 -Bump minor   # New features
.\publish.ps1 -Bump major   # Breaking changes
.\publish.ps1 -NoBump       # Publish the version already in Directory.Build.props
```
