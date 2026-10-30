### Added

- Very Back: listener of the Back on plain TCP (development only), configured by the `VeryBack` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7822), answering the `Ping` operation with the time of SQL Server; a failing connection is closed without stopping the others.
- Very Back: `ConnectionStrings:Main` (empty in `appsettings.json`, set by user secrets or an environment variable); the host refuses to start without it, with a string the SQL client cannot read (the error, which may quote a piece of the string, is not repeated), or with an invalid address or port.
- Very Back: call of `ServerCore.SP_ServerCore_Server_Ping`, whose JSON result is read strictly (one `ServerTime` property, with an explicit offset).
- Very Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Very Back tests (`BoardGamesPlatform.VeryBack.Tests`): settings validation, reading of the Ping result, the host setup, the exit code, and the listener on loopback sockets with fake procedures and a fake listener.
