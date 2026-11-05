# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.4.0] - 2026-11-05

Robust socket protocol, specified in `doc/dev/protocol.md`. Every connection opens with a version handshake. A failed request is answered with an error code, and the connection that every caller shares stays open. A cancellation crosses the tiers, down to the database call. A heartbeat detects a silent peer. Requests are bounded in time and connections in number. The links reconnect with back-off. Development only: the links are still plain TCP, and mutual TLS 1.3 comes in 0.5.0.

### Added

- Developer documentation, `doc/dev/protocol.md`: the specification of version 1 of the socket protocol (frames, handshake, control messages, envelopes, error codes and what closes a connection, cancellation, heartbeat, limits, reconnection, reserved log event ids).
- Socket protocol, heartbeat: the request client sends `{"control":"ping"}` every 10 seconds, and the request server answers `{"control":"pong"}`. The client closes the connection when it has received no frame at all for 30 seconds (its pending requests fail with a `TimeoutException` as the cause). The server ends the service of a connection on which it has received nothing for 45 seconds. Any frame counts. No read is ever canceled without closing the connection, and the write of a ping is bounded by the client timeout. A ping sent by the server is a protocol violation.
- `HeartbeatSettings`: the three timings, validated (both timeouts longer than the interval). The request client and the request server take them, with a clock (`TimeProvider`), so that the tests run on a fake clock.
- Socket protocol, error codes: a response holds either its body or an error, `{"error":{"code":"…"}}`, never both. The codes are short dotted names, never display texts and never internal details (`ErrorCodes`: `request.unknownOperation`, `request.invalid`, `internal`, `link.unavailable`). The caller receives a `RemoteErrorException` holding the code, and the connection stays open.
- Socket protocol, `LinkFailureException`: thrown by the client of a link when the peer cannot be reached or the connection is lost before the response, with the code `link.unavailable`. The cause is kept as the inner exception, for the logs of the tier.
- Links, request timeout: a request that gets no response within the request timeout fails with a `LinkFailureException` whose code is `link.timeout`, and it is canceled on the peer. The setting is `Back:RequestTimeout` in the Gateway (8 seconds by default) and `VeryBack:RequestTimeout` in the Back (5 seconds by default), greater than zero and at most 5 minutes, checked at startup. The timeouts decrease along the chain (website 10 s > Gateway 8 s > Back 5 s), so that each tier still answers a code before its caller gives up.
- Links, maximum of connections: a link server serves at most 8 connections at the same time. A new one beyond that is closed at once and logged as a warning (event id 9019).
- Socket protocol, control messages: frames on the reserved correlation identifier 0 carry control messages, JSON objects whose first property `control` names their kind. They are read strictly, and an unknown kind closes the connection.
- Socket protocol, handshake (`ProtocolHandshake`): every connection opens with a `hello` from the client (the range of protocol versions it speaks and its largest payload). The server answers `helloAck`, with the newest common version (1 for now) and its limit of requests handled at the same time, or `helloRefused`, with the code `protocol.versionUnsupported` or `protocol.limitsMismatch`, and closes. A request before the hello is a protocol violation. A server allows 5 seconds for the handshake. A client bounds it with its connect timeout and checks the answer again.
- Test kit: `TestRequestClients.ConnectAsync` opens a request client on a listening tier with the handshake, as a real client does.
- Links, reconnection with back-off: a lost connection is opened again by the next call, at once. When opening fails, the next attempt waits 0.5 second, doubled after each failure up to 30 seconds, spread at random by 20 % either way. Until then, calls fail at once with `link.unavailable` without touching the network (event id 9006 tells when the next attempt is due). The first connection that opens resets the delay. A failed request is never retried automatically. Done criterion of 0.4.0, proven by a test peer that goes down in the middle of a session and comes back on the same port: the request in flight fails with the code, the calls while it is down fail at once, and the first call once it is back succeeds.
- Request client, `IsOpen`: false from the very start of the close, so that the link client never picks a connection that is closing.
- Socket protocol, request cancellation: when the caller of a request gives up, the request client sends the control message `{"control":"cancel","correlationId":N}` once the request is written. The request server cancels the token of that request only, and answers nothing. A cancellation of a request that already ended, or never existed, is ignored. Through the tiers, a front that disconnects stops the work of its call in the Back and in the Very Back, down to the call to the database (end-to-end test).
- Socket protocol, error code `server.busy`: a request beyond the limit of requests in progress of the connection is answered at once with this code, without being started.
- Socket protocol, request client: it never has more requests in progress than the limit the server announced in the handshake. Extra calls wait for a place.
- Socket protocol, framing: property-based and fuzz tests of the frame codec (random round trips in random chunks, random bytes, truncation at every offset, oversized lengths), run with fixed seeds so that every failure can be replayed.

