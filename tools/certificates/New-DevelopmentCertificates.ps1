# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/certificates/New-DevelopmentCertificates.ps1           │
# │ Purpose      │ Creates the certificates of a developer machine with the     │
# │              │ offline authority of the platform: a development root, then  │
# │              │ the PFX of the Gateway, the Back and the Very Back, and the  │
# │              │ signing keys of the Back and the Very Back (secure channel), │
# │              │ and sets their passwords in the user secrets of each tier.   │
# │ Rules        │ 1. Development only: the root, its key and the PFX are       │
# │              │    written in .certificates/dev (ignored by Git), never on a │
# │              │    server. A server gets its certificate from the real       │
# │              │    authority, by the steps of doc/adm/certificates.md.       │
# │              │ 2. An existing folder is refused, unless -Force: it is then  │
# │              │    deleted and created again, with a new root.               │
# │              │ 3. The passwords are drawn at random and given to the tool   │
# │              │    by its environment variables; the one of the PFX and the  │
# │              │    one of the signing keys (always distinct) go to the user  │
# │              │    secrets (piped, never on a command line), or to the       │
# │              │    console with -SkipUserSecrets.                            │
# │              │ 4. The tool is built before an existing folder is deleted.   │
# │ Usage        │ pwsh tools/certificates/New-DevelopmentCertificates.ps1      │
# │              │     [-Suffix bgp.internal] [-Force] [-SkipUserSecrets]       │
# │ Exit code    │ 0 on success, 1 when refused or failed.                      │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding()]
param(
    # Suffix of the deployment written in the names of the certificates; the same as Tls:IdentitySuffix in the settings of the tiers
    [string] $Suffix = 'bgp.internal',

    # Deletes and creates again an existing folder of development certificates
    [switch] $Force,

    # Writes the password of the PFX to the console instead of the user secrets, to set it some other way
    [switch] $SkipUserSecrets,

    # Root of the repository; by default, two levels above this script
    [string] $RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

# Step 1: the folder, refused when it exists unless -Force (checked first, deleted only once the tool is built)
$output = Join-Path $RepositoryRoot '.certificates\dev'
if ((Test-Path -LiteralPath $output) -and -not $Force)
{
    Write-Error "The folder $output already exists: run again with -Force to replace the development certificates" -ErrorAction Continue
    exit 1
}

# Step 2: the tool of the authority, built once, before anything is deleted, so that a failed build keeps the current certificates
$project = Join-Path $RepositoryRoot 'src\BoardGamesPlatform.CertificateAuthority\BoardGamesPlatform.CertificateAuthority.csproj'
dotnet build $project --configuration Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { Write-Error 'The certificate authority tool did not build' -ErrorAction Continue; exit 1 }
$tool = Join-Path $RepositoryRoot 'src\BoardGamesPlatform.CertificateAuthority\bin\Release\net10.0\BoardGamesPlatform.CertificateAuthority.dll'
if (Test-Path -LiteralPath $output)
{
    Remove-Item -LiteralPath $output -Recurse -Force
}
New-Item -ItemType Directory -Path $output | Out-Null
$authority = Join-Path $output 'ca'
$requests = Join-Path $output 'requests'

function Invoke-Authority
{
    # Runs one command of the tool, and stops at the first failure
    param([string[]] $Arguments)
    dotnet $tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "The command '$($Arguments[0])' of the authority failed" }
}

function New-Password
{
    # 32 random bytes, as Base64: about 43 characters
    return [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
}

# Step 3: random passwords, given to the tool through its environment variables only
$env:BOARDGAMESPLATFORM_CA_PASSWORD = New-Password
$env:BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD = New-Password
$env:BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD = New-Password

try
{
    # Step 4: the development root, then the request, the signature and the PFX of each role
    Invoke-Authority @('init', '--dir', $authority, '--name', 'BoardGamesPlatform Development Root CA', '--years', '2')
    foreach ($role in 'gateway', 'back', 'veryback')
    {
        Invoke-Authority @('request', '--dir', $requests, '--role', $role, '--suffix', $Suffix)
        Invoke-Authority @('sign', '--dir', $authority, '--request', (Join-Path $requests "$role.csr"), '--role', $role, '--suffix', $Suffix, '--out', (Join-Path $requests "$role.crt"))
        Invoke-Authority @('install', '--key', (Join-Path $requests "$role.key"), '--certificate', (Join-Path $requests "$role.crt"), '--authority', (Join-Path $authority 'platform-ca.crt'), '--out', (Join-Path $output "$role.pfx"))
    }
    Copy-Item -LiteralPath (Join-Path $authority 'platform-ca.crt') -Destination (Join-Path $output 'platform-ca.crt')
    Remove-Item -LiteralPath $requests -Recurse -Force

    # Step 5: the signing keys of the secure channel of the Back -> Very Back link, each public key being the file of its peer
    foreach ($role in 'back', 'veryback')
    {
        Invoke-Authority @('signing-key', '--role', $role, '--out', $output)
    }

    # Step 6: the passwords in the user secrets of each tier, read by its Development settings (or on the console when asked)
    if ($SkipUserSecrets)
    {
        Write-Host "Password of the PFX, to set as Tls:CertificatePassword of each tier: $env:BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD"
        Write-Host "Password of the signing keys, to set as Handshake:SigningKeyPassword of the Back and the Very Back: $env:BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD"
    }
    else
    {
        # The passwords are piped as JSON, never given on the command line (they would show in the list of the processes); the Gateway has
        # no secure channel, so no signing key
        foreach ($tier in 'Gateway', 'Back', 'VeryBack')
        {
            $values = [ordered]@{ 'Tls:CertificatePassword' = $env:BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD }
            if ($tier -ne 'Gateway') { $values['Handshake:SigningKeyPassword'] = $env:BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD }
            $tierProject = Join-Path $RepositoryRoot "src\BoardGamesPlatform.$tier\BoardGamesPlatform.$tier.csproj"
            foreach ($name in $values.Keys)
            {
                @{ $name = $values[$name] } | ConvertTo-Json -Compress | dotnet user-secrets set --project $tierProject | Out-Null
                if ($LASTEXITCODE -ne 0) { throw "The user secret $name of the $tier could not be set" }
            }
        }
    }
}
catch
{
    Write-Error $_ -ErrorAction Continue
    exit 1
}
finally
{
    # The passwords never stay in the session
    Remove-Item Env:\BOARDGAMESPLATFORM_CA_PASSWORD, Env:\BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD, Env:\BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD -ErrorAction SilentlyContinue
}

Write-Host "Development certificates of *.$Suffix written to $output."
exit 0
