# Observability

Every executable of the platform can send its **logs, traces and metrics** to an [OpenTelemetry](https://opentelemetry.io) collector.
Nothing is sent until a collector is configured: without it, each executable keeps its usual local logs.

## Executables concerned

| Executable | Logs | Traces | Metrics |
|------------|:----:|:------:|:-------:|
| `SocialGamesPlatform.Back` | ✅ | ✅ (platform sources, outgoing HTTP) | ✅ (.NET runtime, outgoing HTTP, platform meters) |
| `SocialGamesPlatform.VeryBack` | ✅ | ✅ (platform sources, outgoing HTTP) | ✅ (.NET runtime, outgoing HTTP, platform meters) |
| `SocialGamesPlatform.Gateway` | ✅ | ✅ (+ incoming HTTP requests, including the SignalR connection requests) | ✅ (+ ASP.NET Core) |
| `SocialGamesPlatform.Web` (server part) | ✅ | ✅ (+ incoming HTTP requests) | ✅ (+ ASP.NET Core) |

The WebAssembly part of the website runs in the players' browsers: it does not send telemetry directly, since that would expose the collector to the Internet.

## Settings

Each executable reads the `Observability` section of its `appsettings.json`:

```json
"Observability": {
  "OtlpEndpoint": "http://collector.lan:4317",
  "OtlpProtocol": "Grpc",
  "ServiceName": "",
  "IncludeScopes": false
}
```

| Setting | Values | Meaning |
|---------|--------|---------|
| `OtlpEndpoint` | Empty, or an absolute `http` or `https` address | Address of the collector. **Empty: nothing is sent.** Any other value (for example `localhost:4317` without a scheme) stops the executable at startup with an explicit error. |
| `OtlpProtocol` | `Grpc` (default) or `HttpProtobuf` | Protocol used to reach the collector. With `HttpProtobuf`, `v1/logs`, `v1/traces` and `v1/metrics` are appended to the address, which may itself contain a path (for example `http://proxy.lan/otel`). Any other text stops the executable at startup. |
| `ServiceName` | Empty, or a name | Name of the executable in the collector. Empty: the project name, for example `SocialGamesPlatform.Back`. |
| `IncludeScopes` | `false` (default) or `true` | Also sends the logging scopes, for example the request path and the connection of ASP.NET Core. |

The same settings can be given as environment variables, which is convenient for a Windows service, a systemd unit or a container:

```bash
Observability__OtlpEndpoint=http://collector.lan:4318
Observability__OtlpProtocol=HttpProtobuf
```

## What describes each executable

Every log entry, trace and metric carries:

| Attribute | Value |
|-----------|-------|
| `service.namespace` | `SocialGamesPlatform` |
| `service.name` | The `ServiceName` setting, or the project name |
| `service.version` | The version of the executable (it may end with `+` and the commit identifier) |
| `service.instance.id` | A unique identifier, new at each start of the executable |
| `host.name` | The machine name |
| `deployment.environment.name` | The host environment: `Development`, `Staging`, `Production`… |

## Data sent

- **Logs**: the message, its values as attributes, the exception when there is one. The `Logging:LogLevel` filters of the configuration also apply to the exported logs.
- **Traces**: the incoming and outgoing HTTP requests, including their path.
- **Metrics**: counters and durations only.

The code must never log personal data (e-mail addresses, passwords, tokens). The exported data is not authenticated by the collector and travels **in clear text** with an `http://` address: keep the collector in the servers zone, or use an `https://` address.

## Collector unreachable

The executables never stop nor slow down because of the collector: the telemetry is sent in the background, and what cannot be sent is dropped. When an executable stops, it waits for the last export for up to about ten seconds.

## Firewall

| Flow | Port | Protocol |
|------|------|----------|
| Every executable → collector | 4317 | OTLP over gRPC |
| Every executable → collector | 4318 | OTLP over HTTP |

Only the port of the chosen protocol needs to be open. The collector should stay in the servers zone: no executable needs to reach it from the Internet.
