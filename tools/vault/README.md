# Vault tools

## Set-VaultSecret.ps1

Writes one secret of a tier into its vault on **Windows**: one file of the vault folder (setting `Vault:Path` of the tier), encrypted with DPAPI, scope `LocalMachine`, bound to the name of the secret.
On Fedora the vault is the credentials of systemd: see [configuration.md](../../doc/adm/configuration.md#fedora-credentials-of-systemd).

### Rules

- Run it **as an administrator**, after the service of the tier exists (the account of the service must be known): it refuses otherwise. Create the parent folders of the vault as an administrator too: a parent made by another user could later be renamed by that user.
- Prerequisite: PowerShell 7 on the latest .NET patch (`[System.Runtime.InteropServices.RuntimeInformation]::FrameworkDescription`): PowerShell ships its own runtime, so the runtime of the machine does not update it (PowerShell 7.6.9 runs on .NET 10.0.14).
- The value is read from the standard input (as UTF-8), or asked with a hidden prompt; never from an argument (visible in the list of processes and in the history).
- One trailing line break of the standard input is dropped; any other control or format character (a byte order mark) is refused, as the tier would refuse it.
- A new folder is created with its protected access list in one call; an existing folder must be a real folder (no junction) that holds secrets only, or the tool stops.
- The folder and the file get a protected access list (no inheritance): owner the Administrators, full control for SYSTEM and the Administrators, read for the account of the service, nothing for anyone else.
- The file is written atomically: a temporary file of the folder, protected before it holds anything, written to the disk, then renamed over the old secret.
- The value is never printed.

### Parameters

| Parameter | Meaning |
|---|---|
| `-VaultPath` | Folder of the vault of the tier, an absolute path; created when missing |
| `-Name` | Key of the secret (`Tls:CertificatePassword`) or its name in the vault (`Tls__CertificatePassword`) |
| `-Account` | Account of the service that reads the secret, for example `NT SERVICE\BoardGamesPlatform.VeryBack` |

Exit code: `0` on success, `1` when refused or failed.

### Example

```powershell
pwsh tools/vault/Set-VaultSecret.ps1 -VaultPath C:\ProgramData\BoardGamesPlatform\VeryBack\Vault -Name Tls:CertificatePassword -Account 'NT SERVICE\BoardGamesPlatform.VeryBack'
```

The tests of `test/BoardGamesPlatform.Vault.Tests` run the tool and read what it wrote with the vault of the tiers; that test needs an elevated session, and is skipped otherwise.
