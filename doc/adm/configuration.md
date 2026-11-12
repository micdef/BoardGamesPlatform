# Configuration and secrets

How every executable of the platform (Back, Very Back, Gateway, website) reads its settings, and where its secrets live.
From 0.7.0, the secrets of the servers live in the vault of the operating system of each tier (DPAPI on Windows, `systemd-creds` on Fedora); its design and threat model are in [database.md](../dev/database.md#the-vault).

## Rules

1. **No secret in a file of the repository**, in any environment. A secret written in a commit stays in the history, even after it is removed.
   A secret key may appear in a committed `appsettings*.json` file to document it, but its value stays **empty**.
2. **The executable refuses to start with a missing or invalid setting** (fail closed). It never falls back to a default value for a secret.
   The `Observability` section already works this way: an invalid collector address stops the startup.
3. **A secret value is never written to a log**, at any level, Debug included. Error messages name the setting, never its value.
4. **Only the Very Back declares database connection strings** (`ConnectionStrings` section): no other tier can even be configured to reach the database.
5. **Certificates and keys are files outside the repository**: the PFX of a tier and the root key of the authority are never committed (`*.pfx`, `*.key`, `*.csr` and `.certificates/` are ignored by Git). Only their paths are settings; the password of a PFX is a secret like any other (`Tls:CertificatePassword`). See [certificates.md](certificates.md).

Rules 1 and 4 are checked by the CI ([ConfigurationFileTests.cs](../../test/BoardGamesPlatform.Architecture.Tests/ConfigurationFileTests.cs)).
The CI reads the `appsettings*.json` files and the `Properties/launchSettings.json` file of every project.
A key is treated as a secret when one of its segments ends with `Password`, `Pwd`, `Secret`, `ApiKey`, `Token`, `PrivateKey` or `ConnectionString`,
plural included (for example `Smtp:Password`, `Smtp:Passwords:0`, `ConnectionStrings:Main`); its text value must then be empty.
Name every secret setting accordingly, so that the CI protects it.

## Configuration sources

Each executable uses the default configuration of .NET, plus the vault of the platform (`BoardGamesPlatform.Vault`) for the Gateway, the Back and the Very Back.
The sources are read in this order; **a later source overrides an earlier one** for the same key ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0#default-app-configuration-sources)):

| Order | Source | Committed | Holds |
|:-----:|--------|:---------:|-------|
| 1 | `appsettings.json` | ✅ | Defaults of every environment; secret keys with an empty value |
| 2 | `appsettings.{Environment}.json` (for example `appsettings.Development.json`) | ✅ | What differs per environment, never a secret |
| 3 | User secrets, in the `Development` environment only | ❌ | The secrets of a developer machine |
| 4 | Environment variables | ❌ | Settings of a server that are not secrets (the folder of the vault on Windows, `Vault__Path`) |
| 5 | Command-line arguments | ❌ | One-off overrides, never a secret (visible in the process list) |
| 6 | **The vault** (last: it wins over every other source) | ❌ | The secrets of the acceptance and production servers |

**Outside the `Development` environment, a secret of a tier may come from the vault only**: the tier refuses to start when one of its secrets (`ConnectionStrings:Main`, `Tls:CertificatePassword`, `Handshake:SigningKeyPassword`, depending on the tier) has a value in a JSON file, an environment variable of any prefix (the `SQLCONNSTR_` form of the connection strings included) or the command line. The message names the secret and the source, never the value. In `Development` the check is off, and the tier logs a warning at startup (event 9071), so that a server set to Development by mistake is visible in its journal.

The environment is set by the `DOTNET_ENVIRONMENT` variable, for every executable. The Gateway and the website also read `ASPNETCORE_ENVIRONMENT`,
but `DOTNET_ENVIRONMENT` takes precedence over it ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)).
Without either variable, an executable runs as `Production`. The name of the acceptance environment is chosen with the first acceptance deployment.

## Development: user secrets

The secrets of a developer machine are stored with the Secret Manager of .NET, outside the repository:

```powershell
dotnet user-secrets init --project src/BoardGamesPlatform.VeryBack
dotnet user-secrets set "ConnectionStrings:Main" "<connection string>" --project src/BoardGamesPlatform.VeryBack
```

