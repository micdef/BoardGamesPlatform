# Vault tools

## Set-VaultSecret.ps1

Writes one secret of a tier into its vault on **Windows**: one file of the vault folder (setting `Vault:Path` of the tier), encrypted with DPAPI, scope `LocalMachine`, bound to the name of the secret.
On Fedora the vault is the credentials of systemd: see [configuration.md](../../doc/adm/configuration.md#fedora-credentials-of-systemd).

### Rules

- Run it **as an administrator**, after the service of the tier exists (the account of the service must be known): it refuses otherwise.
- The value is read from the standard input, or asked with a hidden prompt; never from an argument (visible in the list of processes and in the history).
- One trailing line break of the standard input is dropped; any other control character is refused, as the tier would refuse it.
- The folder and the file get a protected access list (no inheritance): owner the Administrators, full control for SYSTEM and the Administrators, read for the account of the service, nothing for anyone else.
- The file is written atomically: a temporary file of the folder, protected before it holds anything, then renamed over the old secret.
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
