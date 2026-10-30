### Added

- `VeryBackClient` in `BoardGamesPlatform.Platform.Infrastructure`: the client the Back uses to call the Very Back. It opens one shared connection on first use (within a connect timeout), opens a new one on the next call when the connection has closed, never retries a failed request, and offers the Ping operation.
- `VeryBackClientOptions`: the `VeryBack` settings (`Host`, `Port` 7822 by default, `ConnectTimeout` 5 seconds by default, at most 5 minutes), checked at startup so that the Back refuses to start on an invalid value.
- `BoardGamesPlatform.Platform.Infrastructure.Tests`: tests of the Very Back client against a fake Very Back on a loopback socket.
