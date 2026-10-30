# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/changelog/Merge-ChangelogFragments.ps1                 │
# │ Purpose      │ Moves the changelog fragments of the merged pull requests    │
# │              │ into the [Unreleased] section of CHANGELOG.md, then deletes  │
# │              │ them; run by the docs/release-X.Y.Z pull request only.       │
# │ Rules        │ 1. A fragment is changelog/unreleased/<branch>.md, holding   │
# │              │    "### <Section>" headings followed by "- " entries.        │
# │              │ 2. Sections: Added, Changed, Deprecated, Removed, Fixed,     │
# │              │    Security (Keep a Changelog); any other text is refused.   │
# │              │ 3. Entries keep the order of the fragment files (by name).   │
# │ Usage        │ pwsh tools/changelog/Merge-ChangelogFragments.ps1 [-WhatIf]  │
# │ Exit code    │ 0 on success, 1 when a fragment is invalid (nothing written).│
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding(SupportsShouldProcess)]
param(
    # Root of the repository; by default, two levels above this script
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

# Sections of Keep a Changelog, in their documented order
$sections = @('Added', 'Changed', 'Deprecated', 'Removed', 'Fixed', 'Security')

$changelogPath = Join-Path $RepositoryRoot 'CHANGELOG.md'
$fragmentFolder = Join-Path $RepositoryRoot 'changelog\unreleased'

# Step 1: read every fragment (the README of the folder is not one), refusing anything outside the expected shape
$fragments = @(Get-ChildItem -Path $fragmentFolder -Filter '*.md' -File | Where-Object Name -ne 'README.md' | Sort-Object Name)
if ($fragments.Count -eq 0)
{
    Write-Host 'No changelog fragment to merge.'
    exit 0
}

$newEntries = [ordered]@{}
foreach ($section in $sections) { $newEntries[$section] = [Collections.Generic.List[string]]::new() }
$errors = [Collections.Generic.List[string]]::new()

foreach ($fragment in $fragments)
{
    $current = $null
    $lineNumber = 0
    foreach ($line in Get-Content -Path $fragment.FullName)
    {
        $lineNumber++
        if ($line -match '^### (?<name>\w+)\s*$')
        {
            $current = $Matches['name']
            if ($current -notin $sections) { $errors.Add("$($fragment.Name):$lineNumber unknown section '$current'") }
        }
        elseif ($line -match '^- \S')
        {
            if ($null -eq $current) { $errors.Add("$($fragment.Name):$lineNumber entry before any '### <Section>' heading") }
            elseif ($current -in $sections) { $newEntries[$current].Add($line) }
        }
        elseif ($line -match '^  \S' -and $null -ne $current -and $current -in $sections -and $newEntries[$current].Count -gt 0)
        {
            # Continuation line of the previous entry (indented by two spaces)
            $last = $newEntries[$current].Count - 1
            $newEntries[$current][$last] = $newEntries[$current][$last] + "`n" + $line
        }
        elseif ($line.Trim().Length -gt 0)
        {
            $errors.Add("$($fragment.Name):$lineNumber unexpected text (only '### <Section>' headings and '- ' entries are allowed)")
        }
    }
}

if ($errors.Count -gt 0)
{
    $errors | ForEach-Object { Write-Host "::error::$_" }
    exit 1
}

# Step 2: read the [Unreleased] section of the changelog and its current entries
$lines = [IO.File]::ReadAllLines($changelogPath)
$start = [Array]::IndexOf($lines, '## [Unreleased]')
if ($start -lt 0) { Write-Host '::error::CHANGELOG.md has no "## [Unreleased]" heading'; exit 1 }
$end = $start + 1
while ($end -lt $lines.Length -and -not $lines[$end].StartsWith('## [')) { $end++ }

$existingEntries = [ordered]@{}
foreach ($section in $sections) { $existingEntries[$section] = [Collections.Generic.List[string]]::new() }
$current = $null
for ($i = $start + 1; $i -lt $end; $i++)
{
    if ($lines[$i] -match '^### (?<name>\w+)\s*$') { $current = $Matches['name'] }
    elseif ($lines[$i] -match '^- \S' -and $null -ne $current -and $existingEntries.Contains($current)) { $existingEntries[$current].Add($lines[$i]) }
}

# Step 3: rebuild the [Unreleased] section: existing entries first, then the fragments, section by section
$section = [Collections.Generic.List[string]]::new()
$section.Add('## [Unreleased]')
foreach ($name in $sections)
{
    $entries = @($existingEntries[$name]) + @($newEntries[$name])
    if ($entries.Count -eq 0) { continue }
    $section.Add('')
    $section.Add("### $name")
    $section.Add('')
    foreach ($entry in $entries) { $section.Add($entry) }
}
$section.Add('')

$result = @($lines[0..($start - 1)]) + @($section) + @($lines[$end..($lines.Length - 1)])

# Step 4: write the changelog, then delete the merged fragments
if ($PSCmdlet.ShouldProcess($changelogPath, "Merge $($fragments.Count) fragment(s) into [Unreleased] and delete them"))
{
    [IO.File]::WriteAllText($changelogPath, (($result -join "`n") -replace "`r?`n", "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
    $fragments | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
    Write-Host "Merged $($fragments.Count) fragment(s) into the [Unreleased] section of CHANGELOG.md."
}

exit 0
