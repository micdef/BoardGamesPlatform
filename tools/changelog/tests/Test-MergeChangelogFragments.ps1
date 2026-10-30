# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/changelog/tests/Test-MergeChangelogFragments.ps1       │
# │ Purpose      │ Proves that Merge-ChangelogFragments.ps1 never loses or      │
# │              │ duplicates a line of CHANGELOG.md, and stops (exit code 1,   │
# │              │ nothing written) when it cannot merge safely.                │
# │ Rules        │ 1. Each case runs the script in a throwaway folder of the    │
# │              │    temporary directory, never in the repository.             │
# │              │ 2. Plain PowerShell, no test framework to install.           │
# │              │ 3. The locked-fragment case needs Windows (a lock does not   │
# │              │    prevent a deletion elsewhere); it is skipped otherwise.   │
# │ Usage        │ pwsh tools/changelog/tests/Test-MergeChangelogFragments.ps1  │
# │ Exit code    │ 0 when every case passes, 1 otherwise.                       │
# └──────────────┴──────────────────────────────────────────────────────────────┘

$ErrorActionPreference = 'Stop'

$script = (Resolve-Path (Join-Path $PSScriptRoot '..\Merge-ChangelogFragments.ps1')).Path
$repositoryChangelog = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\CHANGELOG.md')).Path
$failures = [Collections.Generic.List[string]]::new()
$passed = 0

# Creates a throwaway repository root with a changelog and fragments (name → content); returns its path
function New-Case([string] $changelog, [hashtable] $fragments)
{
    $root = Join-Path ([IO.Path]::GetTempPath()) ('changelog-test-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $root 'changelog\unreleased') -Force | Out-Null
    [IO.File]::WriteAllText((Join-Path $root 'CHANGELOG.md'), ($changelog -replace "`r?`n", "`r`n"), [Text.UTF8Encoding]::new($false))
    foreach ($name in $fragments.Keys)
    {
        [IO.File]::WriteAllText((Join-Path $root "changelog\unreleased\$name"), ($fragments[$name] -replace "`r?`n", "`r`n"), [Text.UTF8Encoding]::new($false))
    }
    return $root
}

# Runs the script on a case; returns its exit code and output
function Invoke-Merge([string] $root, [switch] $WhatIf)
{
    $arguments = @('-NoProfile', '-File', $script, '-RepositoryRoot', $root)
    if ($WhatIf) { $arguments += '-WhatIf' }
    $output = & pwsh @arguments 2>&1 | Out-String
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

# Records the result of one check
function Assert([string] $case, [bool] $condition, [string] $message)
{
    if ($condition) { $script:passed++ } else { $script:failures.Add("${case}: $message") }
}

function Read-Changelog([string] $root) { return [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md')) -replace "`r`n", "`n" }
function Get-Fragments([string] $root) { return @(Get-ChildItem (Join-Path $root 'changelog\unreleased') -Filter '*.md' -File | ForEach-Object Name) }

$published = "## [0.1.0] - 2026-10-29`n`n### Added`n`n- First version.`n"
$fragmentA = "### Added`n`n- Entry A.`n"
$fragmentB = "### Fixed`n`n- Entry B,`n  on two lines.`n"

# Case 1: the summary under [Unreleased] and the continuation lines of the existing entries are kept
$case = 'summary and continuation lines kept'
$root = New-Case "# Changelog`n`n## [Unreleased]`n`nSummary of the coming version.`n`n### Added`n`n- Existing entry,`n  continued.`n`n$published" @{ 'a.md' = $fragmentA; 'b.md' = $fragmentB }
$run = Invoke-Merge $root
$text = Read-Changelog $root
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode): $($run.Output)"
Assert $case ($text.Contains("Summary of the coming version.")) 'the summary is lost'
Assert $case ($text.Contains("- Existing entry,`n  continued.`n- Entry A.")) 'the continuation line is lost, or the order is wrong'
Assert $case ($text.Contains("### Fixed`n`n- Entry B,`n  on two lines.")) 'the entry of the fragment on two lines is wrong'
Assert $case ($text.EndsWith($published)) 'the published versions changed'
Assert $case ((Get-Fragments $root).Count -eq 0) 'the fragments were not deleted'

# Case 2: no version published yet: the last line of the file is not repeated
$case = 'no published version'
$root = New-Case "# Changelog`n`n## [Unreleased]`n`n### Added`n`n- Existing entry.`n" @{ 'a.md' = $fragmentA }
$run = Invoke-Merge $root
$text = Read-Changelog $root
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode): $($run.Output)"
Assert $case ($text -eq "# Changelog`n`n## [Unreleased]`n`n### Added`n`n- Existing entry.`n- Entry A.`n") "unexpected result:`n$text"

# Case 3: [Unreleased] on the first line: the first lines are not repeated either
$case = '[Unreleased] on the first line'
$root = New-Case "## [Unreleased]`n`n### Added`n`n- Existing entry.`n`n$published" @{ 'a.md' = $fragmentA }
$run = Invoke-Merge $root
$text = Read-Changelog $root
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode): $($run.Output)"
Assert $case ($text -eq "## [Unreleased]`n`n### Added`n`n- Existing entry.`n- Entry A.`n`n$published") "unexpected result:`n$text"

# Cases 4 and 5: an unexpected heading or text inside a section of [Unreleased] stops the script, nothing written nor deleted
$unexpected = @{
    'unknown heading in [Unreleased]' = "# Changelog`n`n## [Unreleased]`n`n### Notes`n`n- Kept by hand.`n`n$published"
    'free text inside a section' = "# Changelog`n`n## [Unreleased]`n`n### Added`n`n- Entry.`n`nA paragraph written by hand.`n`n$published"
}
foreach ($case in $unexpected.Keys)
{
    $root = New-Case $unexpected[$case] @{ 'a.md' = $fragmentA }
    $before = Read-Changelog $root
    $run = Invoke-Merge $root
    Assert $case ($run.ExitCode -eq 1) "exit code $($run.ExitCode), expected 1"
    Assert $case ((Read-Changelog $root) -eq $before) 'the changelog was changed'
    Assert $case ((Get-Fragments $root).Count -eq 1) 'a fragment was deleted'
}

# Case 6: -WhatIf writes and deletes nothing
$case = '-WhatIf'
$root = New-Case "# Changelog`n`n## [Unreleased]`n`n$published" @{ 'a.md' = $fragmentA }
$before = Read-Changelog $root
$run = Invoke-Merge $root -WhatIf
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode)"
Assert $case ((Read-Changelog $root) -eq $before) 'the changelog was changed'
Assert $case ((Get-Fragments $root).Count -eq 1) 'the fragment was deleted'

