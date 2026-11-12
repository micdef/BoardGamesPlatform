# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/database/Deploy-TestDatabase.ps1                       │
# │ Purpose      │ Builds the SQL project into a DACPAC and deploys a DEV_ or   │
# │              │ TEST_ database from scratch, dropping and recreating it each │
# │              │ time, with its application user: a login named after the     │
# │              │ database, a random password, member of ApplicationRole.      │
# │ Rules        │ 1. The database name must match ^(DEV|TEST)_[A-Za-z0-9_]+$   │
# │              │    (case-sensitive); anything else is refused before any     │
# │              │    build or connection. The login has the same name: no      │
# │              │    other login is ever dropped.                              │
# │              │ 2. Windows authentication only; the server is the local      │
# │              │    default instance unless -ServerInstance says otherwise.   │
# │              │ 3. Nothing is installed: MSBuild and SqlPackage must come    │
# │              │    with Visual Studio, sqlcmd (ODBC 18) with SQL Server.     │
# │              │ 4. The password is never printed, never on a command line,   │
# │              │    never in a file in clear: it goes to SQL Server on the    │
# │              │    standard input of sqlcmd, then to -SecretTarget (the      │
# │              │    user secrets of UserSecrets are plain JSON by design).    │
# │ Usage        │ pwsh tools/database/Deploy-TestDatabase.ps1 `                │
# │              │     -DatabaseName DEV_BoardGamesPlatform `                    │
# │              │     [-SecretTarget None|UserSecrets|Vault] [-WhatIf]         │
# │ Exit code    │ 0 on success, 1 when refused or failed.                      │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding(SupportsShouldProcess)]
param(
    # Database to drop and recreate; must start with DEV_ or TEST_ (upper case); also the name of its application login and user
    [Parameter(Mandatory)]
    [AllowEmptyString()]
    [string] $DatabaseName,

    # SQL Server instance; by default the local default instance (MSSQLSERVER)
    [string] $ServerInstance = 'localhost',

    # Reuses the DACPAC of a previous build instead of building it again
    [switch] $SkipBuild,

    # Where the connection string of the application user goes: nowhere (the SQL tests play the user with EXECUTE AS), the user secrets
    # of the Very Back (a developer machine), or the DPAPI vault of the Very Back (-VaultPath, -VaultAccount; as an administrator)
    [ValidateSet('None', 'UserSecrets', 'Vault')]
    [string] $SecretTarget = 'None',

    # Folder of the vault of the Very Back (its setting Vault:Path), for -SecretTarget Vault
    [string] $VaultPath,

    # Account of the service of the Very Back, for -SecretTarget Vault, for example 'NT SERVICE\BoardGamesPlatform.VeryBack'
    [string] $VaultAccount,

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
if ($SecretTarget -eq 'Vault' -and ([string]::IsNullOrWhiteSpace($VaultPath) -or [string]::IsNullOrWhiteSpace($VaultAccount)))
{
    Write-Error 'Refused: -SecretTarget Vault needs -VaultPath and -VaultAccount.' -ErrorAction Continue
    exit 1
}

# Runs a T-SQL batch given on the standard input of sqlcmd (never on its command line, never in a file), failing on the first error
function Invoke-Batch([string] $Sqlcmd, [string[]] $ConnectionArguments, [string] $Batch)
{
    $output = $Batch | & $Sqlcmd @ConnectionArguments -b -h -1 -W 2>&1
    if ($LASTEXITCODE -ne 0)
    {
        # The messages of SQL Server name the statement, not the password: the batch itself is never printed
        throw "sqlcmd failed (exit code $LASTEXITCODE): $($output -join ' ')"
    }
}

$password = $null
$published = $false
$connectionString = $null
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

    # Step 3: find SqlPackage.exe among what Visual Studio already ships, and sqlcmd among what SQL Server ships; never install them
    $installationPath = & $vswhere -latest -property installationPath | Select-Object -First 1
    $sqlPackageFolder = if ($installationPath) { Join-Path $installationPath 'Common7\IDE\Extensions\Microsoft\SQLDB\DAC' } else { $null }
    $sqlPackage = if ($sqlPackageFolder) { Join-Path $sqlPackageFolder 'SqlPackage.exe' } else { $null }
    if (-not $sqlPackage -or -not (Test-Path $sqlPackage))
    {
        throw "SqlPackage.exe not found. Looked in: '$sqlPackageFolder' (Visual Studio installation found by vswhere). Nothing is installed by this script."
    }
    $sqlcmd = (Get-Command sqlcmd -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1).Source
    if (-not $sqlcmd)
    {
        throw 'sqlcmd not found in the PATH (Microsoft ODBC Driver 18 tools, installed with SQL Server). Nothing is installed by this script.'
    }

    # Only the sqlcmd of the ODBC driver: another one (the sqlcmd written in Go) does not check the certificate of a remote server by default
    $sqlcmdBanner = (& $sqlcmd -? 2>&1 | Select-Object -First 1) -as [string]
    if ($sqlcmdBanner -notmatch '^Microsoft \(R\) SQL Server Command Line Tool')
    {
        throw "The first sqlcmd of the PATH ('$sqlcmd') is not the tool of the Microsoft ODBC Driver: put that tool first in the PATH."
    }

    # A local SQL Server presents a self-signed certificate, which the default encrypted connection refuses.
    # Trusting it is accepted for the local machine only; any other server keeps the strict default, an encryption required (-Nm).
    $isLocalServer = $ServerInstance -in 'localhost', '.', '(local)', '127.0.0.1'
    $trustArgument = if ($isLocalServer) { '/TargetTrustServerCertificate:True' } else { '/TargetTrustServerCertificate:False' }
    $sqlcmdConnection = @('-S', $ServerInstance, '-E', '-d', 'master')
    $sqlcmdConnection += if ($isLocalServer) { '-C' } else { '-Nm' }

    if ($PSCmdlet.ShouldProcess("$ServerInstance / $DatabaseName", 'Drop and recreate the database from the DACPAC, and its application login and user'))
    {
        if (-not (Test-Path $dacpacPath))
        {
            throw "DACPAC not found: '$dacpacPath'. Run without -SkipBuild."
        }

        # A DACPAC older than the sources of the project would deploy something else than the code: refused with -SkipBuild
        $projectFolder = Split-Path $projectPath -Parent
        $newestSource = Get-ChildItem -Path $projectFolder -Recurse -File -Include '*.sql', '*.sqlproj', '*.xml' |
            Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($newestSource -and (Get-Item $dacpacPath).LastWriteTimeUtc -lt $newestSource.LastWriteTimeUtc)
        {
            throw "The DACPAC is older than $($newestSource.Name): run without -SkipBuild."
        }

        # Step 4: drop and recreate the database from the DACPAC (CreateNewDatabase=True)
        & $sqlPackage /Action:Publish "/SourceFile:$dacpacPath" "/TargetServerName:$ServerInstance" "/TargetDatabaseName:$DatabaseName" /p:CreateNewDatabase=True $trustArgument
        if ($LASTEXITCODE -ne 0)
        {
            throw "SqlPackage failed (exit code $LASTEXITCODE)."
        }
        $published = $true

        # Step 5: drop the old login of the same name (the guard checked the name: never another login), after closing its sessions
        Invoke-Batch $sqlcmd $sqlcmdConnection @"
SET NOCOUNT ON;
DECLARE @session INT, @kill NVARCHAR(32);
DECLARE sessions CURSOR LOCAL FAST_FORWARD FOR SELECT [session_id] FROM [sys].[dm_exec_sessions] WHERE [login_name] = N'$DatabaseName';
OPEN sessions;
FETCH NEXT FROM sessions INTO @session;
WHILE @@FETCH_STATUS = 0
BEGIN
    SET @kill = CONCAT(N'KILL ', @session, N';');
    EXEC (@kill);
    FETCH NEXT FROM sessions INTO @session;
END;
CLOSE sessions;
DEALLOCATE sessions;
DECLARE @sql NVARCHAR(MAX) = N'DROP LOGIN ' + QUOTENAME(N'$DatabaseName') + N';';
IF SUSER_ID(N'$DatabaseName') IS NOT NULL EXEC (@sql);
"@

        # Step 6: create the login with a new random password (32 bytes, Base64: letters, digits and + / =, never a quote nor a $(variable) of sqlcmd),
        # drawn again if the password policy refuses it, then its user, its role, and the DENY VIEW ANY DATABASE
        $created = $false
        for ($attempt = 1; $attempt -le 3 -and -not $created; $attempt++)
        {
            $password = [System.Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
            try
            {
                Invoke-Batch $sqlcmd $sqlcmdConnection @"
SET NOCOUNT ON;
DECLARE @sql NVARCHAR(MAX) = N'CREATE LOGIN ' + QUOTENAME(N'$DatabaseName') + N' WITH PASSWORD = N''$password'', DEFAULT_DATABASE = '
    + QUOTENAME(N'$DatabaseName') + N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;';
EXEC (@sql);
SET @sql = N'DENY VIEW ANY DATABASE TO ' + QUOTENAME(N'$DatabaseName') + N';';
EXEC (@sql);
"@
                $created = $true
            }
            catch
            {
                # A refusal of the password policy (error 15118) draws a new password; any other error stops the deployment
                if ($_.Exception.Message -notmatch '15118|Password validation failed' -or $attempt -eq 3) { throw }
            }
        }
        Invoke-Batch $sqlcmd $sqlcmdConnection @"
SET NOCOUNT ON;
USE [$DatabaseName];
DECLARE @sql NVARCHAR(MAX) = N'CREATE USER ' + QUOTENAME(N'$DatabaseName') + N' FOR LOGIN ' + QUOTENAME(N'$DatabaseName') + N';';
EXEC (@sql);
SET @sql = N'ALTER ROLE [ApplicationRole] ADD MEMBER ' + QUOTENAME(N'$DatabaseName') + N';';
EXEC (@sql);
"@

        # Step 7: the connection string of the application user, encrypted; the certificate of a local server only is trusted
        $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
        $builder['Server'] = $ServerInstance
        $builder['Database'] = $DatabaseName
        $builder['User ID'] = $DatabaseName
        $builder['Password'] = $password
        $builder['Encrypt'] = 'Mandatory'
        if ($isLocalServer) { $builder['TrustServerCertificate'] = 'True' }
        $connectionString = $builder.ConnectionString

        # Step 8: send it where -SecretTarget says, on a standard input only
        switch ($SecretTarget)
        {
            'UserSecrets'
            {
                $json = @{ 'ConnectionStrings:Main' = $connectionString } | ConvertTo-Json -Compress
                $veryBack = Join-Path $RepositoryRoot 'src\BoardGamesPlatform.VeryBack'
                $json | dotnet user-secrets set --project $veryBack | Out-Null
                $json = $null
                if ($LASTEXITCODE -ne 0) { throw "dotnet user-secrets failed (exit code $LASTEXITCODE)." }
                Write-Host 'Connection string of the application user written to the user secrets of the Very Back (ConnectionStrings:Main).'
            }
            'Vault'
            {
                $connectionString | pwsh -NoProfile -File (Join-Path $PSScriptRoot '..\vault\Set-VaultSecret.ps1') -VaultPath $VaultPath -Name 'ConnectionStrings:Main' -Account $VaultAccount
                if ($LASTEXITCODE -ne 0) { throw "Set-VaultSecret.ps1 failed (exit code $LASTEXITCODE)." }
            }
            default
            {
                Write-Host 'The password of the application user is kept nowhere (-SecretTarget None): the SQL tests play the user with EXECUTE AS.'
                Write-Host 'A connection string written by an earlier run (user secrets or vault of the Very Back) no longer works: the password is new.'
            }
        }

        Write-Host "Database '$DatabaseName' dropped and recreated on '$ServerInstance', with its application login and user '$DatabaseName'."
    }
}
catch
{
    Write-Error $_.Exception.Message -ErrorAction Continue
    if ($published)
    {
        # The database exists, but its application user, or the place of its connection string, may not: never let that pass unnoticed
        Write-Error "The database '$DatabaseName' is deployed but incomplete (its application user or its connection string): run the script again." -ErrorAction Continue
    }
    exit 1
}
finally
{
    # Dropping the references to the password (a .NET string cannot be erased: it stays in memory until the process ends, which is short)
    $password = $null
    $connectionString = $null
    $builder = $null
}

exit 0
