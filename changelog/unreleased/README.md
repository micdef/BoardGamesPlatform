# Changelog fragments

Every pull request describes its changes in **its own file** of this folder, never in `CHANGELOG.md`.
Two pull requests open at the same time therefore never touch the same lines, and never conflict on the changelog.

## Writing a fragment

- One file per pull request, named after its branch with `/` replaced by `-`: `feature/ping-contracts` → `feature-ping-contracts.md`.
- Only `### <Section>` headings of [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) (`Added`, `Changed`, `Deprecated`, `Removed`, `Fixed`, `Security`), each followed by `- ` entries; a long entry may continue on lines indented by two spaces.

```markdown
### Added

- Socket protocol, messages: JSON envelopes read strictly.

### Fixed

- The request client no longer waits forever when the peer closes.
```

## Releasing

The `docs/release-X.Y.Z` pull request runs `pwsh tools/changelog/Merge-ChangelogFragments.ps1`, which moves every fragment into the `[Unreleased]` section of `CHANGELOG.md` and deletes it, then finalizes the version as described in [CONTRIBUTING.md](../../CONTRIBUTING.md).
The promotions `release/*` → `dev` and `dev` → `main` then carry the finalized `CHANGELOG.md`.
The CI refuses any other pull request that edits it (`.github/scripts/Test-ChangelogUntouched.ps1`).
