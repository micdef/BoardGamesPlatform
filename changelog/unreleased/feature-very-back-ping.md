### Added

- Very Back: listener of the Back on plain TCP (development only), configured by the `VeryBack` section (`ListenAddress`, default `127.0.0.1`; `Port`, default 7822), answering the `Ping` operation with the time of SQL Server; a failing connection is closed without stopping the others.
- Very Back: `ConnectionStrings:Main` (empty in `appsettings.json`, set by user secrets or an environment variable); the host refuses to start without it, or with an invalid address or port.
- Very Back: call of `ServerCore.SP_ServerCore_Server_Ping`, whose JSON result is read strictly (one `ServerTime` property, with an explicit offset).
- Very Back tests (`BoardGamesPlatform.VeryBack.Tests`): settings validation, reading of the Ping result, and the listener on loopback sockets with a fake procedure.
