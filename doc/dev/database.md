# Database foundation

This document specifies the database foundation delivered in 0.7.0 (schemas, filegroups, audit, the write and read patterns, the application principal, the deployment script) and the vault of the secrets of the tiers, and records their threat model.
It was written **before the code** and reviewed before any line of it, as the secure channel was in 0.6.0 ([secure-channel.md](secure-channel.md)), then aligned with what the code and its tests proved; each change of that kind says what found it.

The reference level is the author's own SQL project, AckSolutions: modules and schemas per type of object, immutable audit, table-valued parameters checked again by the procedures, one JSON result per row, an application user limited to procedures, types and views.
This document keeps that level and says, for each place it departs from it, why.

## Scope

| In 0.7.0 | Not in 0.7.0 |
|----------|--------------|
| Schemas, filegroups, naming and headers of every object | The business of the next versions: accounts (0.8.0), lobby, games, moves |
| The audit table, its immutability, its purge | The archive of the purged rows to a backup: its mechanism is a decision of the project lead (see [Decisions left to the project lead](#decisions-left-to-the-project-lead)) |
| The write pattern (TVP, checks, JSON per row, audit) and the read pattern (views), proved on one real table: the settings of the platform | Any call of these procedures from the Very Back: its first business operation comes with 0.8.0 |
| A temporal table and its history | |
| The application principal of each database and its permission matrix | Further SQL users made by a procedure (later, with the same limits) |
| The deployment script of `DEV_` and `TEST_` databases | The deployment of `ACC_` and production databases (native installation, 0.25.0) |
| The vault of the secrets of the tiers, on Windows and on Fedora | A vault product or a server of secrets |

## Principles

1. **Contract as a service, control in depth.** Each layer guards what it imports: a table-valued parameter checks its own rows, the procedure checks them again, the table checks them a third time with its constraints. A check that looks redundant is intended.
2. **The database is the master of its rules.** The tiers never read a table: the application principal executes procedures and reads views, nothing else (decided in 0.3.0: plain ADO.NET, no ORM).
3. **One JSON result per input row.** A write never fails as a whole because one row is wrong: each row comes back with its result, its identifier on success, its reason on failure.
4. **Every write is audited**, in the same transaction as the write itself: there is no write without its audit row, and no audit row without its write.
5. **Comments describe the use, not the code**, so that nobody has to read the T-SQL to know what an object guarantees. They must therefore be exact.
6. **Fail closed.** A missing secret, an unknown actor, an empty input refuses the call; nothing falls back to a default.

## Schemas

The schemas follow the architecture of AckSolutions: **modules** hold the tables and are never reached by the application, **schemas per type of object** hold what the application may call.
Every schema belongs to `dbo` (`AUTHORIZATION [dbo]`), so that a procedure reaches the tables through ownership chaining without the caller holding any right on them.

| Schema | Family | Exposure | Holds |
|--------|--------|----------|-------|
| `Platform` | Module | Internal | The tables of the platform itself; in 0.7.0, the settings |
| `Logs` | Module | Internal | The audit table, the procedures that insert and purge it, its triggers |
| `History` | Object type | Internal | The history tables of the temporal tables, named `<Schema>_<Table>`, made by SQL Server |
| `Functions` | Object type | Internal | The scalar functions of the checks (`IsValid<Object>`) |
| `Types` | Object type | Public (EXECUTE, REFERENCES) | The table-valued parameters (`TVP_<Schema>_<Table>_<Action>`) |
| `StorProc` | Object type | Public (EXECUTE) | Every write of the application (`SP_<Schema>_<Table>_<Action>`) |
| `Views` | Object type | Public (SELECT) | Every read of the application (`V_<Schema>_<Table>`) |
| `ServerCore` | Server | Public (EXECUTE) | The Ping of 0.3.0, kept as it is: it concerns the server, not the data, and the Very Back already calls it |

The accounts of 0.8.0 will bring their own module (`Identity`); a game brings one module per game.
The modules are named after a part of the platform, never after a type of object; no object of the platform lives in `dbo`.

## Filegroups

| Filegroup | Holds |
|-----------|-------|
| `PRIMARY` | The catalog of the database only |
| `Core` | Every table of the modules (`Platform`, `Logs`) and their history tables |
| `Access` | The indexes of the indexed views; in 0.7.0 it holds no object yet |

Each filegroup is created by the project (`ALTER DATABASE [$(DatabaseName)] ADD FILEGROUP`); SSDT adds its file at deployment, under the SQLCMD variables `$(DefaultDataPath)` and `$(DefaultFilePrefix)`, which SqlPackage reads from the target server.
**No path is written in the project**: the files go where the server keeps its data, and an administrator can move them with the publish profile.
`Access` exists from 0.7.0 so that the first indexed view (0.8.0 and later) does not need a change of the storage; an empty filegroup costs a file of a few megabytes.

## Naming and headers

The names and headers follow AckSolutions and `doc/dev/coding-conventions.md`:

- prefixes `SP_`, `TVP_`, `V_`, `TR_`, `IX_` / `IXC_`, `PK_`, `UK_`, `FK_`, `DF_`, `CK_`; full names `<Prefix>_<Schema>_<Table>[_<Column or Action>]`;
- every file starts with a `/** @file @author @brief */` header and the tables of its type of object (Columns, Constraints, Triggers, Indexes, History for a table; Parameters, Errors, Response Structure, Response Examples, Behavior for a procedure); no `Versions` block: Git keeps the history;
- every call is schema-qualified (`[StorProc].[SP_...]`), which is the condition of the SR0016 suppression.

## Tables of 0.7.0

### `Logs.Audit`

| Column | Type | Null | Default | Brief |
|--------|------|:----:|---------|-------|
| `Id` | `UNIQUEIDENTIFIER` | no | `NEWID()` | Identifier of the row |
| `IdActor` | `UNIQUEIDENTIFIER` | no | | Who asked for the change (see [The actor](#the-actor)) |
| `LoginName` | `NVARCHAR(128)` | no | `ORIGINAL_LOGIN()` | The SQL login of the session that wrote the row, whatever the actor claims |
| `ActionDT` | `DATETIME2(7)` | no | `SYSUTCDATETIME()` | When the change was made, in UTC |
| `SchemaName` | `NVARCHAR(128)` | no | | Schema of the changed table |
| `TableName` | `NVARCHAR(128)` | no | | Changed table |
| `OldValue` | `NVARCHAR(MAX)` | yes | | JSON of the row before the change (`NULL` for an insert) |
| `NewValue` | `NVARCHAR(MAX)` | yes | | JSON of the row after the change (`NULL` for a delete) |

Constraints: the identifiers are never `Guid.Empty`; `SchemaName` and `TableName` name an existing table (`Functions.IsValidSchemaName` and `Functions.IsValidTableName`, which read `INFORMATION_SCHEMA` and therefore need the right to read the metadata, see [Permission matrix](#permission-matrix)); `OldValue` or `NewValue` is set, and each one set is valid JSON (`ISJSON`); `LoginName` is not empty.
Not temporal: an audit row never changes, so it has no history. On the filegroup `Core`.
A clustered index on `ActionDT` serves the purge by dates and keeps new rows at the end; the primary key on `Id` stays non-clustered so that random GUIDs do not split the pages (a departure from AckSolutions, whose key is clustered).

**What the audit never holds.** A column that holds a secret or its hash (a password hash, the hash of a session token, from 0.8.0) is never written to `OldValue` or `NewValue`: its change is audited as `"<column>":"changed"`. The audit must not become a second copy of the secrets. The same question for the history of the temporal tables is decided with the first such column (0.8.0).

### `Platform.Settings`

The settings of the platform that an administrator may change while it runs. Its first row is the registration switch that 0.8.0 needs (`Registration.IsOpen`, closed by default): it is a real need of the next version, not invented business.

| Column | Type | Null | Default | Brief |
|--------|------|:----:|---------|-------|
| `Id` | `UNIQUEIDENTIFIER` | no | `NEWID()` | Identifier of the row |
| `Code` | `NVARCHAR(64)` | no | | Unique code of the setting (`Area.Name`, letters, digits and dots) |
| `Kind` | `NVARCHAR(16)` | no | | `Boolean`, `Integer` or `Text`: what `Value` must hold |
| `Value` | `NVARCHAR(4000)` | no | | The value, as text: `true` / `false` for a Boolean, a 64-bit integer for an Integer |
| `Comment` | `NVARCHAR(MAX)` | yes | | What the setting does |
| `ValidFrom`, `ValidTo` | `DATETIME2(7)` | no | system time | Period of the row (UTC, by definition of temporal tables) |

Constraints: `Id` not empty; `Code` unique and matching its pattern (`Functions.IsValidSettingCode`: ASCII letters, digits and dots, a letter first, a dot, no dot at the end); `Kind` among its three values; `Value` consistent with `Kind` (`Functions.IsValidSettingValue`: `true` or `false`, a 64-bit integer in its canonical form, a text not empty). The procedures call the same two functions, so each rule lives in one place.
The update refuses a value equal to the current one (`Value is unchanged`): it would only add a history row and an audit row that say nothing.
The registration switch is written by the post-deployment script, through the insert procedure (so with its audit row), only when it does not exist: a redeployment never reopens the registration.
System-versioned: `WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [History].[Platform_Settings]))`, so that every former value of a setting stays readable with the time it was valid.
The `Kind` of a setting never changes after its insert: the update procedure only takes a new value.

## The audit

The audit keeps the design of AckSolutions: the table `Logs.Audit`, its `INSTEAD OF` triggers, its internal insert, and its purge by dates opened by the session key `AllowAuditPurge`. It departs from it on these points, each explained below:

| AckSolutions | 0.7.0 | Why |
|--------------|-------|-----|
| The triggers and the gate are the protection | `DENY INSERT, UPDATE, DELETE` to `public` on the table | The gate is not a barrier: anyone can set its key (found by the security review) |
| A member of `db_owner` purges | A role `AuditPurgeRole` | Least privilege for the purge |
| The purge deletes every row before its date | The rows that record a purge are never purged; the purge refuses a key already set; the key is read once, the delete runs with `MAXDOP 1` | A purge cannot hide an earlier one; a read-only key could not be closed; a known issue of `SESSION_CONTEXT` in parallel plans |
| `@Date`, compared with the local date | `@Before`, compared with the UTC date | One clock for the audit and the temporal tables |
| Clustered primary key on the GUID | Non-clustered key; clustered index on `ActionDT`; `LoginName` added | No page split; the purge reads by date; the login of the session is a fact the caller cannot forge |

### Immutability

| Statement | Result |
|-----------|--------|
| `INSERT` | Only through `Logs.SP_Logs_Audit_Insert`, called by the procedures of `StorProc` in their own transaction; `DENY INSERT` to `public` on the table |
| `UPDATE` | `DENY UPDATE` to `public`; always refused by the trigger `TR_Logs_Audit_IOfUpdate` (`INSTEAD OF UPDATE`, error 50001) |
| `DELETE` | `DENY DELETE` to `public`; refused by the trigger `TR_Logs_Audit_IOfDelete` (`INSTEAD OF DELETE`, error 50001), except inside the purge |
| `TRUNCATE`, `ALTER TABLE`, `DISABLE TRIGGER`, `DROP` | Not caught by a DML trigger: they need `ALTER` on the table, which only the owner, `db_owner` and `db_ddladmin` hold. The application role is explicitly denied every right on the schema `Logs` |

The `DENY INSERT, UPDATE, DELETE ON [Logs].[Audit] TO public` binds every user of the database except `dbo`, members of `db_owner` and `db_datawriter` included (a DENY wins over a grant, and every user is a member of `public`). The procedures still write and purge the table, because ownership chaining checks no permission on the objects it reaches (proved by the security review of this document, on SQL Server 2025).

A ledger table would refuse all of these to the administrators too, but it can never be purged: the purge of the audit is a requirement of the project lead (by dates, sliding, to a backup), so **ledger tables are excluded** (decided by the project lead in 0.7.0).

### The purge

`Logs.SP_Logs_Audit_Purge @Before DATE` deletes the audit rows written before `@Before` (UTC), and only them:

1. `@Before` must be earlier than today (UTC): the rows of the current day are never purged; a `NULL` date is refused.
2. The key `AllowAuditPurge` must not be set yet in the session: a key set read-only (`@read_only = 1`) could not be closed afterwards, so the purge refuses to start (51000). A key set read-only to NULL passes that check, and the engine then refuses to open the gate (15664): the purge fails before any delete, rolled back (found by the security review of the code).
3. In one transaction, it opens the gate (`sp_set_session_context N'AllowAuditPurge', 1`) just before the delete, deletes the rows, closes the gate (`NULL`), then writes **one audit row of the purge itself** (the date given, the number of rows deleted, the oldest and the newest `ActionDT` deleted).
4. **The rows that record a purge are never purged** (the rows about `Logs.Audit` itself, which only the purge writes): the chain of the purges stays complete, so a purge can never hide an earlier one.
5. On any error, it rolls back, closes the gate and rethrows: no partial purge. The only case where the gate stays open in the session is a client that cancels the call (an attention is not caught by `CATCH`); the session is then the one of an administrator, and its reuse from a pool clears the key.

The procedure is in the schema `Logs`, which the application cannot execute: **a purge is an act of administration**, never of the platform.
A role `AuditPurgeRole` holds `EXECUTE` on `Logs.SP_Logs_Audit_Purge` and `VIEW DEFINITION` on the schema `Logs`, and nothing else, so that purging does not require being a member of `db_owner`; it has no member in the project, an administrator adds one. The metadata right is needed because the purge writes an audit row of itself, whose CHECK constraints read the metadata with the rights of the caller (found by the test of a member of the role).
The archive of the rows before their purge (export, or a backup of the database proved before the purge) is left to the project lead.

**Strength of the gate.** The gate is not a barrier: anyone can set the session key (`sp_set_session_context` is available to every user). The barrier is the `DENY` to `public` above: no user but `dbo` can delete a row, with or without the key. The gate only protects `dbo` against a `DELETE` by mistake. A member of `db_owner` can still revoke the `DENY` or disable the trigger, and `dbo` or a `sysadmin` can do anything; these are acts of administration that a server-level audit sees, not something the database can prevent (see [Threat model](#threat-model)).

**The gate is read once.** SQL Server 2025 documents that `SESSION_CONTEXT` may return wrong results in parallel plans (known issue, workaround: trace flag 11042). The delete trigger reads the key once into a variable, before any statement that touches rows, and its delete runs with `MAXDOP 1`. A test purges over a pooled connection and checks that the key is cleared when the connection is reused.

### The actor

Until 0.8.0 there are no accounts. Every procedure of `StorProc` takes the actor of the change (`@IdActor`) and refuses it when `Functions.IsValidActorId` does not know it:

- in 0.7.0, the only known actor is **the platform itself**, a fixed identifier returned by `Functions.SystemActorId()` (the deployment, the settings written by an administrator through the platform);
- in 0.8.0, `IsValidActorId` also accepts the identifiers of `Identity.Users`, and a foreign key is added from `Logs.Audit.IdActor` to the users, with the platform as a row of its own.

The actor is what the caller claims; `LoginName` is what SQL Server knows. Both are kept. Their limit must be written down, since the pitch of the platform is to prove who did what: **the audit proves that the platform made the change on behalf of the actor it claimed, not the intent of that actor.** Every write of the platform uses the one application login, so a compromised Very Back can attribute a change to any valid actor; `LoginName` separates the platform from the administrators, not one player from another.

## Write pattern

A write procedure of `StorProc` has this shape (from `SP_Core_Languages_Insert` of AckSolutions):

```text
SP_<Schema>_<Table>_<Action> @Rows [Types].[TVP_<Schema>_<Table>_<Action>] READONLY, @IdActor UNIQUEIDENTIFIER, @Result NVARCHAR(MAX) OUTPUT
```

1. `SET NOCOUNT ON; SET XACT_ABORT ON;`, `TRY`, one transaction.
2. **Checks of the call**, each one `THROW 51000` and nothing written: no row; more rows than the maximum of the procedure (1 000 by default, named in its header); `@IdActor` empty or unknown.
3. **Checks of the rows**, one step per rule, in a fixed order; a failing row goes to a table `@results` with `IsSuccess = 0` and the reason of **its first failing rule**.
4. The remaining rows are written, with `OUTPUT` of their identifiers; they go to `@results` with `IsSuccess = 1`.
5. One audit row per written row, through `Logs.SP_Logs_Audit_Insert`.
6. `@Result` is a JSON array, **one object per input row**, in the order of the codes, then the transaction commits.

Departures from AckSolutions, and why:

| Topic | AckSolutions | 0.7.0 | Why |
|-------|--------------|-------|-----|
| A row that breaks two rules | Comes out twice in `@Result` | Comes out once, with its first failing rule | "One JSON result per row" must hold for the caller, who maps each result to one input row |
| What the audit stores | The result object (`IsSuccess`, `Error` included) | The values of the row only | The audit records the data, not the outcome of the call; a failed row is never audited, since nothing was written |
| Time | `SYSDATETIME()`, local time | `SYSUTCDATETIME()` | The periods of the temporal tables are always UTC: one clock for the audit and the history; no change of time twice a year |
| Actor | `IdUser`, a foreign key to the users | `IdActor`, the platform until 0.8.0, plus `LoginName` | There are no accounts yet; the login of the session adds a fact the caller cannot forge |
| Clustered key of the audit | The GUID | `ActionDT` | Random GUIDs fragment a clustered index; the purge reads by date |

The correlation key of a write is the business key of the table (`Code` for the settings), declared `UNIQUE` in the table-valued parameter, so that each result names its row without an extra column.

**Errors.** `51000` for a refused call (its message names the rule, never a value), `50001` for a refused change of the audit. Any other error is the engine's own and is rethrown as it is. These numbers become the codes of the Very Back with its first business call (0.8.0).

**The messages of the engine carry values** (2627 and 2601, a duplicate key "value is …"; 2628, a "truncated value"; 245, a conversion of a "value"). From 0.8.0 these values are e-mail addresses and nicknames. So **the Very Back never logs nor returns `SqlException.Message`**: a failed call leaves its procedure as a `SqlCallException`, which keeps the error `Number`, `State`, `Procedure` and `LineNumber` and the procedure called, without the message nor the `SqlException` inside; the dispatcher logs that, and answers its client with a code. This rule applies from 0.7.0 to the Ping, the only procedure the Very Back calls today.

## Read pattern

The application reads through the views of `Views` only (`V_<Schema>_<Table>`): one view per table it needs, with the columns it needs, never `SELECT *`.
In 0.7.0, `Views.V_Platform_Settings` returns `Code`, `Kind`, `Value` and `ValidFrom` of the current settings. The history is read by an administrator, through `FOR SYSTEM_TIME` on the table; no view exposes it to the application.
A view belongs to `dbo` like its tables: the SELECT right on the view is enough (ownership chaining).

## Permission matrix

The application principal of a database is a member of `ApplicationRole` and of nothing else. The role holds exactly:

| Securable | Permission | Why |
|-----------|------------|-----|
| Schema `StorProc` | `EXECUTE` | The writes |
| Schema `ServerCore` | `EXECUTE` | The Ping |
| Schema `Views` | `SELECT` | The reads |
| Schema `Types` | `EXECUTE` | To declare and pass the table-valued parameters (`REFERENCES`, granted in AckSolutions, is not needed: proved by the security review) |
| Database | `VIEW DEFINITION` | To read the metadata (it sees the tables without being able to query them); the checks of the audit (`IsValidSchemaName`, `IsValidTableName`) depend on it |
| Schemas `Logs`, `Platform`, `History`, `Functions`, `dbo` | `DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP` | A grant added by mistake later, directly or through `public`, cannot open them |

**No DENY may cover `VIEW DEFINITION` on a schema of the platform**, and `CONTROL` implies it: the checks of the audit read the metadata with the rights of the caller, even inside a procedure owned by `dbo` (metadata visibility is not part of ownership chaining). A `DENY CONTROL` on `Logs` makes every audited write fail its CHECK constraint (proved by the security review).

The test of the permission matrix reads every permission and membership of the role and compares it with this list, then plays a member of the role: every procedure of `StorProc` and every view can be used; every table, the purge, the insert of the audit and any creation are refused with error 229.

## Application principal

| Topic | Choice | Why |
|-------|--------|-----|
| Kind | A **login of the server** and a user of the database mapped to it, both named after the database (`DEV_BoardGamesPlatform`) | A contained user needs the server option `contained database authentication` and a partially contained database: a change of the configuration of the server, refused for the author's servers |
| Authentication | SQL authentication, `CHECK_POLICY = ON`, `CHECK_EXPIRATION = OFF`, `DEFAULT_DATABASE` set | The Very Back can run on Fedora, where a Windows account is not available; the password is long and random, so expiration only breaks the service. `CHECK_POLICY` also brings the lockout of the account after failed logins (threshold of the Windows policy): see the threat model |
| Password | 32 random bytes (`RandomNumberGenerator`), written in Base64, made by the deployment script, never shown, never written to a file in clear; drawn again when the policy refuses it (a Base64 text without digit nor `+` or `/` happens about once in 7 500 draws) | Strong enough to make guessing useless; the script is the only one to see it |
| Connection string | Built with `SqlConnectionStringBuilder`, `Encrypt=Mandatory`; `TrustServerCertificate=True` only for the local server of a developer machine. Outside Development, the Very Back refuses a connection string whose `Encrypt` is `False` or `Optional`, or which trusts the server certificate, naming the key, never the value | The connection to SQL Server carries the password and the data: its encryption is a rule, not an assumption |
| Server rights | `CONNECT SQL` only, plus `DENY VIEW ANY DATABASE` | The login does not even list the other databases of the server |
| Where it goes | The vault of the Very Back (or its user secrets on a developer machine), as the full connection string `ConnectionStrings:Main` | The Very Back is the only tier that reaches the database |

The login and the user are **not part of the DACPAC**: a login is a server object, and a user mapped to a login would break the deployment on any other server. The deployment script creates them after the publish.

## Deployment script

`tools/database/Deploy-TestDatabase.ps1` already refuses any database without a `DEV_` or `TEST_` prefix (0.3.0). In 0.7.0 it deploys a database from scratch in one run:

1. **Guard**, before anything: the name matches `^(DEV|TEST)_[A-Za-z0-9_]+\z`, case-sensitive.
2. Build the DACPAC (MSBuild of Visual Studio, the project keeps its original format), unless `-SkipBuild`.
3. **Drop the old login** of the same name, if it exists and only if its name has the same prefix: its sessions are closed first.
4. Publish with `CreateNewDatabase=True` (the database is dropped and recreated), files under the default paths of the server.
5. Create the login with a new random password, the user, its membership of `ApplicationRole`, and the `DENY VIEW ANY DATABASE`. The password never appears on a command line (visible in the list of processes) nor in a file in clear: the batch is given to `sqlcmd` (ODBC Driver 18, encrypted connection by default) **on its standard input**, never with `-Q` nor `-i`. PowerShell 7 ships only the former `System.Data.SqlClient`, deprecated by Microsoft, so the script does not open the connection itself. `CREATE LOGIN` only takes a literal, so the batch is dynamic SQL built in a variable with `QUOTENAME` for the login name; the name has already passed the guard, and the password, Base64, holds neither a quote nor the `$(` of a variable of `sqlcmd`. A refusal by the password policy (error 15118) draws a new password.
6. Send the connection string to the place named by `-SecretTarget`:
   - `None` (default): nowhere; the login exists but nobody knows its password (enough for the SQL tests, which play the user with `EXECUTE AS`);
   - `UserSecrets`: the user secrets of the Very Back, as a JSON batch on the standard input of `dotnet user-secrets set` (built with `ConvertTo-Json`; never on a command line, visible in the list of processes);
   - `Vault`: the vault folder of the Very Back given by `-VaultPath`, readable by the account given by `-VaultAccount`, through `tools/vault/Set-VaultSecret.ps1` (as an administrator).
7. Drop the references to the password (a .NET string cannot be erased: it stays in memory until the process ends, which is short).

What the script never does: deploy a database without the prefix, touch another login, print or log the password, keep it in a file, install anything.

## The vault

### Requirement

Three secrets exist today; each moves from an environment variable of the service to the vault:

| Setting | Tiers | Since |
|---------|-------|-------|
| `ConnectionStrings:Main` | Very Back | 0.3.0 |
| `Tls:CertificatePassword` | Gateway, Back, Very Back | 0.5.0 |
| `Handshake:SigningKeyPassword` | Back, Very Back | 0.6.0 |

The vault is **native to the operating system** (decided by the project lead in 0.7.0): DPAPI on Windows, `systemd-creds` on Fedora, behind one entry point. No server of secrets, no product to install.

### Design

The vault is a **configuration source**: it adds the secrets to the configuration of the tier, under their usual keys, so that the code that reads them (`TlsOptions`, `HandshakeOptions`, `ConnectionStringsOptions`) and its validation at startup do not change.

```csharp
// In AddPlatformGateway, AddPlatformBack and AddPlatformVeryBack, with the secrets of the tier
builder.AddPlatformVault("Tls:CertificatePassword", "Handshake:SigningKeyPassword");
```

| Host | Source | Where the secrets are |
|------|--------|-----------------------|
| Fedora, systemd unit | `$CREDENTIALS_DIRECTORY`, read by the provider of the platform (`SystemdCredentialsStore`) | Each secret is encrypted by `systemd-creds encrypt --name=<name>` (key of the host, and the TPM2 when present) under `/etc/credstore.encrypted/`; the unit names it with `LoadCredentialEncrypted=<name>:<file>`, and a file renamed to another name is refused by systemd; systemd decrypts it at start into a read-only, non-swappable folder readable by root and **by the user of the service**, so each tier runs under its own system user (or `DynamicUser=yes`), never a shared one |
| Windows service | The folder named by `Vault:Path`, read by the provider of the platform (`DpapiVaultStore`), DPAPI `LocalMachine` | Each secret is a file encrypted with DPAPI (`ProtectedData`); the vault refuses a file or a folder whose owner is not SYSTEM, the Administrators or the account of the service, or on which another account holds a right (any right on the file, a right to write in the folder); the tool of the administrators is stricter: owner the Administrators, read only for the service |
| Developer machine, tests | None | User secrets (Development) and in-memory settings, as today |

The name of a secret is its configuration key with `__` for `:` (`Tls__CertificatePassword`), mapped back to the key by the provider.
Both stores implement one interface (`IVaultStore`), read by one configuration source (`VaultConfigurationSource`), added last so that it wins over every other source.
The key-per-file provider of Microsoft was considered for Fedora and left out: one provider of the platform gives both systems the same checks (the names of the tier only, no control character, messages without values), with one dependency less.

Rules:

1. **Outside the `Development` environment, a secret of the tier may have a non-empty value only from the vault.** A secret the tier reads (`ConnectionStrings:Main`, `Tls:CertificatePassword`, `Handshake:SigningKeyPassword`, depending on the tier) set by any other source stops the startup, naming the key and the source: a JSON file edited on the server, an environment variable of any prefix (the connection-string prefixes such as `SQLCONNSTR_`, which map to `ConnectionStrings:`, included), a command-line argument. The check asks each provider for the key as it maps it, never reads the raw names of the variables. Only the secrets of the tier are checked: the hosts of .NET read every environment variable of the machine without prefix, so a rule on the names of the keys alone refused a token of an unrelated tool (found by the tests). The environment variables of 0.5.0 and 0.6.0 are therefore removed from the services when the vault is set up.
2. **In the `Development` environment the check is off**, so a tier logs a warning at startup when it runs as Development: an environment set to Development by mistake on a server is visible in the journal.
3. **A vault that is configured but unreadable stops the startup**: a missing folder, a file that cannot be decrypted, a value that holds a control character (a line break left by an editor), a file or folder whose owner or access list allows more than the table above (Windows), each named, never its content. The access list is checked on the handle of the file that is read, so that the file cannot be swapped between the check and the read.
4. On Windows, each file is bound to its name: the DPAPI entropy is the name of the secret, so a file copied under another name does not decrypt. On Fedora, `systemd-creds` embeds the name and refuses another one.
5. No secret is ever logged; the error messages name the setting and the file, never a value (rule 3 of [configuration.md](../adm/configuration.md)).

**DPAPI scope: `LocalMachine` (proposed with the security review, to be validated by the project lead).** `CurrentUser` would bind a secret to the account of the service. But writing such a file needs the profile of that account loaded, and a virtual account `NT SERVICE\<name>` cannot log on to provision it. The DPAPI of CNG protects only to a domain group or to web credentials, not to a local account.

With `LocalMachine`, any process of the machine can decrypt a file it can read **and write a valid one**: the entropy, the name of the secret, is public. The access list of the file and of its folder is therefore the protection, checked at load, on both the read and the write side. The account of the service itself may own and write its secrets: it can already read them all, so writing them gives it nothing more. This departs from the security review, which asked SYSTEM or the Administrators only; it is submitted to that review again with the code.

It is the same model as `systemd-creds` without a TPM: the secret is safe at rest on another machine, but not in a full copy of the disk of its host, which holds the key of the machine (the DPAPI master key of the machine is protected by keys stored in the registry; `/var/lib/systemd/credential.secret` on Fedora). Hence: `--with-key=host+tpm2` where a TPM2 exists (a vTPM for the virtual machines), and an encrypted disk (BitLocker, LUKS); otherwise a residual risk.

The tool that writes a secret on Windows is `tools/vault/Set-VaultSecret.ps1`: it refuses to run without the rights of an administrator (the account of the service only reads the vault, so nobody else could write it), after the service exists; it reads the value from the standard input or a prompt, never from an argument; it encrypts it; it sets the owner (Administrators) and the access list (read for the account of the service; full control for SYSTEM and Administrators; no inheritance) on the folder and on the file, written atomically. On Fedora, `systemd-ask-password -n | systemd-creds encrypt --name=<name> - /etc/credstore.encrypted/<name>` writes a secret without a line break; the commands are documented in [configuration.md](../adm/configuration.md), and the full installation of the units comes with 0.25.0.

**Packages.** `System.Security.Cryptography.ProtectedData` (Windows only: the DPAPI store is marked `SupportedOSPlatform("windows")` and chosen on Windows only), on the line 10.0.15 of the other Microsoft packages, whose update to 10.0.15 comes with it (CVE audit of this document).

## Threat model

### Assets

- The data of the platform and its audit.
- The password of the application principal, and the two passwords of the keys of each tier.

### Assumptions

- The SQL Server, its host and the hosts of the tiers are administered by trusted people; their operating systems and .NET are kept up to date.
- The link Back → Very Back is protected by 0.5.0 and 0.6.0: only the Very Back connects to the database.
- SQL Server 2025, tested on 17.0.1155.3 (RTM-GDR, the latest build of its branch on 2026-11-12), kept up to date.
- Port 1433 of the SQL Server is open only to the hosts of the Very Back (and to the administrators).

### In scope

| Threat | Protection |
|--------|------------|
| A compromised or buggy Very Back reads or changes a table directly | The application principal has no right on the tables; it only executes the procedures and reads the views; `DENY` on the internal schemas |
| SQL injection through the inputs | No dynamic SQL in the procedures; inputs are parameters and table-valued parameters; the Very Back never builds SQL text |
| A change or a deletion of the audit by the application, or by any user of the database but `dbo` (a member of `db_datawriter` that sets the key of the gate, a grant added by mistake) | `DENY INSERT, UPDATE, DELETE` to `public` on the table; no right on `Logs`; triggers refuse `UPDATE` and `DELETE`; the purge is not executable by the application |
| A write without its audit, or an audit without its write | Same transaction, `XACT_ABORT ON`, rollback on any error |
| A purge that hides itself or an earlier one | The purge writes its own audit row, and the rows that record a purge are never purged |
| A gate of purge left open in a session | The purge closes it in its `CATCH` and refuses to start with a key already set; the reset of a pooled connection clears the session context (tested); a canceled call can leave it open in the session of an administrator only |
| A wrong `SESSION_CONTEXT` in a parallel plan (known issue of SQL Server 2025) | The key is read once, the delete of the trigger runs with `MAXDOP 1` |
| A wrong actor claimed by a tier | An unknown actor is refused; `LoginName` separates the platform from the administrators (the limit of this proof is written in [The actor](#the-actor)) |
| A partial write, a row lost without a reason | One result per input row, success or reason |
| A call with a huge table-valued parameter | A maximum number of rows per call, refused with 51000 |
| Personal data leaked through an error of the engine (a duplicate key, a truncated or converted value) | The Very Back never logs nor returns `SqlException.Message`, only the number, state, procedure and line |
| A secret or its hash copied into the audit | Such columns are audited as `"changed"`, never with their value |
| The password of the application principal read from a file in clear or the list of processes | Never on a command line, never in clear in a file; sent inside the process on an encrypted connection; stored encrypted by the OS (DPAPI, `systemd-creds`) |
| A connection to SQL Server that is not encrypted, or that trusts any certificate | Refused at startup outside Development; the script writes `Encrypt=Mandatory` |
| A secret left in an environment variable or in a JSON file of the server | Refused at startup outside Development; a warning when a tier runs as Development |
| A vault file planted or replaced by another account (DPAPI `LocalMachine` lets any local process write a valid file) | Owner and access list of the file and of its folder checked at load, on the handle read |
| A stolen vault file copied to another machine | DPAPI and `systemd-creds` keys belong to the machine (and its TPM2 on Fedora when present) |
| Another process of the same user reads the credentials of a systemd unit | Each tier runs under its own system user |
| The deployment script run against a real database | The prefix guard, before any build or connection; the login dropped only with the same prefix |
| The login used to explore the server | `CONNECT SQL` only, `DENY VIEW ANY DATABASE` |
| The login of the application locked by failed logins (`CHECK_POLICY` brings the lockout of the Windows policy; the name of the login is public, it is the name of the database) | Port 1433 open only to the hosts of the Very Back; the lockout is watched (`LOGINPROPERTY(…, 'IsLocked')`); the deployment script fails when the policy refuses the password |

### Out of scope, residual risks

- **An administrator of the database or of the server** (`dbo`, `db_owner`, `db_ddladmin`, `sysadmin`) can revoke the `DENY`, disable the triggers, change or truncate the audit, and read every secret of the machine where they are administrator. The audit is immutable against the platform and the limited users, not against its administrators. A server-level SQL Server Audit, written outside the database (to a file: SQL Server 2025 has a pending fix for its Windows Security log target), would detect it; it belongs to the installation (0.25.0) and is noted for the project lead.
- **A full copy of the disk of a host** holds the key of the machine: DPAPI `LocalMachine` and `systemd-creds` without TPM2 protect the secrets against a copy of their files, not against a copy of the whole disk (VHDX, backup). Mitigation where available: TPM2 (`host+tpm2`), encrypted disks.
- **The metadata right** (`VIEW DEFINITION` on the database) lets the application read the definition of every object (the code is public in the repository), and also every principal and role membership (who may purge) and the number of rows of every table (`sys.partitions`). Accepted: the project lead's design gives the application the reading of the metadata; granting it only on the schemas of the platform would hide the principals, and is noted as an option.
- **Secrets in memory**: a .NET `string` cannot be erased, so a dump of the memory of a tier shows its secrets, and its heap can be swapped; the vault protects them at rest only.
- **A tier compromised while running** can use its own login as the platform does: the permission matrix limits the damage to what the procedures allow.
- **The archive before the purge**: until its mechanism is decided, a purge deletes the rows without proof that a backup holds them.
- **Personal data and GDPR**: the purge does not touch the history tables (`History.*`), and the `LoginName` of a purge is the personal login of an administrator. Both belong to the work on NIS2 and GDPR planned by the project lead.

## Tests

- **Per group of procedures**, one test class: the insert and update of the settings (success, each rule of rejection, a row that breaks two rules comes out once, the call refused with no row or an unknown actor, one JSON result per input row in the documented shape).
- **Audit**: one row per written row, same transaction (a failure after the write leaves neither); the values only; `UPDATE` and `DELETE` refused with 50001, even as `dbo`; the gate opened by hand does not let the application, a member of `db_datawriter` or of `db_owner` delete (229); the purge deletes only before the date, never the rows of an earlier purge, refuses today, a future date, `NULL` and a key already set, writes its own row, and leaves the gate closed after a failure; a gate left open is cleared when its pooled connection is reused.
- **Very Back**: a failing SQL call is logged with its number and without the message of the engine.
- **Temporal history**: an update leaves the former value in `History.Platform_Settings` with its period; `FOR SYSTEM_TIME AS OF` returns the former value.
- **Permission matrix**: the declared permissions and memberships of the role, then a member of the role (and the real application user deployed by the script) is allowed every procedure and view, and refused every table, `Logs`, `Functions`, a creation, with error 229.
- **Conventions**: every object of the project has its header; every procedure of `StorProc` has a test class (a new procedure without a test fails); every table of a module is on the filegroup `Core`; every temporal table has its history in `History`.
- **Deployment**: the guard refuses a wrong name before any build; a deployment from scratch creates the login, the user and its role; a second run replaces the password; the password never appears in the output.
- **Vault**: Windows (DPAPI): a secret written by the tool is read under its key; a file with another name, a corrupted file, a file or folder readable or writable by another account, a missing folder, a value with a line break each stop the startup; Fedora: the folder of the credentials is read under its keys, an unknown credential and a value with a line break stop the startup (automated, on both systems, over a plain folder); the decryption by systemd itself (`systemd-creds`, `LoadCredentialEncrypted=`) is checked by hand in the WSL `FedoraLinux-44`; Windows: `tools/vault/Set-VaultSecret.ps1` writes a secret the vault reads (elevated session only) and refuses to run otherwise; everywhere: a secret of the tier from an environment variable (any prefix, `SQLCONNSTR_` included), a JSON file or the command line stops the startup outside Development and is accepted in Development, with the warning; a variable of the machine that is not a secret of the tier is ignored; a connection string not encrypted or trusting the certificate of a remote server stops the Very Back outside Development; no message holds a value.
- **Done criterion of 0.7.0**: `DEV_` is deployed from scratch by one script; the application user executes the procedures but cannot read the tables; an update of the audit is rejected.

## Decisions left to the project lead

| Decision | Options | Proposed |
|----------|---------|----------|
| How the rows reach a backup before their purge | (a) The purge refuses to run unless a full backup of the database finished after the newest row to purge (read from `msdb`); (b) an export of the rows (JSON or BCP) by the administration before the purge; (c) the backup policy of the server, outside the database | None: to be decided |
| Updates of the development tools | SQL Server 2025 CU10 + GDR (17.0.5045.3) instead of the GDR branch; Visual Studio 2026 updated for SqlPackage 170.5.96 or later (fixes of the deployment of triggers and default constraints); PowerShell 7 on the runtime 10.0.15 (7.6.9 ships its own 10.0.14); the sqlcmd utilities | All, by the author on his machines (CVE audits of this document and of the code) |
| Validation of the choices proposed here and not yet approved | DPAPI scope `LocalMachine`; the account of the service trusted with its own vault; a login of the server with SQL authentication (`CHECK_POLICY` on, no expiration) rather than a contained user; an encrypted connection required outside Development | As written in this document |
| A server-level SQL Server Audit, to a file outside the database | It would see what the administrators do to the audit (decided with the native installation, 0.25.0) | Yes, to a file target |
| `VIEW DEFINITION` of the application | On the database (as written here, the reading of the metadata of the design of the author) or on the schemas of the platform only, which hides the principals and the row counts (0.8.0) | On the database, as designed |
| Secrets in the history of a temporal table | A column that holds a secret or its hash is audited as `"changed"`; whether its table may be temporal is decided with the first such column (0.8.0) | Not temporal, or the column kept out of the history |
| GDPR and NIS2 for the audit | The history tables are not purged; the login of an administrator in the audit is personal data | With the GDPR work of the author |
