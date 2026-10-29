# Configuration and secrets

How every executable of the platform (Back, Very Back, Gateway, website) reads its settings, and where its secrets live.
The vault of the platform is chosen in 0.7.0; until then, these conventions apply to every environment.

## Rules

1. **No secret in a file of the repository**, in any environment. A secret written in a commit stays in the history, even after it is removed.
   A secret key may appear in a committed `appsettings*.json` file to document it, but its value stays **empty**.
2. **The executable refuses to start with a missing or invalid setting** (fail closed). It never falls back to a default value for a secret.
   The `Observability` section already works this way: an invalid collector address stops the startup.
3. **A secret value is never written to a log**, at any level, Debug included. Error messages name the setting, never its value.
4. **Only the Very Back declares database connection strings** (`ConnectionStrings` section): no other tier can even be configured to reach the database.

Rules 1 and 4 are checked by the CI ([ConfigurationFileTests.cs](../../test/BoardGamesPlatform.Architecture.Tests/ConfigurationFileTests.cs)).
The CI reads the `appsettings*.json` files and the `Properties/launchSettings.json` file of every project.
A key is treated as a secret when one of its segments ends with `Password`, `Pwd`, `Secret`, `ApiKey`, `Token`, `PrivateKey` or `ConnectionString`,
plural included (for example `Smtp:Password`, `Smtp:Passwords:0`, `ConnectionStrings:Main`); its text value must then be empty.
Name every secret setting accordingly, so that the CI protects it.

## Configuration sources

Each executable uses the default configuration of .NET, without a custom provider.
The sources are read in this order; **a later source overrides an earlier one** for the same key ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/?view=aspnetcore-10.0#default-app-configuration-sources)):

| Order | Source | Committed | Holds |
|:-----:|--------|:---------:|-------|
| 1 | `appsettings.json` | ✅ | Defaults of every environment; secret keys with an empty value |
| 2 | `appsettings.{Environment}.json` (for example `appsettings.Development.json`) | ✅ | What differs per environment, never a secret |
| 3 | User secrets, in the `Development` environment only | ❌ | The secrets of a developer machine |
| 4 | Environment variables | ❌ | The secrets of the acceptance and production servers |
| 5 | Command-line arguments | ❌ | One-off overrides, never a secret (visible in the process list) |

The environment is set by the `DOTNET_ENVIRONMENT` variable, for every executable. The Gateway and the website also read `ASPNETCORE_ENVIRONMENT`,
but `DOTNET_ENVIRONMENT` takes precedence over it ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/fundamentals/environments?view=aspnetcore-10.0)).
Without either variable, an executable runs as `Production`. The name of the acceptance environment is chosen with the first acceptance deployment.

## Development: user secrets

The secrets of a developer machine are stored with the Secret Manager of .NET, outside the repository:

```powershell
dotnet user-secrets init --project src/BoardGamesPlatform.VeryBack
dotnet user-secrets set "ConnectionStrings:Main" "<connection string>" --project src/BoardGamesPlatform.VeryBack
```

- `init` adds a `UserSecretsId` to the project file; it is done once per project, in the pull request that brings its first secret.
- The values are stored **unencrypted** in a JSON file of the user profile (`%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` on Windows,
  `~/.microsoft/usersecrets/<id>/secrets.json` on Linux). They are for development only: never put a secret of an acceptance or production server there
  ([Microsoft Learn](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0#use-the-secret-manager-tool)).

## Acceptance and production: environment variables (until 0.7.0)

On the servers, the deployment sets each secret as an environment variable of the service only, never of the whole machine.
A key `Section:Key` becomes the variable `Section__Key` (double underscore), the form every system accepts, for example `ConnectionStrings__Main`.

| Host | Where the variables are set | Protection |
|------|-----------------------------|------------|
| systemd unit (Linux) | An `EnvironmentFile=` of the unit, for example `/etc/boardgamesplatform/very-back.env` | Owned by `root`, mode `600`: systemd reads it before starting the service |
| Windows service | The `Environment` value of the service in the registry | Writable by administrators only |
| Website and Gateway under IIS | The environment variables of the application pool (`applicationHost.config`), not the `web.config` of the site | Outside the site folder |

Environment variables are stored as plain text: whoever controls the machine or the process can read them
([Microsoft Learn](https://learn.microsoft.com/aspnet/core/security/app-secrets?view=aspnetcore-10.0#work-with-environment-variables)).
This is the accepted step before the vault of 0.7.0. The exact commands per host are written with the native installation (0.25.0).

## Repository protection

GitHub **secret scanning** with **push protection** is enabled on the repository (checked on 2026-10-29): a push holding a known kind of secret (API keys, tokens…) is refused before it reaches GitHub.
It complements the CI check, which knows the setting names of the platform.
