# ┌──────────────┬──────────────────────────────────────────────────────────────┐
# │ File         │ tools/vault/Set-VaultSecret.ps1                              │
# │ Purpose      │ Writes one secret of a tier into its vault on Windows: a     │
# │              │ file of the vault folder, encrypted with DPAPI (scope        │
# │              │ LocalMachine, entropy bound to the name of the secret),      │
# │              │ readable by the account of the service only.                 │
# │ Rules        │ 0. Run as an administrator, after the service exists: the   │
# │              │    folder and the file belong to the Administrators.        │
# │              │ 1. The value is read from the standard input or asked with  │
# │              │    a hidden prompt, never taken from an argument (visible   │
# │              │    in the list of processes and in the history).            │
# │              │ 2. The folder and the file get a protected access list:     │
# │              │    full control for SYSTEM and the Administrators, read for │
# │              │    the account of the service, nothing for anyone else.     │
# │              │ 3. The file is written atomically: a temporary file of the  │
# │              │    folder, protected first, then renamed over the old one.  │
# │              │ 4. The value is never printed; one trailing line break of   │
# │              │    the standard input is dropped, any other control         │
# │              │    character is refused (the tier would refuse it too).     │
# │ Usage        │ 'secret' | pwsh tools/vault/Set-VaultSecret.ps1 `           │
# │              │     -VaultPath C:\ProgramData\BoardGamesPlatform\VeryBack `  │
# │              │     -Name Tls:CertificatePassword `                          │
# │              │     -Account 'NT SERVICE\BoardGamesPlatform.VeryBack'        │
# │ Exit code    │ 0 on success, 1 when refused or failed.                      │
# └──────────────┴──────────────────────────────────────────────────────────────┘

[CmdletBinding()]
param(
    # Folder of the vault of the tier (its setting Vault:Path), an absolute path; created when missing
    [Parameter(Mandatory)]
    [string] $VaultPath,

    # Configuration key of the secret (Tls:CertificatePassword) or its name in the vault (Tls__CertificatePassword)
    [Parameter(Mandatory)]
    [string] $Name,

    # Account of the service that reads the secret, for example 'NT SERVICE\BoardGamesPlatform.VeryBack'
    [Parameter(Mandatory)]
    [string] $Account
)

$ErrorActionPreference = 'Stop'

# Step 1: the checks, before anything is read or written
$vaultName = $Name.Replace(':', '__')
if ($vaultName -cnotmatch '^[A-Za-z][A-Za-z0-9_]*\z')
{
    Write-Error "Refused: '$Name' is not the key of a secret (letters, digits, ':' or '__' only)." -ErrorAction Continue
    exit 1
}
if (-not [System.IO.Path]::IsPathFullyQualified($VaultPath))
{
    Write-Error "Refused: the folder of the vault must be an absolute path (setting Vault:Path)." -ErrorAction Continue
    exit 1
}