### Changed

- Roadmap: the done criterion of 0.4.0 is rewritten to what a test can prove (in-flight requests fail with a code, calls fail at once while the Very Back is down, the first call once it is back succeeds), the branches of 0.4.0 are listed as delivered, and the decisions on the protocol version, the error responses and the robustness of the protocol are recorded.
- Socket protocol, operation dispatcher: an unknown operation, a body that does not match its operation, or a failing handler is answered with an error code instead of closing the connection that every caller shares. A tier behind that cannot be reached is answered `link.unavailable`, any other failure `internal`. The details go to the log of the tier only (event ids 9021-9024). A malformed envelope still closes the connection. The dispatcher now takes the logger of the tier.
- Back: when the Very Back cannot be reached, a `Ping` of the Gateway is answered `link.unavailable`, and the connection of the Gateway stays open.
- Links: the link server runs the handshake on each accepted connection before serving it. The link client runs it after connecting, within the connect timeout. A refusal reaches the caller as a `LinkFailureException` that keeps the code of the refusal; any other failure of the handshake gives `link.unavailable`.
- Socket protocol, request server: it never stops reading while the connection lives, so that a cancellation is received even when every slot is busy (until now it stopped reading when its slots were full). A request whose identifier is still in progress, or a control message other than a cancellation, is a protocol violation. The slot of a request is given back just before its response is written, so that the next request of the client always finds it free.

### Fixed

- Socket protocol, operation dispatcher: a response larger than the link can carry, or that cannot be written, is answered `internal` and logged (event id 9025). It no longer closes the connection that every caller shares, which then failed them all with `link.unavailable`.
- Socket protocol, operation dispatcher: a code answered by a tier behind is no longer logged as a handler failure. It is logged as a warning (event id 9026), `server.busy` is passed on, and any other code becomes `internal`. A handler that fails with any exception once its request is canceled is no longer answered.
- Socket protocol, serializer: a request whose operation name or body the reader would refuse is refused before it is written, with an `ArgumentException` to the caller, instead of closing the shared connection. The errors of a malformed message no longer copy any text of the peer (property names, values), nor keep an inner exception that could, so that a peer cannot write fake lines or control characters into the logs.
- Socket protocol, request server: the failure of a response write is recorded while the write lock is still held, so that the end of the service always reports it. After a client closes its sending side, the requests still running are canceled once the idle timeout has passed, instead of holding the connection for ever.
- Socket protocol, request client: a failure to close the connection met while sending a cancellation is thrown by `DisposeAsync` instead of being lost. The reason of a close is exposed (`CloseReason`) and logged with the loss of the connection (event id 9002).
- Links: the request timeout now covers the whole call, the wait for the connection included, so that the timeouts of the tiers add up. The back-off counts on a monotonic clock, never waits more than 30 seconds, the random spread included, and counts an opening cut by the request timeout as a failure. Requests canceled at the end of the drain after a client closed are reported as a failure of the service, logged by the link server. The client never keeps more than 64 requests in progress, whatever the server announces. A connection that cannot be closed is logged (event id 9016) instead of stopping the accept loop, and a failure to close after a failed handshake no longer hides the failure of the handshake.

## [0.3.0] - 2026-10-30

Walking skeleton: a button on a Blazor page shows the time of SQL Server, obtained through the five tiers, and an end-to-end test proves it. Development only: the links between the tiers are plain TCP and nothing is authenticated yet.

### Added

