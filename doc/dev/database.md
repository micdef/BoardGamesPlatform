# Database foundation

This document specifies the database foundation delivered in 0.7.0 (schemas, filegroups, audit, the write and read patterns, the application principal, the deployment script) and the vault of the secrets of the tiers, and records their threat model.
It is written **before the code** and reviewed before any line of it, as the secure channel was in 0.6.0 ([secure-channel.md](secure-channel.md)).

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
A clustered index on `ActionDT` would serve the purge by dates; the primary key on `Id` stays non-clustered so that random GUIDs do not split the pages (a departure from AckSolutions, whose key is clustered).

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

Constraints: `Id` not empty; `Code` unique, not empty, matching its pattern; `Kind` among its three values; `Value` consistent with `Kind`.
System-versioned: `WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [History].[Platform_Settings]))`, so that every former value of a setting stays readable with the time it was valid.
The `Kind` of a setting never changes after its insert: the update procedure only takes a new value.

## The audit

### Immutability

| Statement | Result |
|-----------|--------|
| `INSERT` | Only through `Logs.SP_Logs_Audit_Insert`, called by the procedures of `StorProc` in their own transaction |
| `UPDATE` | Always refused by the trigger `TR_Logs_Audit_IOfUpdate` (`INSTEAD OF UPDATE`, error 50001) |
| `DELETE` | Refused by the trigger `TR_Logs_Audit_IOfDelete` (`INSTEAD OF DELETE`, error 50001), except inside the purge |
| `TRUNCATE`, `ALTER TABLE`, `DISABLE TRIGGER`, `DROP` | Not caught by a DML trigger: they need `ALTER` on the table, which only the owner and the administrators of the database hold. The application role is explicitly denied every right on the schema `Logs` |

A ledger table would refuse all of these to the administrators too, but it can never be purged: the purge of the audit is a requirement of the project lead (by dates, sliding, to a backup), so **ledger tables are excluded** (decided by the project lead in 0.7.0).

### The purge

`Logs.SP_Logs_Audit_Purge @Before DATE` deletes the audit rows written before `@Before` (UTC), and only them:

1. `@Before` must be earlier than today (UTC): the rows of the current day are never purged; a `NULL` date is refused.
2. In one transaction, it opens the gate (`sp_set_session_context N'AllowAuditPurge', 1`), deletes the rows, closes the gate (`NULL`), then writes **one audit row of the purge itself** (the date given, the number of rows deleted, the oldest and the newest `ActionDT` deleted). The purge leaves a trace that it cannot erase, since that row is dated today.
3. On any error, it rolls back, closes the gate and rethrows: no partial purge, no gate left open in the session.

The procedure is in the schema `Logs`, which the application cannot execute: **a purge is an act of administration**, never of the platform.
The archive of the rows before their purge (export, or a backup of the database proved before the purge) is left to the project lead.

