### Added

- End-to-end tests (`BoardGamesPlatform.EndToEnd.Tests`, test ports 17500-17599): a headless SignalR front calls the real Gateway, which calls the real Back, which calls the real Very Back, started in one process (settings in memory, start in dependency order, stop in the reverse order, each stop bounded and every failure collected). The front reaches the Gateway through the in-memory test server by long polling, not through a socket: only the hops Gateway to Back to Very Back are real TCP connections. With a fake Ping procedure (CI): the front receives exactly the fake time with its non-local offset; a failing procedure and a stopped Very Back give the generic SignalR error only, and the Gateway stays usable. With the real `SqlPingProcedure`: the same Ping on the database named by `BOARDGAMESPLATFORM_TEST_DATABASE`, skipped without it.
- TestKit, `TestDatabase`: the single guard of the tests that use a real SQL Server (the variable, the skip reason, and the refusal of any database whose real name does not start with `TEST_` or `DEV_`), with unit tests of the name rule.

### Fixed

- The Very Back Ping test on a real SQL Server now refuses a database that is not a `TEST_` or `DEV_` one, like the other SQL tests (it never checked the name).
