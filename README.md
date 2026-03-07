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

```bash
cd C:\desktopContents\projects\SaaS\NuGetPackages\Logging.Client
.\publish.ps1 -ApiKey XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX -Bump patch  # Bug fixes
.\publish.ps1 -ApiKey XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX -Bump minor  # New features
.\publish.ps1 -ApiKey XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX -Bump major  # Breaking changes
```