**Strength of the gate.** The session key can be set by anyone (`sp_set_session_context` is granted to public), so the gate itself only protects against a `DELETE` by mistake. The real barrier is the right to delete: only `dbo` and the members of `db_owner` hold `DELETE` on `Logs.Audit`, and they can also disable the trigger. The audit is therefore immutable against the application and against any principal limited like it, not against an administrator of the database; that is a residual risk (see [Threat model](#threat-model)).

### The actor

Until 0.8.0 there are no accounts. Every procedure of `StorProc` takes the actor of the change (`@IdActor`) and refuses it when `Functions.IsValidActorId` does not know it:

- in 0.7.0, the only known actor is **the platform itself**, a fixed identifier returned by `Functions.SystemActorId()` (the deployment, the settings written by an administrator through the platform);
- in 0.8.0, `IsValidActorId` also accepts the identifiers of `Identity.Users`, and a foreign key is added from `Logs.Audit.IdActor` to the users, with the platform as a row of its own.

The actor is what the caller claims; `LoginName` is what SQL Server knows. Both are kept, so that a wrong actor sent by a tier never hides which login wrote.

## Write pattern

A write procedure of `StorProc` has this shape (from `SP_Core_Languages_Insert` of AckSolutions):

```text
SP_<Schema>_<Table>_<Action> @Rows [Types].[TVP_<Schema>_<Table>_<Action>] READONLY, @IdActor UNIQUEIDENTIFIER, @Result NVARCHAR(MAX) OUTPUT
```

1. `SET NOCOUNT ON; SET XACT_ABORT ON;`, `TRY`, one transaction.
2. **Checks of the call**, each one `THROW 51000` and nothing written: no row; `@IdActor` empty or unknown.
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
| Schema `Types` | `EXECUTE`, `REFERENCES` | To declare and pass the table-valued parameters |
| Database | `VIEW DEFINITION` | To read the metadata (it sees the tables without being able to query them); the checks of the audit (`IsValidSchemaName`, `IsValidTableName`) depend on it |
| Schemas `Logs`, `Platform`, `History`, `Functions`, `dbo` | `DENY` of every right (`SELECT`, `INSERT`, `UPDATE`, `DELETE`, `EXECUTE`, `ALTER`, `CONTROL`) | A grant added by mistake later, directly or through `public`, cannot open them |

The test of the permission matrix reads every permission and membership of the role and compares it with this list, then plays a member of the role: every procedure of `StorProc` and every view can be used; every table, the purge, the insert of the audit and any creation are refused with error 229.

## Application principal

| Topic | Choice | Why |
|-------|--------|-----|
| Kind | A **login of the server** and a user of the database mapped to it, both named after the database (`DEV_BoardGamesPlatform`) | A contained user needs the server option `contained database authentication` and a partially contained database: a change of the configuration of the server, refused for the author's servers |
| Authentication | SQL authentication, `CHECK_POLICY = ON`, `CHECK_EXPIRATION = OFF`, `DEFAULT_DATABASE` set | The Very Back can run on Fedora, where a Windows account is not available; the password is long and random, so expiration only breaks the service |
| Password | 32 random bytes (`RandomNumberGenerator`), written in Base64, made by the deployment script, never shown, never written to a file in clear | Strong enough to make guessing useless; the script is the only one to see it |
| Server rights | `CONNECT SQL` only, plus `DENY VIEW ANY DATABASE` | The login does not even list the other databases of the server |
| Where it goes | The vault of the Very Back (or its user secrets on a developer machine), as the full connection string `ConnectionStrings:Main` | The Very Back is the only tier that reaches the database |

The login and the user are **not part of the DACPAC**: a login is a server object, and a user mapped to a login would break the deployment on any other server. The deployment script creates them after the publish.

## Deployment script

`tools/database/Deploy-TestDatabase.ps1` already refuses any database without a `DEV_` or `TEST_` prefix (0.3.0). In 0.7.0 it deploys a database from scratch in one run:

1. **Guard**, before anything: the name matches `^(DEV|TEST)_[A-Za-z0-9_]+\z`, case-sensitive.
2. Build the DACPAC (MSBuild of Visual Studio, the project keeps its original format), unless `-SkipBuild`.
3. **Drop the old login** of the same name, if it exists and only if its name has the same prefix: its sessions are closed first.
4. Publish with `CreateNewDatabase=True` (the database is dropped and recreated), files under the default paths of the server.
5. Create the login with a new random password, the user, its membership of `ApplicationRole`, and the `DENY VIEW ANY DATABASE`.
6. Send the connection string to the place named by `-SecretTarget`:
   - `None` (default): nowhere; the login exists but nobody knows its password (enough for the SQL tests, which play the user with `EXECUTE AS`);
   - `UserSecrets`: the user secrets of the Very Back, through the standard input of `dotnet user-secrets set` (never on a command line, visible in the list of processes);
   - `Vault`: the vault folder of the Very Back given by `-VaultPath`, through `tools/vault/Set-VaultSecret.ps1`.
7. Clear the password from the memory of the script.

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
builder.Configuration.AddPlatformVault(builder.Environment);
```

| Host | Source | Where the secrets are |
|------|--------|-----------------------|
| Fedora, systemd unit | `$CREDENTIALS_DIRECTORY`, read by the key-per-file provider of Microsoft (`Microsoft.Extensions.Configuration.KeyPerFile`) | Each secret is encrypted by `systemd-creds encrypt` (key of the host, and the TPM2 when present) under `/etc/credstore.encrypted/`; the unit names it with `LoadCredentialEncrypted=<name>:<file>`; systemd decrypts it at start into a private, non-swappable folder that only the service can read |
| Windows service | A provider of the platform over the folder named by `Vault:Path` | Each secret is a file encrypted with DPAPI (`ProtectedData`), whose access list gives read to the account of the service and to the administrators only |
| Developer machine, tests | None | User secrets (Development) and in-memory settings, as today |

The name of a secret is its configuration key with `__` for `:` (`Tls__CertificatePassword`), the form both providers map back to the key.

Rules:

1. **Outside the `Development` environment, a secret comes from the vault only.** A secret key (same rule as the CI: a segment ending with `Password`, `Pwd`, `Secret`, `ApiKey`, `Token`, `PrivateKey` or `ConnectionString`) set by an environment variable or a command-line argument stops the startup, naming the key. The environment variables of 0.5.0 and 0.6.0 are therefore removed from the services when the vault is set up.
2. **A vault that is configured but unreadable stops the startup**: a missing folder, a file that cannot be decrypted, a file readable by other accounts (Windows), each named, never its content.
3. On Windows, each file is bound to its name: the DPAPI entropy is the name of the secret, so a file copied under another name does not decrypt.
4. No secret is ever logged; the error messages name the setting and the file, never a value (rule 3 of [configuration.md](../adm/configuration.md)).

**DPAPI scope.** `CurrentUser` binds a secret to the account of the service: another account of the machine cannot decrypt it even with a wrong access list, but the file must be written while running as that account (a virtual account `NT SERVICE\<name>` cannot log on). `LocalMachine` lets any process of the machine decrypt a file it can read, so its protection is the access list of the file, like the credentials of systemd (readable by root and the service). The choice is made with the security review of this document; the default proposed is `LocalMachine` with the access list checked at load, the same model on both systems.

The tool that writes a secret on Windows is `tools/vault/Set-VaultSecret.ps1` (reads the value from the standard input or a prompt, never from an argument; encrypts; sets the access list). On Fedora, the commands of `systemd-creds` are documented in [configuration.md](../adm/configuration.md); the full installation of the units comes with 0.25.0.

## Threat model

### Assets

- The data of the platform and its audit.
- The password of the application principal, and the two passwords of the keys of each tier.

### Assumptions

- The SQL Server, its host and the hosts of the tiers are administered by trusted people; their operating systems and .NET are kept up to date.
- The link Back → Very Back is protected by 0.5.0 and 0.6.0: only the Very Back connects to the database.
- The SQL Server is reached over an encrypted connection (`Encrypt=True`, the default of `Microsoft.Data.SqlClient`).

### In scope

| Threat | Protection |
|--------|------------|
| A compromised or buggy Very Back reads or changes a table directly | The application principal has no right on the tables; it only executes the procedures and reads the views; `DENY` on the internal schemas |
| SQL injection through the inputs | No dynamic SQL in the procedures; inputs are parameters and table-valued parameters; the Very Back never builds SQL text |
| A change or a deletion of the audit by the application | No right on `Logs`; triggers refuse `UPDATE` and `DELETE`; the purge is not executable by the application |
| A write without its audit, or an audit without its write | Same transaction, `XACT_ABORT ON`, rollback on any error |
| A purge that hides itself | The purge writes its own audit row, dated today, which no purge of the same day can delete |
| A gate of purge left open in a pooled session | The purge closes it in its `CATCH`; `sp_reset_connection` clears the session context when a pooled connection is reused |
| A wrong actor claimed by a tier | `LoginName` records the login; an unknown actor is refused |
| A partial write, a row lost without a reason | One result per input row, success or reason |
| The password of the application principal read from a file, a backup of a disk, the list of processes | Never on a command line, never in clear in a file; stored encrypted by the OS (DPAPI, `systemd-creds`) |
| A secret left in an environment variable | Refused at startup outside Development |
| A stolen vault file copied to another machine | DPAPI and `systemd-creds` keys belong to the machine (and its TPM2 on Fedora when present) |
| The deployment script run against a real database | The prefix guard, before any build or connection; the login dropped only with the same prefix |
| The login used to explore the server | `CONNECT SQL` only, `DENY VIEW ANY DATABASE` |

### Out of scope, residual risks

- **An administrator of the database or of the server** can disable the triggers, change or truncate the audit, read every secret of the machine where he is administrator. The audit is immutable against the platform, not against its administrators. A server-level SQL Server Audit, written outside the database, would detect it; it belongs to the installation (0.25.0) and is noted for the project lead.
- **The metadata right** (`VIEW DEFINITION`) lets the application read the definition of every object; the code is public in the repository, so this discloses nothing new.
- **Secrets in memory**: a .NET `string` cannot be erased, so a dump of the memory of a tier shows its secrets; the vault protects them at rest only.
- **A tier compromised while running** can use its own login as the platform does: the permission matrix limits the damage to what the procedures allow.
- **The archive before the purge**: until its mechanism is decided, a purge deletes the rows without proof that a backup holds them.

## Tests

- **Per group of procedures**, one test class: the insert and update of the settings (success, each rule of rejection, a row that breaks two rules comes out once, the call refused with no row or an unknown actor, one JSON result per input row in the documented shape).
- **Audit**: one row per written row, same transaction (a failure after the write leaves neither); the values only; `UPDATE` and `DELETE` refused with 50001, even as `dbo`; the gate opened by hand does not let the application delete; the purge deletes only before the date, refuses today, a future date and `NULL`, writes its own row, and leaves the gate closed after a failure.
- **Temporal history**: an update leaves the former value in `History.Platform_Settings` with its period; `FOR SYSTEM_TIME AS OF` returns the former value.
- **Permission matrix**: the declared permissions and memberships of the role, then a member of the role (and the real application user deployed by the script) is allowed every procedure and view, and refused every table, `Logs`, `Functions`, a creation, with error 229.
- **Conventions**: every object of the project has its header; every procedure of `StorProc` has a test class (a new procedure without a test fails); every table of a module is on the filegroup `Core`; every temporal table has its history in `History`.
- **Deployment**: the guard refuses a wrong name before any build; a deployment from scratch creates the login, the user and its role; a second run replaces the password; the password never appears in the output.
- **Vault**: Windows (DPAPI): a secret written by the tool is read under its key; a file with another name, a corrupted file, a file readable by everyone, a missing folder each stop the startup; Fedora: a credential given by `systemd-run -p LoadCredentialEncrypted=` is read under its key (in the WSL `FedoraLinux-44`); everywhere: a secret key from an environment variable stops the startup outside Development and is accepted in Development; no message holds a value.
- **Done criterion of 0.7.0**: `DEV_` is deployed from scratch by one script; the application user executes the procedures but cannot read the tables; an update of the audit is rejected.

## Decisions left to the project lead

| Decision | Options | Proposed |
|----------|---------|----------|
| How the rows reach a backup before their purge | (a) The purge refuses to run unless a full backup of the database finished after the newest row to purge (read from `msdb`); (b) an export of the rows (JSON or BCP) by the administration before the purge; (c) the backup policy of the server, outside the database | None: to be decided |
| DPAPI scope on Windows | `LocalMachine` with a checked access list, or `CurrentUser` of the account of the service | `LocalMachine`, unless the security review says otherwise |
