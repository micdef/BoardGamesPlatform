# Certificate tools

## New-DevelopmentCertificates.ps1

Creates the certificates of a developer machine with the offline authority of the platform (`src/BoardGamesPlatform.CertificateAuthority`): a development root, then the PFX of the Gateway, the Back and the Very Back, in `.certificates/dev` (ignored by Git). The password of the PFX, drawn at random, is set as `Tls:CertificatePassword` in the user secrets of each tier (piped to `dotnet user-secrets set`, never given on a command line); the tool is built before an existing folder is replaced, so that a failed build keeps the current certificates; the `appsettings.Development.json` of each tier already points to these files.

```powershell
pwsh tools/certificates/New-DevelopmentCertificates.ps1
pwsh tools/certificates/New-DevelopmentCertificates.ps1 -Force            # replaces the development certificates, with a new root
pwsh tools/certificates/New-DevelopmentCertificates.ps1 -SkipUserSecrets  # writes the password to the console instead
```

| Parameter | Default | Meaning |
|---|---|---|
| `-Suffix` | `bgp.internal` | Suffix of the names of the certificates, the same as `Tls:IdentitySuffix` |
| `-Force` | off | Deletes and creates again an existing `.certificates/dev` |
| `-SkipUserSecrets` | off | Writes the password to the console instead of the user secrets |

Development only: a server gets its certificate from the real authority, by the steps of [doc/adm/certificates.md](../../doc/adm/certificates.md).
