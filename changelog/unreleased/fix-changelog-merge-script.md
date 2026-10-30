### Fixed

- `tools/changelog/Merge-ChangelogFragments.ps1` no longer loses lines of `CHANGELOG.md` while reporting success: the summary under `[Unreleased]` and the continuation lines of its entries are kept; the last or first lines are no longer repeated when `[Unreleased]` ends or starts the file; an unknown heading or a paragraph inside `[Unreleased]` stops the script before it writes; a fragment that cannot be deleted stops it with an error, so that it is not merged twice.

### Added

- `tools/changelog/tests/Test-MergeChangelogFragments.ps1`: plain PowerShell tests of the merge script, run on throwaway copies (one case per defect above, `-WhatIf`, and the real `CHANGELOG.md`).