# Case 7: a fragment that cannot be deleted stops the script with exit code 1, names it and says not to run the script again
$case = 'fragment that cannot be deleted'
if ($IsWindows)
{
    $root = New-Case "# Changelog`n`n## [Unreleased]`n`n$published" @{ 'a.md' = $fragmentA; 'b.md' = $fragmentB }
    $lock = [IO.File]::Open((Join-Path $root 'changelog\unreleased\b.md'), 'Open', 'Read', 'Read')
    try { $run = Invoke-Merge $root } finally { $lock.Dispose() }
    Assert $case ($run.ExitCode -eq 1) "exit code $($run.ExitCode), expected 1"
    Assert $case ($run.Output -match 'b\.md') 'the fragment left is not named'
    Assert $case ($run.Output -match '(?i)do not run') 'the output does not say not to run the script again'
    Assert $case ((Read-Changelog $root).Contains('- Entry B,')) 'the changelog does not hold the merged entries'
}
else { Write-Host "Skipped: $case (needs Windows)" }

# Case 8: the real changelog of the repository with three fragments: every line before the merge is still there, once
$case = 'real changelog of the repository'
$root = New-Case ([IO.File]::ReadAllText($repositoryChangelog)) @{ 'a.md' = $fragmentA; 'b.md' = $fragmentB; 'c.md' = "### Changed`n`n- Entry C.`n" }
$beforeLines = (Read-Changelog $root) -split "`n" | Where-Object { $_.Trim().Length -gt 0 }
$run = Invoke-Merge $root
$afterLines = (Read-Changelog $root) -split "`n" | Where-Object { $_.Trim().Length -gt 0 }
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode): $($run.Output)"
foreach ($group in ($beforeLines | Group-Object -CaseSensitive))
{
    # A "### <Section>" heading may be added by the merge; any other line must appear exactly as many times as before
    $count = @($afterLines | Where-Object { $_ -ceq $group.Name }).Count
    $expected = $group.Count
    if ($group.Name.StartsWith('### ')) { if ($count -lt $expected) { $failures.Add("${case}: the heading '$($group.Name)' appears $count time(s), fewer than $expected") } }
    elseif ($count -ne $expected) { $failures.Add("${case}: the line '$($group.Name)' appears $count time(s) instead of $expected") }
}
foreach ($entry in '- Entry A.', '- Entry B,', '  on two lines.', '- Entry C.')
{
    Assert $case (@($afterLines | Where-Object { $_ -ceq $entry }).Count -eq 1) "the merged line '$entry' is not there exactly once"
}
# Cases 9 to 11: an indented line that follows no entry of its own fragment, or a blank line inside an entry, stop the script, nothing written
$refused = [ordered]@{
    'indented line after an entry of another fragment' = @("# Changelog`n`n## [Unreleased]`n`n$published", @{ 'a.md' = "### Added`n`n- A one.`n"; 'b.md' = "### Added`n`n  stray line of b`n- B.`n" })
    'blank line inside an entry of a fragment' = @("# Changelog`n`n## [Unreleased]`n`n$published", @{ 'a.md' = "### Added`n`n- Entry,`n`n  second paragraph.`n" })
    'blank line inside an entry of [Unreleased]' = @("# Changelog`n`n## [Unreleased]`n`n### Added`n`n- e1`n`n  para2`n`n$published", @{ 'a.md' = $fragmentA })
}
foreach ($case in $refused.Keys)
{
    $root = New-Case $refused[$case][0] $refused[$case][1]
    $before = Read-Changelog $root
    $count = (Get-Fragments $root).Count
    $run = Invoke-Merge $root
    Assert $case ($run.ExitCode -eq 1) "exit code $($run.ExitCode), expected 1"
    Assert $case ((Read-Changelog $root) -eq $before) 'the changelog was changed'
    Assert $case ((Get-Fragments $root).Count -eq $count) 'a fragment was deleted'
}

