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
# │              │ 4. No line of CHANGELOG.md is ever dropped: the summary of   │
# │              │    [Unreleased] and the continuation lines are kept; any     │
# │              │    other text there, an indented line that follows no entry  │
# │              │    and a blank line inside an entry stop it before it writes.│
# │              │ 5. The sections are written in the order of rule 2, a        │
# │              │    heading present twice is merged into one; a fragment      │
# │              │    without any entry is deleted with a warning.              │
# │ Usage        │ pwsh tools/changelog/Merge-ChangelogFragments.ps1 [-WhatIf]  │
# │ Tests        │ pwsh tools/changelog/tests/Test-MergeChangelogFragments.ps1  │
# │ Exit code    │ 0 on success; 1 when a fragment or the [Unreleased] section  │
# │              │ is not in the expected shape (nothing written), or when a    │
# │              │ merged fragment could not be deleted (do not run it again).  │
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
    # Entries of this fragment under its current heading: a continuation line only follows an entry of the same fragment and heading
    $current = $null
    $entriesHere = 0
    $fragmentEntries = 0
    $previousBlank = $false
    $lineNumber = 0
    foreach ($line in Get-Content -Path $fragment.FullName -Encoding UTF8)
    {
        $lineNumber++
        if ($line -match '^### (?<name>\w+)\s*$')
        {
            $current = $Matches['name']
            $entriesHere = 0
            if ($current -notin $sections) { $errors.Add("$($fragment.Name):$lineNumber unknown section '$current'") }
        }
        elseif ($line -match '^- \S')
        {
            if ($null -eq $current) { $errors.Add("$($fragment.Name):$lineNumber entry before any '### <Section>' heading") }
            elseif ($current -in $sections) { $newEntries[$current].Add($line); $entriesHere++; $fragmentEntries++ }
        }
        elseif ($line -match '^  \S' -and $previousBlank -and $entriesHere -gt 0)
        {
            # A blank line inside an entry would be lost: refused
            $errors.Add("$($fragment.Name):$lineNumber blank line inside an entry (write the entry without blank lines)")
        }
        elseif ($line -match '^  \S' -and $null -ne $current -and $current -in $sections -and $entriesHere -gt 0)
        {
            # Continuation line of the previous entry (indented by two spaces)
            $last = $newEntries[$current].Count - 1
            $newEntries[$current][$last] = $newEntries[$current][$last] + "`n" + $line
        }
        elseif ($line.Trim().Length -gt 0)
        {
            $errors.Add("$($fragment.Name):$lineNumber unexpected text (only '### <Section>' headings and '- ' entries are allowed)")
        }
        $previousBlank = $line.Trim().Length -eq 0
    }

    # A fragment that brings no entry is deleted like the others: say so, since it may be a mistake
    if ($fragmentEntries -eq 0)
    {
        Write-Host "::warning::$($fragment.Name) holds no entry: nothing is merged from it, and it is deleted"
    }
}

if ($errors.Count -gt 0)
{
    $errors | ForEach-Object { Write-Host "::error::$_" }
    exit 1
}

# Step 2: read the [Unreleased] section of the changelog: its summary (the text before the first heading) and its entries, each one with its
# continuation lines; anything else (an unknown heading, a paragraph inside a section) is refused, so that no line is ever dropped
$lines = [IO.File]::ReadAllLines($changelogPath)
$start = [Array]::IndexOf($lines, '## [Unreleased]')
if ($start -lt 0) { Write-Host '::error::CHANGELOG.md has no "## [Unreleased]" heading'; exit 1 }
$end = $start + 1
while ($end -lt $lines.Length -and -not $lines[$end].StartsWith('## [')) { $end++ }

