### Added

- Gateway, SignalR hub: `PlatformHub` on the path `/hubs/platform` (setting `Gateway:HubPath`, validated at startup, must start with a slash) with the `Ping` method, which asks the Back through `IBackClient` and returns its time with its offset. Detailed errors are off: a front never receives an internal message. The hub is not authenticated until 0.10.0. Until the link to the Back exists (`feature/back-gateway-link`), a temporary `UnavailableBackClient` makes `Ping` fail cleanly.
- Gateway tests (`BoardGamesPlatform.Gateway.Tests`): the Gateway started in process with a fake Back client, called by a SignalR client (Ping, offset of the time, failing Back without internal message, default client, hub path setting, `/health`).
- `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.AspNetCore.SignalR.Client` 10.0.14, for the Gateway tests only.
