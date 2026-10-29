# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Commit scopes (`release`, project names) and branch protection rules in the contribution guide.

## [0.1.0] - 2026-10-29

Repository bootstrap: no game code yet.

### Added

- Solution baseline: .NET 10 SDK, central package management, Roslynator analyzers, build-time code style.
- Repository layout: `src/`, `test/` and `doc/` (analysis, developer, user and administrator documentation).
- Doxygen configuration generating the developer documentation from the C# XML comments.
- Bilingual README (English and French) and PolyForm Strict 1.0.0 license.
- Contribution guide describing the branching model, commit and pull request conventions, versioning and releases.
- Continuous integration workflow building and testing every pull request.

[Unreleased]: https://github.com/micdef/SocialGamesPlatform/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/micdef/SocialGamesPlatform/releases/tag/v0.1.0
