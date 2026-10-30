# Database tools

## Deploy-TestDatabase.ps1

Builds the SQL project (`src/BoardGamesPlatform.Database`) into a DACPAC and deploys it to a throwaway database, **dropping and recreating** that database every time. The SQL tests start from this known state.

### The prefix rule

The database name must match `^(DEV|TEST)_[A-Za-z0-9_]+$`, **case-sensitive**. Anything else (`master`, a production name, `test_X`, `DEV_`, brackets, spaces, `;`) is refused with exit code 1 **before** anything is built or any connection is opened.

### Parameters

| Parameter | Default | Meaning |
|---|---|---|
| `-DatabaseName` | (mandatory) | Database to drop and recreate. |
| `-ServerInstance` | `localhost` | SQL Server instance; the local default instance. Windows authentication only. |
| `-SkipBuild` | off | Reuses the DACPAC of a previous build. |
| `-WhatIf` | off | Shows what would be built, dropped and recreated, and does nothing. |

Exit code: `0` on success, `1` when refused or failed.

### Examples

```powershell
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName TEST_BoardGamesPlatform
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName TEST_BoardGamesPlatform -WhatIf
pwsh tools/database/Deploy-TestDatabase.ps1 -DatabaseName DEV_BoardGamesPlatform -SkipBuild
```

### What it needs

Nothing is installed by the script. It uses what Visual Studio already provides, found with `vswhere`:

- the MSBuild of Visual Studio (component `Microsoft.Component.MSBuild`);
- `SqlPackage.exe`, under `Common7\IDE\Extensions\Microsoft\SQLDB\DAC` of the Visual Studio installation;
- a SQL Server reachable with the Windows account of the caller (right to create and drop databases).

On the local machine the script trusts the server certificate (`/TargetTrustServerCertificate:True`), because a local SQL Server uses a self-signed one. Any other server keeps the strict default.

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
