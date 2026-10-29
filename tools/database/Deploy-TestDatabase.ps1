# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/database/Deploy-TestDatabase.ps1                       │
# │ Purpose      │ Builds the SQL project into a DACPAC and deploys it to a     │
# │              │ throwaway DEV_ or TEST_ database, dropping and recreating    │
# │              │ the database each time, so that the SQL tests start from a   │
# │              │ known state.                                                 │
# │ Rules        │ 1. The database name must match ^(DEV|TEST)_[A-Za-z0-9_]+$   │
# │              │    (case-sensitive); anything else is refused before any     │
# │              │    build or connection.                                      │
# │              │ 2. Windows authentication only; the server is the local      │
# │              │    default instance unless -ServerInstance says otherwise.   │
# │              │ 3. Nothing is installed: MSBuild and SqlPackage must already │
# │              │    come with Visual Studio.                                  │
# │ Usage        │ pwsh tools/database/Deploy-TestDatabase.ps1 `                │
# │              │     -DatabaseName TEST_BoardGamesPlatform [-WhatIf]          │
# │ Exit code    │ 0 on success, 1 when refused or failed.                      │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding(SupportsShouldProcess)]
param(
    # Database to drop and recreate; must start with DEV_ or TEST_ (upper case)
    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $DatabaseName,

    # SQL Server instance; by default the local default instance (MSSQLSERVER)
    [string] $ServerInstance = 'localhost',

    # Reuses the DACPAC of a previous build instead of building it again
    [switch] $SkipBuild,

    # Root of the repository; by default, two levels above this script
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

# Step 1: the guard, before anything is built or connected to.
# \z (not $) so that a trailing line break cannot slip through; -cnotmatch is case-sensitive.
if ($DatabaseName -cnotmatch '^(DEV|TEST)_[A-Za-z0-9_]+\z')
{
    Write-Error "Refused: '$DatabaseName' is not a valid throwaway database name. It must match ^(DEV|TEST)_[A-Za-z0-9_]+`$ (upper-case prefix)." -ErrorAction Continue
    exit 1
}

try
{
    $projectPath = Join-Path $RepositoryRoot 'src\BoardGamesPlatform.Database\BoardGamesPlatform.Database.sqlproj'
    $dacpacPath = Join-Path $RepositoryRoot 'src\BoardGamesPlatform.Database\bin\Release\BoardGamesPlatform.Database.dacpac'
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'

    if (-not (Test-Path $vswhere))
    {
        throw "vswhere.exe not found at '$vswhere'. Visual Studio must be installed."
    }

    # Step 2: build the SQL project into a DACPAC (Release), without touching the project file
    if (-not $SkipBuild)
    {
        $msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if (-not $msbuild)
        {
            throw 'MSBuild.exe not found by vswhere (component Microsoft.Component.MSBuild).'
        }

        if ($PSCmdlet.ShouldProcess($projectPath, 'Build into a DACPAC (Release)'))
        {
            & $msbuild $projectPath /p:Configuration=Release /restore /nologo /verbosity:minimal
            if ($LASTEXITCODE -ne 0)
            {
                throw "MSBuild failed (exit code $LASTEXITCODE)."
            }
        }
    }

    # Step 3: find SqlPackage.exe among what Visual Studio already ships; never install it
    $installationPath = & $vswhere -latest -property installationPath | Select-Object -First 1
    $sqlPackageFolder = if ($installationPath) { Join-Path $installationPath 'Common7\IDE\Extensions\Microsoft\SQLDB\DAC' } else { $null }
    $sqlPackage = if ($sqlPackageFolder) { Join-Path $sqlPackageFolder 'SqlPackage.exe' } else { $null }
    if (-not $sqlPackage -or -not (Test-Path $sqlPackage))
    {
        throw "SqlPackage.exe not found. Looked in: '$sqlPackageFolder' (Visual Studio installation found by vswhere). Nothing is installed by this script."
    }

    # Step 4: drop and recreate the database from the DACPAC (CreateNewDatabase=True)
    if ($PSCmdlet.ShouldProcess("$ServerInstance / $DatabaseName", 'Drop and recreate the database from the DACPAC'))
    {
        if (-not (Test-Path $dacpacPath))
        {
            throw "DACPAC not found: '$dacpacPath'. Run without -SkipBuild."
        }

        # A local SQL Server presents a self-signed certificate, which the default encrypted connection refuses.
        # Trusting it is accepted for the local machine only; any other server keeps the strict default.
        $isLocalServer = $ServerInstance -in 'localhost', '.', '(local)', '127.0.0.1'
        $trustArgument = if ($isLocalServer) { '/TargetTrustServerCertificate:True' } else { '/TargetTrustServerCertificate:False' }

        & $sqlPackage /Action:Publish "/SourceFile:$dacpacPath" "/TargetServerName:$ServerInstance" "/TargetDatabaseName:$DatabaseName" /p:CreateNewDatabase=True $trustArgument
        if ($LASTEXITCODE -ne 0)
        {
            throw "SqlPackage failed (exit code $LASTEXITCODE)."
        }

        Write-Host "Database '$DatabaseName' dropped and recreated on '$ServerInstance'."
    }
}
catch
{
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}

exit 0