- Socket protocol, messages: JSON envelopes (`{"operation":…,"body":…}` for a request, `{"body":…}` for a response), read strictly (unknown members, missing parts, null bodies and deep nesting are protocol violations); an operation dispatcher that routes each request to the handler of its operation; typed requests on the request client.
- Ping contracts: the `Ping` operation and its request and response bodies in the Very Back and Back contracts, and the platform hub path and `Ping` method in the Gateway contract.
- Database: the `ServerCore` schema, the `ServerCore.SP_ServerCore_Server_Ping` procedure (the server time as JSON, with its offset) and the `ApplicationRole` role, allowed to execute the procedures of `ServerCore` only; accepted static analysis warnings documented in `StaticCodeAnalysis.SuppressMessages.xml`.
- SQL tests (`BoardGamesPlatform.Database.Tests`): conventions of the SQL files (declared in the project, headers, procedure sections and naming), run everywhere; the Ping procedure and the permission matrix of the application role, on a `TEST_` or `DEV_` database, skipped without one.
- `Microsoft.Data.SqlClient` 7.1.1, with its transitive `System.Security.Cryptography.Pkcs` pinned to 10.0.14 (CVE-2026-84008, CVE-2026-70162).
- Socket protocol, request/response correlation: a request client that sends several requests on one connection and pairs each response with its request (identifiers 1, 2, 3…; 0 is reserved), and a request server that answers them, several at a time up to a limit per connection. Any close of the connection fails the pending requests; a response to an unknown identifier closes the link; a canceled request never interrupts a frame being written.
- Socket protocol, transport: interfaces of a connection (a byte stream), a listener and a connector, so that TLS can wrap the stream in 0.5.0; their plain TCP implementation (development only), with the Nagle delay disabled; a listener on port 0 gets its port from the system. First loopback integration tests.
- Database tooling (`tools/database/Deploy-TestDatabase.ps1`): builds the SQL project into a DACPAC and deploys it to a throwaway database on the local SQL Server, dropping and recreating it; refuses any database name that does not start with `DEV_` or `TEST_` (upper case), before any build or connection.
- CI: a separate job builds the SQL project, in its original format, with the MSBuild of Visual Studio of the runner image, and publishes the DACPAC as the `database-dacpac` artifact.
- Socket protocol, framing: frames with a 12-byte big-endian header (payload length, correlation identifier) and an opaque payload, read whole whatever the way the stream delivers the bytes; a frame larger than the maximum (1 MiB by default, 16 MiB at most) is refused before its payload is read; a stream ending inside a frame is refused; a frame is written in a single call. After any failure or cancellation the connection is closed, never reused. Tested by `BoardGamesPlatform.Protocol.Tests`.
- Changelog fragments: each pull request describes its changes in `changelog/unreleased/<branch>.md`; `tools/changelog/Merge-ChangelogFragments.ps1` moves them into `CHANGELOG.md` for the release; the CI refuses a pull request other than `docs/release-*` that edits `CHANGELOG.md`.
- The CI runs the tests of the changelog merge script on every pull request and push, so that a change that breaks it is caught before the release step uses it.
- Back: listener of the Gateway on plain TCP (development only), configured by the `Back` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7821), answering the `Ping` operation with the time the Very Back gives, offset included; one client of the Very Back, shared by every connection, configured by the `VeryBack` section (`Host`, `Port` 7822, `ConnectTimeout`). The host refuses to start on an invalid value, before it listens.
- Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Back tests (`BoardGamesPlatform.Back.Tests`): settings validation, the host setup, and the listener of the Gateway on loopback sockets with a fake Very Back.
- `VeryBackClient` in `BoardGamesPlatform.Platform.Infrastructure`: the client the Back uses to call the Very Back. It opens one shared connection on first use (within a connect timeout), opens a new one on the next call when the connection has closed, never retries a failed request, and offers the Ping operation.
- `VeryBackClientOptions`: the `VeryBack` settings (`Host`, `Port` 7822 by default, `ConnectTimeout` 5 seconds by default, at most 5 minutes), checked at startup so that the Back refuses to start on an invalid value.
- `BoardGamesPlatform.Platform.Infrastructure.Tests`: tests of the Very Back client against a fake Very Back on a loopback socket.
- Website, ping page (`/ping`): a button calls the Ping method of the Gateway and shows the time of SQL Server with its offset, formatted with the culture of the request, with a localized "in progress" state and a localized generic error (never the text of the exception).
- Website, client of the Gateway: `IGatewayClient` and its `SignalRGatewayClient`, a scoped service (one SignalR connection per circuit, started on first use, disposed with the circuit). Setting `Gateway:HubUrl` (also `Gateway__HubUrl`, default `http://localhost:5273/hubs/platform`), validated at startup: an invalid value stops the website and the message names the setting, never its value. Setting `Gateway:RequestTimeout` (default 10 seconds, from zero excluded to one minute) bounds every call: a Gateway that never answers gives the generic error. Disposing the client cancels the calls in progress and the calls waiting to connect. Plain HTTP until authentication (0.10.0).
- Website, languages: every text comes from `.resx` resources (`WebTexts`, English by default and French); the supported cultures are `en` and `fr` (that the culture chosen by the request localization middleware reaches the circuit is not verified end to end).
- Website tests (`BoardGamesPlatform.Web.Tests`): the page rendered by bUnit with a fake client (time, progress, error without the exception message, English and French), the SignalR client against the real Gateway started in process with a fake Back client (restart after a loss, single start for parallel calls, cancellation, timeout, disposal), and the `Gateway:HubUrl` setting.
- `bunit` 2.13.3, for the website tests only.
- Administrator documentation: `doc/adm/website.md`.
- End-to-end tests (`BoardGamesPlatform.EndToEnd.Tests`, test ports 17500-17599): a headless SignalR front calls the real Gateway, which calls the real Back, which calls the real Very Back, started in one process (settings in memory, start in dependency order, stop in the reverse order, each stop bounded and every failure collected). The front reaches the Gateway through the in-memory test server by long polling, not through a socket: only the hops Gateway to Back to Very Back are real TCP connections. With a fake Ping procedure (CI): the front receives exactly the fake time with its non-local offset; a failing procedure and a stopped Very Back give the generic SignalR error only, and the Gateway stays usable. With the real `SqlPingProcedure`: the same Ping on the database named by `BOARDGAMESPLATFORM_TEST_DATABASE`, skipped without it.
- TestKit, `TestDatabase`: the single guard of the tests that use a real SQL Server (the variable, the skip reason, and the refusal of any database whose real name does not start with `TEST_` or `DEV_`), with unit tests of the name rule.
- Gateway, `BackClient`: the real client of the Back, built on the `LinkClient` of the protocol, with plain TCP (development only until TLS in 0.5.0). It replaces the temporary `UnavailableBackClient`, which is removed: `Ping` of the hub now reaches the Back.
- Gateway settings of the link to the Back, section `Back` (`Back:Host` default `127.0.0.1`, `Back:Port` default 7821, `Back:ConnectTimeout` default 5 seconds), validated at startup: an invalid value stops the Gateway and the message names the setting, never its value.
- Gateway tests: the `Back` settings, `BackClient` against a fake Back on a loopback socket (time with offset, unreachable Back, disposal), and the hub with the real client (Ping through the hub, generic error when the Back does not answer, invalid `Back:Port` stops the startup).
- Gateway tests use their block of fixed test ports (17400-17499, `TestPortBlocks.Gateway`), never a port handed out by the system.
- Gateway, SignalR hub: `PlatformHub` on the path `/hubs/platform` (setting `Gateway:HubPath`, validated at startup, must start with a slash) with the `Ping` method, which asks the Back through `IBackClient` and returns its time with its offset. Detailed errors are off: a front never receives an internal message. The hub is not authenticated until 0.10.0.
- Gateway tests (`BoardGamesPlatform.Gateway.Tests`): the Gateway started in process with a fake Back client, called by a SignalR client (Ping, offset of the time, failing Back without internal message, hub path setting, `/health`).
- `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.AspNetCore.SignalR.Client` 10.0.14, for the Gateway tests only.
- Very Back: listener of the Back on plain TCP (development only), configured by the `VeryBack` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7822), answering the `Ping` operation with the time of SQL Server; a failing connection is closed without stopping the others.
- Very Back: `ConnectionStrings:Main` (empty in `appsettings.json`, set by user secrets or an environment variable); the host refuses to start without it, with a string the SQL client cannot read (the error, which may quote a piece of the string, is not repeated), or with an invalid address or port.
- Very Back: call of `ServerCore.SP_ServerCore_Server_Ping`, whose JSON result is read strictly (one `ServerTime` property, with an explicit offset).
- Very Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Very Back tests (`BoardGamesPlatform.VeryBack.Tests`): settings validation, reading of the Ping result, the host setup, the exit code, and the listener on loopback sockets with fake procedures and a fake listener.
- `tools/changelog/tests/Test-MergeChangelogFragments.ps1`: plain PowerShell tests of the merge script, run on throwaway copies (one case per defect above, `-WhatIf`, and the real `CHANGELOG.md`).
- Website tests: every key exists in both languages, no text is empty, every key asked in the source exists, no .razor file holds a visible literal text (a heuristic, not a Razor parser), and the not found page, the error banner, the error page and the reconnection dialog rendered by bUnit in English and in French.
- Very Back: its SQL sessions are named `BoardGamesPlatform.VeryBack` (`program_name` on SQL Server) when the connection string sets no `Application Name`; a value set by the administrator is kept.
- Very Back: documentation of the connections to SQL Server (pool, `Max Pool Size`, `Load Balance Timeout` / `Connection Lifetime`, `Pooling=False`).

