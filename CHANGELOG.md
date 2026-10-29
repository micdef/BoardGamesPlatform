# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Commit scopes (`release`, project names) and branch protection rules in the contribution guide.
- Coding conventions for C# and SQL: file headers (legal author, no version history: Git keeps it), regions, intent-first documentation, code rules, SQL object headers and naming.
- `chore/` branches for repository maintenance.
- Roadmap to v1.0.0 (`doc/analyse/roadmap.md`): scope, versions, test strategy, network flows, risks and decisions, including internationalization and the arcade neon design system.
- README preview: mock-ups of the home page and of a Battleships match.
- Solution layout: 15 projects (shared libraries, Blazor Auto website, Gateway, Back platform modules and host, Very Back, SQL project), grouped by tier in solution folders; every host starts and logs its startup.
- CI solution filter that builds every .NET project and leaves out the SQL project.
- Test infrastructure: xUnit v3 on Microsoft.Testing.Platform (selected in `global.json`), Shouldly, coverage and TRX extensions shared by every `*.Tests` project; a TestKit library; architecture tests on the project references and, with ArchUnitNET, on the dependencies between layers.
- CI: `dotnet format --verify-no-changes` checks the `.editorconfig` rules the build does not enforce (line endings, naming).
- CI guards: the run fails when the CI solution filter no longer matches the solution, or when a test project runs no test.
- Observability: the Back, the Very Back, the Gateway and the website write their logs to the system log (Windows Event Log or systemd journal), and can send their logs, traces and metrics to an OpenTelemetry collector configured in their settings; nothing is sent without one (`doc/adm/observability.md`).

### Changed

- Code style rules aligned with the conventions: explicit types instead of `var`, explicit object creation, braces optional for single-line statements, block-bodied members, classic constructors; SQL files indented with tabs.

## [0.1.0] - 2026-10-29

Repository bootstrap: no game code yet.

### Added

- Solution baseline: .NET 10 SDK, central package management, Roslynator analyzers, build-time code style.
- Repository layout: `src/`, `test/` and `doc/` (analysis, developer, user and administrator documentation).
- Doxygen configuration generating the developer documentation from the C# XML comments.
- Bilingual README (English and French) and PolyForm Strict 1.0.0 license.
- Contribution guide describing the branching model, commit and pull request conventions, versioning and releases.
- Continuous integration workflow building and testing every pull request.

[Unreleased]: https://github.com/micdef/BoardGamesPlatform/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/micdef/BoardGamesPlatform/releases/tag/v0.1.0
