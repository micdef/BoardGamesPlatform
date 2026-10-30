# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ .github/scripts/Test-ChangelogUntouched.ps1                  │
# │ Purpose      │ Fails a pull request that edits CHANGELOG.md directly:       │
# │              │ every pull request adds its own fragment instead, so that    │
# │              │ two open pull requests never conflict on the changelog.      │
# │ Rules        │ 1. A docs/release-* branch may change CHANGELOG.md           │
# │              │    (it merges the fragments, then finalizes the version).    │
# │              │ 2. The promotions release/* → dev and dev → main carry the   │
# │              │    finalized changelog: they may change it too.              │
# │              │ 3. The other branches add changelog/unreleased/<branch>.md.  │
# │ Usage        │ pwsh .github/scripts/Test-ChangelogUntouched.ps1 `           │
# │              │     -BaseRef origin/release/0.3.0 -HeadBranch feature/x      │
# │ Exit code    │ 0 when the rule holds, 1 otherwise.                          │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding()]
param(
    # Base of the pull request, as a git reference that exists locally (for example origin/release/0.3.0)
    [Parameter(Mandatory)]
    [string] $BaseRef,

    # Name of the branch of the pull request (for example feature/ping-contracts)
    [Parameter(Mandatory)]
    [string] $HeadBranch
)

$ErrorActionPreference = 'Stop'

# Rule 1: the release documentation branch merges the fragments and finalizes the changelog
if ($HeadBranch -like 'docs/release-*')
{
    Write-Host "$HeadBranch is a release documentation branch: it may change CHANGELOG.md."
    exit 0
}

# Rule 2: a promotion carries the changelog already finalized on the release branch
$baseBranch = $BaseRef -replace '^origin/', ''
if (($HeadBranch -like 'release/*' -and $baseBranch -eq 'dev') -or ($HeadBranch -eq 'dev' -and $baseBranch -eq 'main'))
{
    Write-Host "$HeadBranch into $baseBranch is a promotion: it may change CHANGELOG.md."
    exit 0
}

# Files changed by the pull request since it left its base (three dots: the changes of the branch only)
$changed = @(git diff --name-only "$BaseRef...HEAD")
if ($LASTEXITCODE -ne 0)
{
    Write-Host "::error::git diff against $BaseRef failed; the base must be fetched (actions/checkout with fetch-depth 0)"
    exit 1
}

# Rule 3: any other branch leaves CHANGELOG.md alone
if ($changed -contains 'CHANGELOG.md')
{
    $fragment = 'changelog/unreleased/' + ($HeadBranch -replace '[/\\]', '-') + '.md'
    Write-Host "::error::This pull request edits CHANGELOG.md. Move its entries to $fragment (see changelog/unreleased/README.md)."
    exit 1
}

Write-Host 'CHANGELOG.md is untouched by this pull request.'
exit 0
