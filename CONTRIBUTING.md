# Contributing to SocialGamesPlatform

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
| `hotfix/<name>`  | `main`           | `main`, then `dev` and the current release  | An urgent fix on a released version.                 |

Branch names are lowercase, in English, with words separated by hyphens: `feature/fleet-placement`.

### Flow of a version

1. Create `release/X.Y.Z` from `dev`.
2. Create one branch per feature or fix from `release/X.Y.Z`, then open a pull request back into it.
3. When the version is complete, open a pull request from `release/X.Y.Z` into `dev` and validate it there.
4. Open a pull request from `dev` into `main`, merge it, then tag the merge commit (see the Releases section below).

### Hotfixes

A hotfix starts from `main` and is merged into `main` with a new patch tag.
It is then merged into `dev` and into the release branch in progress, otherwise the next release would bring the bug back.

## Commits

Commit messages follow [Conventional Commits](https://www.conventionalcommits.org) and are written in English.

```
<type>: <summary>

<body: what changed and why, as a short list or paragraph>
```

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
Merged branches are deleted automatically; `main` and `dev` are protected against deletion.

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

1. Merge `dev` into `main` through a pull request.
2. Move the `[Unreleased]` entries of [CHANGELOG.md](CHANGELOG.md) under the new version number.
3. Create an annotated tag on the merge commit: `git tag -a v1.0.0 -m "Release 1.0.0"`, then push it.
4. Publish a GitHub release from the tag, using the changelog entries as release notes.

Downloadable packages are published from version `1.0.0` onward.
Pre-release tags (`0.x.y-alpha.N`, `0.x.y-beta.N`) are published as GitHub pre-releases without packages.

## Continuous integration

Every pull request targeting `main`, `dev` or `release/*` is built and tested by GitHub Actions ([ci.yml](.github/workflows/ci.yml)).
A pull request with a failing build is not merged.

## Code and documentation

- Code, comments, documentation and commit messages are written in English.
- Every public member carries an XML documentation comment (`///`), which feeds IntelliSense and the Doxygen documentation.
- Analyzer and style warnings are errors: fix them, do not suppress them without a documented reason.
