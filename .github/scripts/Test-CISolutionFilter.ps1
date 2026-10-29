# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ .github/scripts/Test-CISolutionFilter.ps1                    │
# │ Purpose      │ Fails when the CI solution filter no longer matches the      │
# │              │ solution, so that no project is silently left out of the     │
# │              │ CI build and tests.                                          │
# │ Rules        │ 1. Every .csproj under src/ and test/ is in the solution.    │
# │              │ 2. The filter lists exactly the .csproj of the solution.     │
# │              │ 3. The filter lists no .sqlproj (built in Visual Studio).    │
# │ Usage        │ pwsh .github/scripts/Test-CISolutionFilter.ps1               │
# │ Exit code    │ 0 when the filter matches, 1 otherwise.                      │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding()]
param(
    # Root of the repository; by default, two levels above this script
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

# Paths are compared in one form: relative, forward slashes (-in, -notin and -like ignore case)
function ConvertTo-NormalizedPath([string] $Path)
{
    return $Path.Replace('\', '/')
}

$solutionPath = Join-Path $RepositoryRoot 'BoardGamesPlatform.slnx'
$filterPath = Join-Path $RepositoryRoot 'BoardGamesPlatform.CI.slnf'

# Projects declared in the solution
[xml] $solution = Get-Content -Raw $solutionPath
$solutionProjects = @($solution.SelectNodes('//Project') | ForEach-Object { ConvertTo-NormalizedPath $_.Path })

# Projects kept by the CI filter
$filter = Get-Content -Raw $filterPath | ConvertFrom-Json
$filterProjects = @($filter.solution.projects | ForEach-Object { ConvertTo-NormalizedPath $_ })

# Projects present on disk
$diskProjects = @(
    foreach ($folder in 'src', 'test')
    {
        Get-ChildItem -Path (Join-Path $RepositoryRoot $folder) -Filter '*.csproj' -Recurse -File |
            ForEach-Object { ConvertTo-NormalizedPath ([IO.Path]::GetRelativePath($RepositoryRoot, $_.FullName)) }
    }
)

$solutionCsprojs = @($solutionProjects | Where-Object { $_ -like '*.csproj' })
$errors = [Collections.Generic.List[string]]::new()

# Rule 1: a project on disk but not in the solution is never built
foreach ($project in $diskProjects | Where-Object { $_ -notin $solutionProjects })
{
    $errors.Add("Not in BoardGamesPlatform.slnx: $project")
}

# Rule 2: a project of the solution missing from the filter is never built nor tested by the CI
foreach ($project in $solutionCsprojs | Where-Object { $_ -notin $filterProjects })
{
    $errors.Add("Not in BoardGamesPlatform.CI.slnf: $project")
}

# Rule 2 (reverse): the filter only lists projects of the solution
foreach ($project in $filterProjects | Where-Object { $_ -like '*.csproj' -and $_ -notin $solutionCsprojs })
{
    $errors.Add("In BoardGamesPlatform.CI.slnf but not in the solution: $project")
}

# Rule 3: dotnet build cannot build the SQL project (MSBuild format)
foreach ($project in $filterProjects | Where-Object { $_ -notlike '*.csproj' })
{
    $errors.Add("Only .csproj projects belong in BoardGamesPlatform.CI.slnf: $project")
}

if ($errors.Count -gt 0)
{
    $errors | ForEach-Object { Write-Host "::error::$_" }
    exit 1
}

Write-Host "BoardGamesPlatform.CI.slnf matches the solution ($($filterProjects.Count) projects)."
exit 0