### Changed

- Roadmap, 0.3.0: socket protocol decided (byte-stream transport, length-prefixed frames with a correlation identifier and a JSON payload, maximum frame size, several requests in flight), default ports 7821 (Gateway → Back) and 7822 (Back → Very Back) on the loopback address, ordered branch list, end-to-end test against a fake procedure executor in the CI; the SQL project keeps its original format.
- Contribution guide: two pull requests open at the same time never touch the same file.
- Roadmap and administrator documentation: the website uses the Blazor Interactive Server render mode only (no WebAssembly, no static rendering), and its server process is the only SignalR client of the gateway; the gateway is never reachable from the Internet. The decision and its reasons are in the decisions table.
- Roadmap: every executable runs on Kestrel as a service, without IIS; the website is exposed behind a reverse proxy (Nginx, IIS possible); the network flows separate the Internet, the reverse proxy, the website and the gateway.
- Roadmap 0.10.0: the gateway authenticates the website, counts the rate limit per player, receives the IP address of the player from the website only, and the storage of the session token in the website is decided.
- Observability: `HostExitCode` gives the exit code of any executable once its host has run (moved from the Very Back, which uses it too).
- Platform.Infrastructure: `VeryBackClient.PingAsync` returns the time of the database server itself, so that no message type of the Very Back reaches the Back.
- Back and Very Back: the startup log is written once the listener is bound.
- Tests: the network tests use fixed ports of the block 17000-17999 (one block of 100 ports per test project, locked for its run, moved with `BOARDGAMESPLATFORM_TEST_PORT_BASE`), never a port handed out by the system; the Back and the Very Back are also started with their delivered configuration, to prove they listen on 7821 and 7822. The rule is written in the roadmap and in `test/README.md`.
- Website: the Interactive Server render mode is set for the whole application (`Routes` and `HeadOutlet`), as decided for 0.3.0.
- Settings of the Very Back client: `VeryBackClientOptions.FindProblem()` returns the first problem (or null) and the startup validator of the Back uses it,
  with no exception used for control flow; `Validate()` remains for the constructor of the client. Same rules, same messages.
