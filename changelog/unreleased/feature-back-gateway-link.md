### Added

- Back: listener of the Gateway on plain TCP (development only), configured by the `Back` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7821), answering the `Ping` operation with the time the Very Back gives, offset included; one client of the Very Back, shared by every connection, configured by the `VeryBack` section (`Host`, `Port` 7822, `ConnectTimeout`). The host refuses to start on an invalid value, before it listens.
- Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Back tests (`BoardGamesPlatform.Back.Tests`): settings validation, the host setup, and the listener of the Gateway on loopback sockets with a fake Very Back.

### Changed

- Observability: `HostExitCode` gives the exit code of any executable once its host has run (moved from the Very Back, which uses it too).
