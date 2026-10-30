### Added

- Changelog fragments: each pull request describes its changes in `changelog/unreleased/<branch>.md`; `tools/changelog/Merge-ChangelogFragments.ps1` moves them into `CHANGELOG.md` for the release; the CI refuses a pull request other than `docs/release-*` that edits `CHANGELOG.md`.

### Changed

- Contribution guide: two pull requests open at the same time never touch the same file.
