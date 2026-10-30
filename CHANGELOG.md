# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

### Changed

- Roadmap, 0.3.0: socket protocol decided (byte-stream transport, length-prefixed frames with a correlation identifier and a JSON payload, maximum frame size, several requests in flight), default ports 7821 (Gateway → Back) and 7822 (Back → Very Back) on the loopback address, ordered branch list, end-to-end test against a fake procedure executor in the CI; the SQL project keeps its original format.

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

[Unreleased]: https://github.com/micdef/BoardGamesPlatform/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/micdef/BoardGamesPlatform/releases/tag/v0.1.0
