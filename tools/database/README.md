# Database tools

## Deploy-TestDatabase.ps1

Builds the SQL project (`src/BoardGamesPlatform.Database`) into a DACPAC and deploys a throwaway database **from scratch**, dropping and recreating it every time, with its **application user**: a login of the server named after the database, a random password, a user of the database member of `ApplicationRole` (doc/dev/database.md, Application principal and Deployment script). The SQL tests start from this known state.

### The prefix rule

The database name must match `^(DEV|TEST)_[A-Za-z0-9_]+$`, **case-sensitive**. Anything else (`master`, a production name, `test_X`, `DEV_`, brackets, spaces, `;`) is refused with exit code 1 **before** anything is built or any connection is opened. The login has the name of the database, so the script never drops another login.

### What it does

1. The prefix rule, before anything.
2. Builds the DACPAC (unless `-SkipBuild`).
3. Publishes it with `CreateNewDatabase=True`: the database is dropped and recreated, its files under the default paths of the server; the post-deployment script writes the registration switch, closed.
4. Drops the old login of the same name, after closing its sessions.
5. Creates the login with a new random password (32 random bytes in Base64, drawn again if the password policy refuses it), `CHECK_POLICY = ON`, `CHECK_EXPIRATION = OFF`, its database as default, `DENY VIEW ANY DATABASE`; then its user and its membership of `ApplicationRole`.
6. Sends the connection string of the application user (`Encrypt=Mandatory`; `TrustServerCertificate=True` for the local server only) where `-SecretTarget` says.

The password travels on the standard input of `sqlcmd` and of the tool that stores it, never on a command line (visible in the list of processes) nor in a file in clear; it is never printed.

### Parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-DatabaseName` | (mandatory) | Database to drop and recreate; also the name of its application login and user. |
| `-ServerInstance` | `localhost` | SQL Server instance; the local default instance. Windows authentication only. |
| `-SkipBuild` | off | Reuses the DACPAC of a previous build. |
| `-SecretTarget` | `None` | Where the connection string goes: `None` (kept nowhere: the SQL tests play the user with `EXECUTE AS`), `UserSecrets` (the user secrets of the Very Back, a developer machine), `Vault` (the DPAPI vault of the Very Back, as an administrator). |
| `-VaultPath` | | Folder of the vault of the Very Back, for `-SecretTarget Vault`. |
| `-VaultAccount` | | Account of the service of the Very Back, for `-SecretTarget Vault`. |
| `-WhatIf` | off | Shows what would be built, dropped and recreated, and does nothing. |

Exit code: `0` on success, `1` when refused or failed.

### Examples

```powershell
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName TEST_BoardGamesPlatform
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName TEST_BoardGamesPlatform -WhatIf
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName DEV_BoardGamesPlatform -SkipBuild -SecretTarget UserSecrets
```

### What it needs

Nothing is installed by the script. It uses what Visual Studio and SQL Server already provide:

- the MSBuild of Visual Studio (component `Microsoft.Component.MSBuild`), found with `vswhere`;
- `SqlPackage.exe`, under `Common7\IDE\Extensions\Microsoft\SQLDB\DAC` of the Visual Studio installation;
- `sqlcmd` of the Microsoft ODBC Driver 18 tools, in the `PATH` (installed with SQL Server);
- a SQL Server reachable with the Windows account of the caller, with the rights to create and drop databases and logins.

On the local machine the script trusts the server certificate (`/TargetTrustServerCertificate:True`, `sqlcmd -C`), because a local SQL Server uses a self-signed one. Any other server keeps the strict default.

## Running the SQL tests

The SQL tests of `test/BoardGamesPlatform.Database.Tests` run against a throwaway database deployed by the script above. Without one, they are reported as **skipped**, never as passed (this is the case in the CI); the convention tests of the same project run everywhere.

```powershell
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName TEST_BoardGamesPlatform
$env:BOARDGAMESPLATFORM_TEST_DATABASE = 'Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=True;TrustServerCertificate=True'
dotnet test --project test/BoardGamesPlatform.Database.Tests
```

- The tests refuse any database whose name does not start with `TEST_` or `DEV_`: they create and drop objects (a temporary user without login, dropped at the end of its test). The rule lives in one place, `TestDatabase` of `test/BoardGamesPlatform.TestKit`, used by the three test projects that reach a real SQL Server (`Database.Tests`, `VeryBack.Tests`, `EndToEnd.Tests`); the name is read from the open connection.
- The session test of the Very Back (`SqlSessionsTests`) counts the sessions in `sys.dm_exec_sessions`, which shows the sessions of others only with `VIEW SERVER STATE` (SQL Server 2019 and earlier) or `VIEW SERVER PERFORMANCE STATE` (SQL Server 2022 and later); without it, the test is skipped with that reason.
- Running them locally is **mandatory before every `release/X.Y.Z` → `dev` merge** (roadmap, test strategy).
- The real SQL variant of the end-to-end test (`test/BoardGamesPlatform.EndToEnd.Tests`, a Ping from a headless front through the five tiers down to this database) runs with the same variable, the same `TEST_` / `DEV_` rule, and is skipped without it.