$summary = [Collections.Generic.List[string]]::new()
$existingEntries = [ordered]@{}
foreach ($section in $sections) { $existingEntries[$section] = [Collections.Generic.List[string]]::new() }
$current = $null
$entriesHere = 0
$previousBlank = $false
for ($i = $start + 1; $i -lt $end; $i++)
{
    $line = $lines[$i]
    if ($line -match '^### (?<name>\w+)\s*$')
    {
        $current = $Matches['name']
        $entriesHere = 0
        if ($current -notin $sections) { $errors.Add("CHANGELOG.md:$($i + 1) unknown section '$current' in [Unreleased]") }
    }
    elseif ($null -eq $current)
    {
        # The summary of the coming version, kept as it is
        $summary.Add($line)
    }
    elseif ($line -match '^- \S' -and $current -in $sections) { $existingEntries[$current].Add($line); $entriesHere++ }
    elseif ($line -match '^  \S' -and $previousBlank -and $entriesHere -gt 0)
    {
        # A blank line inside an entry would be lost: refused
        $errors.Add("CHANGELOG.md:$($i + 1) blank line inside an entry of [Unreleased] (write the entry without blank lines)")
    }
    elseif ($line -match '^  \S' -and $current -in $sections -and $entriesHere -gt 0)
    {
        # Continuation line of the previous entry (indented by two spaces)
        $last = $existingEntries[$current].Count - 1
        $existingEntries[$current][$last] = $existingEntries[$current][$last] + "`n" + $line
    }
    elseif ($line.Trim().Length -gt 0)
    {
        $errors.Add("CHANGELOG.md:$($i + 1) unexpected text in [Unreleased] (only a summary before the first heading, '### <Section>' headings and '- ' entries are kept)")
    }
    $previousBlank = $line.Trim().Length -eq 0
}

if ($errors.Count -gt 0)
{
    $errors | ForEach-Object { Write-Host "::error::$_" }
    Write-Host '::error::Nothing was written: fix CHANGELOG.md by hand, then run the script again.'
    exit 1
}

# Step 3: rebuild the [Unreleased] section: the summary, then the existing entries first and the fragments after, section by section
$section = [Collections.Generic.List[string]]::new()
$section.Add('## [Unreleased]')
$summaryText = ($summary -join "`n").Trim("`n")
if ($summaryText.Trim().Length -gt 0)
{
    $section.Add('')
    $section.Add($summaryText)
}
foreach ($name in $sections)
{
    $entries = @($existingEntries[$name]) + @($newEntries[$name])
    if ($entries.Count -eq 0) { continue }
    $section.Add('')
    $section.Add("### $name")
    $section.Add('')
    foreach ($entry in $entries) { $section.Add($entry) }
}

# The lines before and after the section; a PowerShell range a..b counts down when b < a, so an empty part is taken explicitly
$before = if ($start -gt 0) { @($lines[0..($start - 1)]) } else { @() }
$after = if ($end -lt $lines.Length) { @('') + @($lines[$end..($lines.Length - 1)]) } else { @() }
$result = @($before) + @($section) + @($after)

# Step 4: write the changelog, then delete the merged fragments; a fragment that cannot be deleted (open in an editor, for example) would be
# merged a second time by the next run, so the script stops with an error and names it
if ($PSCmdlet.ShouldProcess($changelogPath, "Merge $($fragments.Count) fragment(s) into [Unreleased] and delete them"))
{
    # Writing a temporary file next to the changelog, then replacing it: an interrupted write never leaves a truncated CHANGELOG.md
    $temporaryPath = "$changelogPath.tmp"
    try
    {
        [IO.File]::WriteAllText($temporaryPath, ((($result -join "`n").TrimEnd("`n")) -replace "`r?`n", "`r`n") + "`r`n", [Text.UTF8Encoding]::new($false))
        [IO.File]::Move($temporaryPath, $changelogPath, $true)
    }
    catch
    {
        Write-Host "::error::CHANGELOG.md could not be written: $($_.Exception.Message)"
        Write-Host '::error::Nothing was merged and no fragment was deleted; CHANGELOG.md is unchanged.'
        exit 1
    }
    $left = [Collections.Generic.List[string]]::new()
    foreach ($fragment in $fragments)
    {
        try { Remove-Item -LiteralPath $fragment.FullName -ErrorAction Stop }
        catch { $left.Add("$($fragment.Name): $($_.Exception.Message)") }
    }
    if ($left.Count -gt 0)
    {
        Write-Host "::error::CHANGELOG.md now holds the entries of every fragment, but $($left.Count) fragment(s) could not be deleted:"
        $left | ForEach-Object { Write-Host "::error::  $_" }
        Write-Host '::error::Delete them by hand and do not run the script again: it would merge them a second time.'
        exit 1
    }
    Write-Host "Merged $($fragments.Count) fragment(s) into the [Unreleased] section of CHANGELOG.md."
}

exit 0
