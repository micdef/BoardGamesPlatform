# Contributing to BoardGamesPlatform

This guide describes how changes reach the repository: branches, commits, pull requests, versions and releases.
These rules apply to every contributor, human or AI assistant.

## Branching model

```
main ──────────────────────────●─────────── tag v1.0.0
  └─ dev ─────────────────●────┘
       └─ release/1.0.0 ──┘
            ├─ feature/game-board ─┘
            └─ fix/hit-detection ──┘
```

| Branch           | Created from     | Merged into                                 | Purpose                                              |
|------------------|------------------|---------------------------------------------|------------------------------------------------------|
| `main`           | —                | —                                           | Released versions only. Every merge is tagged.       |
| `dev`            | `main`           | `main`                                      | Integration branch: the last safeguard before a release. |
| `release/X.Y.Z`  | `dev`            | `dev`                                       | Collects the work planned for one version.           |
| `feature/<name>` | `release/X.Y.Z`  | `release/X.Y.Z`                             | A new feature.                                       |
| `fix/<name>`     | `release/X.Y.Z`  | `release/X.Y.Z`                             | A bug fix during the development of a version.       |
| `docs/<name>`    | `release/X.Y.Z`  | `release/X.Y.Z`                             | Documentation only.                                  |
| `chore/<name>`   | `release/X.Y.Z`  | `release/X.Y.Z`                             | Repository maintenance: tooling, configuration, CI.  |
| `hotfix/<name>`  | `main`           | `main`, then `dev` and the current release  | An urgent fix on a released version.                 |

Branch names are lowercase, in English, with words separated by hyphens: `feature/fleet-placement`.

### Flow of a version

1. Create `release/X.Y.Z` from `dev`.
2. Create one branch per feature or fix from `release/X.Y.Z`, then open a pull request back into it.
3. When the version is complete, finalize its changelog, open a pull request from `release/X.Y.Z` into `dev` and validate it there.
4. Open a pull request from `dev` into `main`, merge it, then tag the merge commit.

The Releases section below details steps 3 and 4.

### Hotfixes

A hotfix starts from `main` and is merged into `main` with a new patch tag.
It is then merged into `dev` and into the release branch in progress, otherwise the next release would bring the bug back.

## Commits

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org) and are written in English.

```
<type>(<scope>): <summary>

<body: what changed and why, as a short list or paragraph>
```

The `(<scope>)` part is optional (`docs: fix typo` is valid).

| Type       | Use for                                              |
|------------|------------------------------------------------------|
| `feat`     | A new feature                                        |
| `fix`      | A bug fix                                            |
| `docs`     | Documentation only                                   |
| `test`     | Adding or updating tests                             |
| `refactor` | A code change that neither fixes a bug nor adds a feature |
| `perf`     | A performance improvement                            |
| `build`    | Build system, project files, NuGet packages          |
| `ci`       | Continuous integration workflows                     |
| `chore`    | Repository maintenance                               |

### Scopes

A scope names the part of the solution a commit touches, in lowercase. It is used when the change is limited to one area.

| Scope            | Use for                                                     | Example                                  |
|------------------|-------------------------------------------------------------|------------------------------------------|
| `release`        | Moving a version up the branches (release → dev → main)     | `chore(release): merge 0.2.0 into dev`   |
| `<project-name>` | A change limited to one project of `src/` or `test/`, named after the project in lowercase | `feat(battleships): add fleet placement` |

New scopes are added to this table when a new area of the solution is created.

### Rules

- The summary uses the imperative mood (`add`, not `added`), starts in lowercase, has no final period and stays under 72 characters.
- The body explains **what** changed and **why**, not how: the diff already shows how.
- One commit = one coherent change. Unrelated changes go into separate commits.
- Commits written with an AI assistant keep its `Co-Authored-By` trailer.

## Pull requests

- Every change reaches `main`, `dev` and `release/*` through a pull request opened against the parent branch.
- The title follows the commit convention: `feat: add fleet placement`.
- The description states the purpose, the main changes and how they were tested.
- The build must pass and every review thread must be resolved before merging.
- **Only the repository owner merges a pull request.** Nobody else, including AI assistants, merges.

### Merge strategy

Pull requests are merged with a **merge commit** only. Squash and rebase merges are disabled so that the full history is kept.
Merged branches are deleted automatically.

### Branch protection

GitHub rulesets enforce these rules; nobody can bypass them, including the repository owner.

| Rule                                   | `main`, `dev` | `release/*` |
|----------------------------------------|:-------------:|:-----------:|
| Pull request required                  | ✅            | ✅          |
| `Build and test` check must pass       | ✅            | ✅          |
| Merge commit only                      | ✅            | ✅          |
| Force push forbidden                   | ✅            | ✅          |
| Deletion forbidden                     | ✅            | ❌ (deleted automatically once merged into `dev`) |

### History

History is never rewritten on a shared branch (`main`, `dev`, `release/*`): no force push, no amend, no rebase of pushed commits.
A mistake is corrected by a new commit.

## Versioning

Versions follow [Semantic Versioning](https://semver.org) `MAJOR.MINOR.PATCH`:

- **MAJOR**: incompatible change.
- **MINOR**: new feature, backward compatible.
- **PATCH**: bug fix, backward compatible.

Before `1.0.0`, versions are `0.Y.Z` and the software is not considered stable. Pre-releases use a suffix:

| Stage | Example           | Meaning                                    |
|-------|-------------------|--------------------------------------------|
| Alpha | `0.1.0-alpha.1`   | Incomplete, for internal testing           |
| Beta  | `0.4.0-beta.2`    | Feature complete for this version, being stabilized |
| Final | `1.0.0`           | First installable and executable release   |

## Releases

1. On the release branch, move the `[Unreleased]` entries of [CHANGELOG.md](CHANGELOG.md) under the new version number and date, through a `docs/release-X.Y.Z` pull request.
   The changelog is finalized before the version leaves its branch, because `dev` and `main` only accept pull requests.
2. Merge `release/X.Y.Z` into `dev` through a pull request.
3. Merge `dev` into `main` through a pull request.
4. Create an annotated tag on the merge commit of `main`: `git tag -a v1.0.0 -m "Release 1.0.0"`, then push it.
5. Publish a GitHub release from the tag, using the changelog entries as release notes.

Downloadable packages are published from version `1.0.0` onward.
Every `0.x.y` version, with or without an `-alpha.N` / `-beta.N` suffix, is published as a GitHub pre-release without packages.

## Continuous integration

Every pull request targeting `main`, `dev` or `release/*` is built and tested by GitHub Actions ([ci.yml](.github/workflows/ci.yml)).
A pull request with a failing build is not merged.

The CI builds the projects listed in `BoardGamesPlatform.CI.slnf`, and fails when:

- a `.csproj` under `src/` or `test/` is missing from `BoardGamesPlatform.slnx`, or a `.csproj` of the solution is missing from the filter ([Test-CISolutionFilter.ps1](.github/scripts/Test-CISolutionFilter.ps1)); a new project is therefore added to both files;
- a test project runs no test.

## Code and documentation

- Code, comments, documentation and commit messages are written in English.
- Every member, public or private, carries an XML documentation comment (`///`), which feeds IntelliSense and the Doxygen documentation.
- File headers, regions, documentation and code rules for C# and SQL are described in the [coding conventions](doc/dev/coding-conventions.md).
- Analyzer and style warnings are errors: fix them, do not suppress them without a documented reason.