$clear = $null
$encrypted = $null
$temporary = $null
try
{
    Add-Type -AssemblyName System.Security.Cryptography.ProtectedData

    # Step 2: the accounts of the access list (an unknown account fails here, before the value is asked)
    $system = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::LocalSystemSid, $null)
    $administrators = New-Object System.Security.Principal.SecurityIdentifier([System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid, $null)
    $service = (New-Object System.Security.Principal.NTAccount($Account)).Translate([System.Security.Principal.SecurityIdentifier])
    $isElevated = (New-Object System.Security.Principal.WindowsPrincipal([System.Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole($administrators)
    if (-not $isElevated)
    {
        # The access list gives the account of the service read only: a caller that is not an administrator could not write the file
        throw 'Run this tool as an administrator: the vault belongs to SYSTEM and the Administrators, the service only reads it.'
    }

    # Step 3: the value, from the standard input or a hidden prompt
    if ([Console]::IsInputRedirected)
    {
        # Read as UTF-8, whatever the code page of the console (a non-ASCII password would be altered otherwise)
        $reader = [System.IO.StreamReader]::new([Console]::OpenStandardInput(), [System.Text.UTF8Encoding]::new($false))
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($text.EndsWith("`r`n")) { $text = $text.Substring(0, $text.Length - 2) }
        elseif ($text.EndsWith("`n")) { $text = $text.Substring(0, $text.Length - 1) }
    }
    else
    {
        $secure = Read-Host -AsSecureString -Prompt "Value of $vaultName"
        $text = [System.Net.NetworkCredential]::new('', $secure).Password
    }
    if ([string]::IsNullOrEmpty($text) -or [regex]::IsMatch($text, '[\p{Cc}\p{Cf}]'))
    {
        throw "The value of $vaultName is empty or holds a control or format character: the tier would refuse it."
    }

    # Step 4: the encryption, bound to the name of the secret
    $clear = [System.Text.Encoding]::UTF8.GetBytes($text)
    $text = $null
    $entropy = [System.Text.Encoding]::UTF8.GetBytes("BoardGamesPlatform.Vault:$vaultName")
    $encrypted = [System.Security.Cryptography.ProtectedData]::Protect($clear, $entropy, [System.Security.Cryptography.DataProtectionScope]::LocalMachine)

    # Step 5: the folder, with its protected access list (the account of the service may list it)
    $folderSecurity = New-Object System.Security.AccessControl.DirectorySecurity
    $folderSecurity.SetAccessRuleProtection($true, $false)
    $inherit = [System.Security.AccessControl.InheritanceFlags]'ContainerInherit, ObjectInherit'
    foreach ($trusted in @($system, $administrators))
    {
        $folderSecurity.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($trusted, 'FullControl', $inherit, 'None', 'Allow')))
    }
    $folderSecurity.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($service, 'ReadAndExecute', 'None', 'None', 'Allow')))
    $folderSecurity.SetOwner($administrators)
    $folder = New-Object System.IO.DirectoryInfo($VaultPath)
    if (-not $folder.Exists)
    {
        # Created with its protection in one call: never visible with the inherited rights of its parent (create the parents as an administrator)
        [System.IO.FileSystemAclExtensions]::Create($folder, $folderSecurity)
    }
    else
    {
        # An existing folder must be a real folder that holds secrets only: anything else is left to the administrator to look at
        if ($folder.Attributes.HasFlag([System.IO.FileAttributes]::ReparsePoint)) { throw "The folder $VaultPath is a junction or a link: it must be a real folder." }
        $foreign = @($folder.EnumerateFileSystemInfos() | Where-Object { $_ -is [System.IO.DirectoryInfo] -or $_.Name -cnotmatch '^[A-Za-z][A-Za-z0-9_]*\z' })
        if ($foreign.Count -gt 0) { throw "The folder $VaultPath holds something else than secrets ($($foreign[0].Name)): remove it first." }
        [System.IO.FileSystemAclExtensions]::SetAccessControl($folder, $folderSecurity)
    }

    # Step 6: a temporary file, protected before it holds anything, then renamed over the secret
    $fileSecurity = New-Object System.Security.AccessControl.FileSecurity
    $fileSecurity.SetAccessRuleProtection($true, $false)
    foreach ($trusted in @($system, $administrators))
    {
        $fileSecurity.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($trusted, 'FullControl', 'Allow')))
    }
    $fileSecurity.AddAccessRule((New-Object System.Security.AccessControl.FileSystemAccessRule($service, 'Read', 'Allow')))
    $fileSecurity.SetOwner($administrators)
    $temporary = Join-Path $VaultPath ".writing-$([guid]::NewGuid().ToString('N'))"
    $stream = [System.IO.FileSystemAclExtensions]::Create((New-Object System.IO.FileInfo($temporary)), 'CreateNew', 'Write', 'None', 4096, 'None', $fileSecurity)
    try
    {
        # Written to the disk before the rename, so that a power loss never leaves a renamed but empty secret
        $stream.Write($encrypted, 0, $encrypted.Length)
        $stream.Flush($true)
    }
    finally { $stream.Dispose() }
    [System.IO.File]::Move($temporary, (Join-Path $VaultPath $vaultName), $true)
    $temporary = $null

    Write-Host "Secret $vaultName written to the vault $VaultPath (readable by $Account)."
}
catch
{
    Write-Error $_.Exception.Message -ErrorAction Continue
    exit 1
}
finally
{
    # Erasing the clear bytes, and removing a temporary file left by a failure
    if ($clear) { [System.Security.Cryptography.CryptographicOperations]::ZeroMemory($clear) }
    if ($temporary -and (Test-Path -LiteralPath $temporary)) { Remove-Item -LiteralPath $temporary -Force }
}

exit 0
