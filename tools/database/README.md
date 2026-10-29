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