- Protocol: the hardened accept loop of the Very Back moves to `LinkServer` (`BoardGamesPlatform.Protocol.Links`), and the connection management of the Back's Very Back client moves to `LinkClient`, so that the Back and the Gateway reuse them instead of a copy per tier. No change of behavior (the tests of the Very Back and of the client run unchanged), except two texts: the stop of a listener is logged as "Listener of the Very Back stopped", and a call after the disposal says "The client of the Very Back has been disposed". The events of these classes take the ids 9001-9005 and 9011-9018, reserved for them.
- TestKit: the logger that keeps the messages and the listener whose accepts fail on demand move there from the Very Back tests; the Protocol tests check the link server and the link client directly, under another tier name.

### Removed

- `BoardGamesPlatform.Web.Client` (WebAssembly project of the template) and the WebAssembly packages: the website runs in the Interactive Server render mode only.

### Fixed

- The Very Back Ping test on a real SQL Server now refuses a database that is not a `TEST_` or `DEV_` one, like the other SQL tests (it never checked the name).
- `tools/changelog/Merge-ChangelogFragments.ps1` no longer loses lines of `CHANGELOG.md` while reporting success: the summary under `[Unreleased]` and the continuation lines of its entries are kept; the last or first lines are no longer repeated when `[Unreleased]` ends or starts the file; an unknown heading or a paragraph inside `[Unreleased]` stops the script before it writes; an indented line that follows no entry of its own fragment, or a blank line inside an entry, stop it too; a fragment that cannot be deleted stops it with an error, so that it is not merged twice; the changelog is written through a temporary file, and a failed write is reported; a fragment without any entry gives a warning.
- Website: the texts of the Blazor template (error page with its "Request ID" label, not found page, error banner of the layout, reconnection dialog, title of the home page) now come from the resources (`WebTexts.resx` and `WebTexts.fr.resx`) instead of being written in the pages; the `lang` attribute of the document follows the language of the request. The script of the reconnection dialog writes no text: the texts are in the markup, rendered from the resources.
- Website: the error page no longer shows the template advice about the Development environment.
- Website: the not found page has a title and a level 1 heading (was a level 3 heading), so the router focuses it.
- Very Back: no SQL session stays open on SQL Server after a stop or a redeployment: the pool of its connection string is cleared once the listener has stopped.
- Gateway tests: the checks that an invalid setting stops the startup build the host directly instead of going through `WebApplicationFactory`, which could report a disposed service provider instead of the validation failure on the CI runner.
- CI, changelog check: the promotions `release/*` → `dev` and `dev` → `main` may carry `CHANGELOG.md`, finalized on the release branch; they were refused like any other pull request.