# Case 12: a fragment without any entry is deleted with a warning
$case = 'fragment without any entry'
$root = New-Case "# Changelog`n`n## [Unreleased]`n`n$published" @{ 'a.md' = $fragmentA; 'empty.md' = "### Added`n" }
$run = Invoke-Merge $root
Assert $case ($run.ExitCode -eq 0) "exit code $($run.ExitCode): $($run.Output)"
Assert $case ($run.Output -match '::warning::empty\.md holds no entry') 'no warning for the fragment without any entry'

# Case 13: a changelog that cannot be written stops the script with an error; nothing is merged and the fragments stay
$case = 'changelog that cannot be written'
$root = New-Case "# Changelog`n`n## [Unreleased]`n`n$published" @{ 'a.md' = $fragmentA }
$changelogFile = Get-Item (Join-Path $root 'CHANGELOG.md')
$before = Read-Changelog $root
$changelogFile.IsReadOnly = $true
try { $run = Invoke-Merge $root } finally { $changelogFile.IsReadOnly = $false }
Assert $case ($run.ExitCode -eq 1) "exit code $($run.ExitCode), expected 1"
Assert $case ($run.Output -match '::error::CHANGELOG\.md could not be written') 'the error is not reported'
Assert $case ((Read-Changelog $root) -eq $before) 'the changelog was changed'
Assert $case ((Get-Fragments $root).Count -eq 1) 'the fragment was deleted'

# Result
Write-Host "$passed check(s) passed, $($failures.Count) failed."
$failures | ForEach-Object { Write-Host "::error::$_" }
exit ([int]($failures.Count -gt 0))