- `init` adds a `UserSecretsId` to the project file; it is done once per project, in the pull request that brings its first secret. The Gateway, the Back and the Very Back have one.
- The password of the development certificates (`Tls:CertificatePassword`) is set in the user secrets of the three tiers by `tools/certificates/New-DevelopmentCertificates.ps1`. To remove it: `dotnet user-secrets remove "Tls:CertificatePassword" --project <project of the tier>`.
- The password of the development signing keys of the secure channel (`Handshake:SigningKeyPassword`, always distinct from the one of the PFX) is set by the same script in the user secrets of the Back and the Very Back; it is removed the same way.
- The values are stored **unencrypted** in a JSON file of the user profile (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows,
  `~/.microsoft/usersecrets/<id>/secrets.json` on Linux). They are for development only: never put a secret of an acceptance or production server there
  ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0#use-the-secret-manager-tool)).

## Acceptance and production: the vault (from 0.7.0)

Each secret is stored encrypted by the operating system, under its name in the vault: the key `Section:Key` with `__` for `:`, for example `ConnectionStrings__Main`, `Tls__CertificatePassword` or `Handshake__SigningKeyPassword`.
A secret is read once, when the tier starts: after a change, restart the service.
The environment variables of the secrets used until 0.6.0 (`Tls__CertificatePassword`, `Handshake__SigningKeyPassword`, `ConnectionStrings__Main`) must be removed from the services: a tier refuses to start while one of them is set.

| Tier | Secrets |
|------|---------|
| Gateway | `Tls__CertificatePassword` |
| Back | `Tls__CertificatePassword`, `Handshake__SigningKeyPassword` |
| Very Back | `Tls__CertificatePassword`, `Handshake__SigningKeyPassword`, `ConnectionStrings__Main` |

A secret is UTF-8 text, without any control character: a line break left by an editor or by `echo` is refused, naming the secret.
A file or a credential of the vault whose name is not a secret of the tier stops it too.

### Fedora: credentials of systemd

Each tier runs under **its own system user** (or `DynamicUser=yes`): the credentials of a unit are readable by root and by the user of the unit, so two tiers must never share a user.
Encrypt each secret once, as root, without a line break (`systemd-ask-password -n` asks it hidden; `umask 077` keeps the encrypted file private):

```bash
umask 077
systemd-ask-password -n "Password of the PFX of the Very Back:" | systemd-creds encrypt --name=Tls__CertificatePassword - /etc/credstore.encrypted/Tls__CertificatePassword
```

Then name it in the unit of the tier, one line per secret:

```ini
[Service]
LoadCredentialEncrypted=Tls__CertificatePassword:/etc/credstore.encrypted/Tls__CertificatePassword
```

systemd decrypts it at the start of the service into a read-only, non-swappable folder, named in the variable `CREDENTIALS_DIRECTORY`, which the tier reads; the name embedded at the encryption is checked, so a file copied under another name is refused.
The key is the one of the host (`/var/lib/systemd/credential.secret`), and of the TPM2 when the host has one (`--with-key=host+tpm2`, recommended, a vTPM for a virtual machine): without a TPM, a full copy of the disk can decrypt the secrets, so keep the disk encrypted (LUKS).
Checked by hand in the WSL `FedoraLinux-44` (systemd 259): the value arrives without any added byte, readable by the user of the unit only.

### Windows: DPAPI folder

Set the folder of the vault of the tier in the setting `Vault:Path` (an environment variable of the service `Vault__Path`, or `appsettings.json` of the server: it is not a secret), for example `C:\ProgramData\BoardGamesPlatform\VeryBack\Vault`.
Write each secret with [tools/vault/Set-VaultSecret.ps1](../../tools/vault/README.md), **as an administrator, after the service exists** (its account must be known):

```powershell
pwsh tools/vault/Set-VaultSecret.ps1 -VaultPath C:\ProgramData\BoardGamesPlatform\VeryBack\Vault -Name Tls:CertificatePassword -Account 'NT SERVICE\BoardGamesPlatform.VeryBack'
```

The tool asks the value hidden (or reads it from its standard input), encrypts it with DPAPI (scope `LocalMachine`, bound to the name of the secret), and gives the folder and the file to the Administrators, readable by the account of the service only.
At startup, the tier refuses a folder or a file whose owner or access list trusts another account than SYSTEM, the Administrators and its own account: with DPAPI `LocalMachine`, any process of the machine could otherwise read a secret or plant one.
A file copied from another machine or under another name does not decrypt; a full copy of the disk can, so keep it encrypted (BitLocker).

The exact installation of the services (units, accounts, folders) comes with the native installation (0.25.0).

## Repository protection

GitHub **secret scanning** with **push protection** is enabled on the repository (checked on 2026-10-29): a push holding a known kind of secret (API keys, tokens…) is refused before it reaches GitHub.
It complements the CI check, which knows the setting names of the platform.
