### Added

- Very Back: its SQL sessions are named `BoardGamesPlatform.VeryBack` (`program_name` on SQL Server) when the connection string sets no `Application Name`; a value set by the administrator is kept.
- Very Back: documentation of the connections to SQL Server (pool, `Max Pool Size`, `Load Balance Timeout` / `Connection Lifetime`, `Pooling=False`).

### Fixed

- Very Back: no SQL session stays open on SQL Server after a stop or a redeployment: the pool of its connection string is cleared once the listener has stopped.