## [0.2.0] - 2026-10-29

Solution skeleton and test infrastructure: every tier starts and logs its startup, and the CI enforces the architecture. No game code yet.

### Added

- Roadmap to v1.0.0 (`doc/analyse/roadmap.md`): scope, versions, test strategy, network flows, risks and decisions, including internationalization and the arcade neon design system.
- README preview: mock-ups of the home page and of a Battleships match.
- Coding conventions for C# and SQL: file headers (legal author, no version history: Git keeps it), regions, intent-first documentation, code rules, SQL object headers and naming.
- Commit scopes (`release`, project names), `chore/` branches for repository maintenance, and branch protection rules in the contribution guide.
- Solution layout: 15 projects (shared libraries, Blazor Auto website, Gateway, Back platform modules and host, Very Back, SQL project), grouped by tier in solution folders; every host starts and logs its startup.
- Observability: the Back, the Very Back, the Gateway and the website write their logs to the system log (Windows Event Log or systemd journal), and can send their logs, traces and metrics to an OpenTelemetry collector configured in their settings; nothing is sent without one (`doc/adm/observability.md`).
- Configuration and secrets conventions (`doc/adm/configuration.md`): default .NET configuration sources, no secret in a committed file, user secrets in development, service environment variables on the servers until the vault of 0.7.0.
- Test infrastructure: xUnit v3 on Microsoft.Testing.Platform (selected in `global.json`), Shouldly, coverage and TRX extensions shared by every `*.Tests` project; a TestKit library (repository paths, fake clock).
- Architecture tests: project references, dependencies between layers (ArchUnitNET), committed settings that hold no secret value, connection strings declared by the Very Back only.
- CI solution filter that builds every .NET project and leaves out the SQL project.
- CI checks: `dotnet format --verify-no-changes` for the `.editorconfig` rules the build does not enforce (line endings, naming); the run fails when the CI solution filter no longer matches the solution, or when a test project runs no test.
- CI reports: every run publishes the TRX and Cobertura coverage reports of the test projects; the coverage leaves out the test tooling (`test/CodeCoverage.config`).

### Changed

- Code style rules aligned with the conventions: explicit types instead of `var`, explicit object creation, braces optional for single-line statements, block-bodied members, classic constructors; SQL files indented with tabs.
- Roadmap: a TestKit helper is added by the version that first needs it; the seeded random source moves to 0.13.0, and network tests bind port 0 instead of reserving a free port in advance (0.3.0).
- The solution is renamed from SocialGamesPlatform to BoardGamesPlatform (projects, namespaces, documentation).

## [0.1.0] - 2026-10-29

Repository bootstrap: no game code yet.

### Added

- Solution baseline: .NET 10 SDK, central package management, Roslynator analyzers, build-time code style.
- Repository layout: `src/`, `test/` and `doc/` (analysis, developer, user and administrator documentation).
- Doxygen configuration generating the developer documentation from the C# XML comments.
- Bilingual README (English and French) and PolyForm Strict 1.0.0 license.
- Contribution guide describing the branching model, commit and pull request conventions, versioning and releases.
- Continuous integration workflow building and testing every pull request.

[Unreleased]: https://github.com/micdef/BoardGamesPlatform/compare/v0.4.0...HEAD
[0.4.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/micdef/BoardGamesPlatform/releases/tag/v0.1.0
