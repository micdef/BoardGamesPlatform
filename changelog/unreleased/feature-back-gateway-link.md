### Added

- Back: listener of the Gateway on plain TCP (development only), configured by the `Back` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7821), answering the `Ping` operation with the time the Very Back gives, offset included; one client of the Very Back, shared by every connection, configured by the `VeryBack` section (`Host`, `Port` 7822, `ConnectTimeout`). The host refuses to start on an invalid value, before it listens.
- Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Back tests (`BoardGamesPlatform.Back.Tests`): settings validation, the host setup, and the listener of the Gateway on loopback sockets with a fake Very Back.

### Changed

- Observability: `HostExitCode` gives the exit code of any executable once its host has run (moved from the Very Back, which uses it too).
- Platform.Infrastructure: `VeryBackClient.PingAsync` returns the time of the database server itself, so that no message type of the Very Back reaches the Back.
- Back and Very Back: the startup log is written once the listener is bound.
- Tests: the network tests use fixed ports of the block 17000-17999 (one block of 100 ports per test project, locked for its run, moved with `BOARDGAMESPLATFORM_TEST_PORT_BASE`), never a port handed out by the system; the Back and the Very Back are also started with their delivered configuration, to prove they listen on 7821 and 7822. The rule is written in the roadmap and in `test/README.md`.
