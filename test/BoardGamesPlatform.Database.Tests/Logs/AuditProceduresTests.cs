/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Logs/AuditProceduresTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the procedures of the audit (insert, purge) and the immutability of its rows
 */

// Using
using System.Text.Json;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Logs;

/// <summary>
/// Tests used to check the group of procedures of Logs.Audit, and the triggers that keep its rows immutable
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of version 0.7.0 says that an update of the audit is rejected; the audit is also the proof of who
/// changed what, so every row it holds must be one that a procedure wrote, and only the purge may remove rows, by dates.</para>
/// <para><b>Behavior:</b> every row a test writes carries a marker (a GUID) in its value, so that the test reads its own rows only. The rows dated
/// in the past are written directly in the table, as its owner, since the procedures always date a row with the current time; they are dated in
/// the year 2001, which no other test uses, and purged at the end of each test. The tests of this class run one after the other (xUnit runs
/// the tests of one class in sequence), so that a purge never removes the rows of another test. Skipped when no throwaway database is
/// configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
public sealed class AuditProceduresTests
{
    #region Members

    #region Constants

    /// <summary>
    /// The identifier of the actor "the platform itself" (Functions.SystemActorId)
    /// </summary>
    private const string SystemActorId = "5778C6C3-5A1A-4C0A-8878-F36CEECB523A";

    /// <summary>
    /// Error number of SQL Server when a CHECK constraint refuses a row (here, a constraint of a table-valued parameter)
    /// </summary>
    private const int ConstraintViolation = 547;

    /// <summary>
    /// Error number raised by the temporary trigger that makes the purge fail after its gate is open
    /// </summary>
    private const int ForcedFailure = 50999;

    /// <summary>
    /// Error number of SQL Server when a read-only key of the session context is changed
    /// </summary>
    private const int ReadOnlyKey = 15664;

    #endregion

    #region Variables

    /// <summary>
    /// Expected properties of the result of a purge that deleted rows, in this order
    /// </summary>
    private static readonly string[] _purgeProperties = new string[] { "Before", "Rows", "OldestActionDT", "NewestActionDT" };

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Enumerations

    // #N/A

    #endregion

    #region Services

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members & Properties

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build a batch that inserts audit rows through Logs.SP_Logs_Audit_Insert
    /// </summary>
    /// <param name="rows">[Cannot be null] The rows, each one a list of five T-SQL values: actor, schema, table, old value, new value</param>
    /// <returns>
    /// The batch, which declares the table-valued parameter, fills it and calls the procedure
    /// </returns>
    private static string InsertBatch(params string[] rows)
    {
        // Setting the batch
        string values = string.Join(", ", rows.Select(row => $"({row})"));

        // Function's return
        return $"""
            DECLARE @rows [Types].[TVP_Logs_Audit_Insert];
            INSERT INTO @rows ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue]) VALUES {values};
            EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @rows;
            """;
    }

    /// <summary>
    /// Method used to count the audit rows whose new value holds a marker
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="marker">[Cannot be null] Marker of the test (a GUID)</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The number of rows
    /// </returns>
    private static async Task<int> CountMarkedRowsAsync(SqlConnection connection, string marker, CancellationToken cancellationToken)
    {
        // Function's return
        return (int)(await SqlBatch.ScalarAsync(connection, $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [NewValue] LIKE N'%{marker}%';", cancellationToken))!;
    }

    /// <summary>
    /// Method used to create a temporary table of the schema Logs, that the old audit rows of a test name as their changed table
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, as the owner of the database</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The name of the table (from a GUID: safe to put in the batches); the test drops it with <see cref="DropTargetTableAsync"/>
    /// </returns>
    /// <remarks>
    /// <para><b>Purpose:</b> the purge never deletes the rows about Logs.Audit itself (they record the purges), so the rows a purge test expects to
    /// be deleted must name another table. The CHECK constraints of the audit only check the table when a row is written: a row keeps naming
    /// the table after it is dropped.</para>
    /// </remarks>
    private static async Task<string> CreateTargetTableAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        // Creating the table
        string tableName = $"TestAuditTarget_{Guid.NewGuid():N}";
        await SqlBatch.ExecuteAsync(connection, $"CREATE TABLE [Logs].[{tableName}] ([Id] INT NOT NULL) ON [Core];", cancellationToken);

        // Function's return
        return tableName;
    }

    /// <summary>
    /// Method used to drop a temporary table made by <see cref="CreateTargetTableAsync"/>, if it exists
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, as the owner of the database</param>
    /// <param name="tableName">[Cannot be null] Name of the table</param>
    /// <returns>
    /// A task that completes when the table is dropped
    /// </returns>
    private static async Task DropTargetTableAsync(SqlConnection connection, string tableName)
    {
        // Dropping the table
        await SqlBatch.ExecuteAsync(connection, $"IF OBJECT_ID(N'[Logs].[{tableName}]', N'U') IS NOT NULL DROP TABLE [Logs].[{tableName}];", CancellationToken.None);
    }

    /// <summary>
    /// Method used to write an audit row dated in the past, directly in the table, as its owner
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, as the owner of the database</param>
    /// <param name="tableName">[Cannot be null] Table of the schema Logs that the row names as its changed table</param>
    /// <param name="actionDate">[Cannot be null] Date and time of the row, in T-SQL form ('2001-01-01T10:00:00')</param>
    /// <param name="marker">[Cannot be null] Marker of the test (a GUID)</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the row is written
    /// </returns>
    private static async Task WriteOldRowAsync(SqlConnection connection, string tableName, string actionDate, string marker, CancellationToken cancellationToken)
    {
        // Writing the row
        await SqlBatch.ExecuteAsync(connection, $$"""
            INSERT INTO [Logs].[Audit] ([IdActor], [ActionDT], [SchemaName], [TableName], [NewValue])
            VALUES ('{{SystemActorId}}', '{{actionDate}}', N'Logs', N'{{tableName}}', N'{"marker":"{{marker}}"}');
            """, cancellationToken);
    }

    /// <summary>
    /// Method used to play a user without login, member of a role, for one check, then to drop it
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, as the owner of the database</param>
    /// <param name="roleName">[Cannot be null] Role of the user (a fixed role such as db_owner, or a role of the project)</param>
    /// <param name="check">[Cannot be null] The check, run while acting as the user</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the check has run and the user is dropped, even when the check failed
    /// </returns>
    private static async Task AsMemberOfAsync(SqlConnection connection, string roleName, Func<Task> check, CancellationToken cancellationToken)
    {
        // Creating the user, member of the role
        string userName = $"TestAuditUser_{Guid.NewGuid():N}";
        await SqlBatch.ExecuteAsync(connection, $"CREATE USER [{userName}] WITHOUT LOGIN; ALTER ROLE [{roleName}] ADD MEMBER [{userName}];", cancellationToken);
        try
        {
            // Acting as the user for the check
            await SqlBatch.ExecuteAsync(connection, $"EXECUTE AS USER = N'{userName}';", cancellationToken);
            try
            {
                await check();
            }
            finally
            {
                await SqlBatch.ExecuteAsync(connection, "EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = NULL; REVERT;", CancellationToken.None);
            }
        }
        finally
        {
            // Dropping the user
            await SqlBatch.ExecuteAsync(connection, $"ALTER ROLE [{roleName}] DROP MEMBER [{userName}]; DROP USER [{userName}];", CancellationToken.None);
        }
    }

    /// <summary>
    /// Method used to call the purge
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="before">T-SQL value of the date of the purge ('2001-01-02', NULL...)</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The JSON result of the purge
    /// </returns>
    private static async Task<string> PurgeAsync(SqlConnection connection, string before, CancellationToken cancellationToken)
    {
        // Calling the procedure
        object? result = await SqlBatch.ScalarAsync(connection, $"""
            DECLARE @result NVARCHAR(MAX);
            EXEC [Logs].[SP_Logs_Audit_Purge] @Before = {before}, @Result = @result OUTPUT;
            SELECT @result;
            """, cancellationToken);

        // Function's return
        return result.ShouldBeOfType<string>();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The insert writes one audit row per input row, dated now in UTC, with the SQL login of the session, whatever the caller says
    /// </summary>
    [Fact]
    public async Task TheInsertWritesEveryRowWithTheUtcTimeAndTheLoginAsync()
    {
        // Inserting two rows, between two readings of the UTC clock of the server
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        DateTime before = (DateTime)(await SqlBatch.ScalarAsync(connection, "SELECT SYSUTCDATETIME();", token))!;
        await SqlBatch.ExecuteAsync(connection, InsertBatch(
            $"'{SystemActorId}', N'Logs', N'Audit', NULL, N'{{\"marker\":\"{marker}\",\"row\":1}}'",
            $"'{SystemActorId}', N'Logs', N'Audit', N'{{\"row\":0}}', N'{{\"marker\":\"{marker}\",\"row\":2}}'"), token);
        DateTime after = (DateTime)(await SqlBatch.ScalarAsync(connection, "SELECT SYSUTCDATETIME();", token))!;

        // Reading the rows written
        string login = (string)(await SqlBatch.ScalarAsync(connection, "SELECT ORIGINAL_LOGIN();", token))!;
        List<(Guid Actor, string Login, DateTime ActionDate)> rows = new List<(Guid Actor, string Login, DateTime ActionDate)>();
        await using (SqlCommand command = new SqlCommand($"SELECT [IdActor], [LoginName], [ActionDT] FROM [Logs].[Audit] WHERE [NewValue] LIKE N'%{marker}%';", connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                rows.Add((reader.GetGuid(0), reader.GetString(1), reader.GetDateTime(2)));
        }

        // Checks
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(row => row.Actor == Guid.Parse(SystemActorId) && row.Login == login && row.ActionDate >= before && row.ActionDate <= after);
    }

    /// <summary>
    /// The insert refuses a call without any row
    /// </summary>
    [Fact]
    public async Task TheInsertRefusesACallWithoutRowAsync()
    {
        // Calling the procedure with an empty table-valued parameter
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, "DECLARE @rows [Types].[TVP_Logs_Audit_Insert]; EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @rows;",
            SqlBatch.CheckFailed, token);
    }

    /// <summary>
    /// The insert refuses a row that names an unknown actor, a missing schema or table, a view, or a value that is not JSON; and the table-valued
    /// parameter itself refuses an empty actor, an empty name, or a row without any value
    /// </summary>
    /// <param name="actor">T-SQL value of the actor</param>
    /// <param name="schemaName">T-SQL value of the schema</param>
    /// <param name="tableName">T-SQL value of the table</param>
    /// <param name="newValue">T-SQL value of the new value (the old value is NULL)</param>
    /// <param name="expectedError">Error number expected: 51000 from the procedure, 547 from the table-valued parameter</param>
    [Theory]
    [InlineData("NEWID()", "N'Logs'", "N'Audit'", "N'{}'", SqlBatch.CheckFailed)]
    [InlineData("'" + SystemActorId + "'", "N'NoSuchSchema'", "N'Audit'", "N'{}'", SqlBatch.CheckFailed)]
    [InlineData("'" + SystemActorId + "'", "N'Logs'", "N'NoSuchTable'", "N'{}'", SqlBatch.CheckFailed)]
    [InlineData("'" + SystemActorId + "'", "N'INFORMATION_SCHEMA'", "N'TABLES'", "N'{}'", SqlBatch.CheckFailed)]
    [InlineData("'" + SystemActorId + "'", "N'Logs'", "N'Audit'", "N'not json'", SqlBatch.CheckFailed)]
    [InlineData("'00000000-0000-0000-0000-000000000000'", "N'Logs'", "N'Audit'", "N'{}'", ConstraintViolation)]
    [InlineData("'" + SystemActorId + "'", "N' '", "N'Audit'", "N'{}'", ConstraintViolation)]
    [InlineData("'" + SystemActorId + "'", "N'Logs'", "N'Audit'", "NULL", ConstraintViolation)]
    public async Task TheInsertRefusesAnInvalidRowAsync(string actor, string schemaName, string tableName, string newValue, int expectedError)
    {
        // Calling the procedure with the invalid row
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, InsertBatch($"{actor}, {schemaName}, {tableName}, NULL, {newValue}"), expectedError, token);
    }

    /// <summary>
    /// The insert writes every row or none: one invalid row among valid ones leaves no row written
    /// </summary>
    [Fact]
    public async Task TheInsertWritesEveryRowOrNoneAsync()
    {
        // Calling the procedure with a valid row and a row that names a missing table
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        await SqlBatch.ShouldFailWithAsync(connection, InsertBatch(
            $"'{SystemActorId}', N'Logs', N'Audit', NULL, N'{{\"marker\":\"{marker}\"}}'",
            $"'{SystemActorId}', N'Logs', N'NoSuchTable', NULL, N'{{\"marker\":\"{marker}\"}}'"), SqlBatch.CheckFailed, token);

        // Check
        (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(0);
    }

    /// <summary>
    /// An update of the audit is refused, even to the owner of the database, and the row keeps its value
    /// </summary>
    [Fact]
    public async Task AnUpdateOfTheAuditIsRefusedEvenToTheOwnerAsync()
    {
        // Writing a row, then trying to change it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        await SqlBatch.ExecuteAsync(connection, InsertBatch($"'{SystemActorId}', N'Logs', N'Audit', NULL, N'{{\"marker\":\"{marker}\"}}'"), token);
        await SqlBatch.ShouldFailWithAsync(connection, $"UPDATE [Logs].[Audit] SET [NewValue] = N'{{}}' WHERE [NewValue] LIKE N'%{marker}%';",
            SqlBatch.AuditImmutable, token);

        // Check
        (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(1);
    }

    /// <summary>
    /// A delete of the audit is refused, even to the owner of the database, when the gate of the purge is closed
    /// </summary>
    [Fact]
    public async Task ADeleteOfTheAuditIsRefusedEvenToTheOwnerAsync()
    {
        // Writing a row, then trying to delete it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        await SqlBatch.ExecuteAsync(connection, InsertBatch($"'{SystemActorId}', N'Logs', N'Audit', NULL, N'{{\"marker\":\"{marker}\"}}'"), token);
        await SqlBatch.ShouldFailWithAsync(connection, $"DELETE FROM [Logs].[Audit] WHERE [NewValue] LIKE N'%{marker}%';", SqlBatch.AuditImmutable, token);

        // Check
        (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(1);
    }

    /// <summary>
    /// The purge refuses a missing date, today and a future date (UTC): the rows of the current day are never purged
    /// </summary>
    /// <param name="before">T-SQL value of the date of the purge</param>
    [Theory]
    [InlineData("NULL")]
    [InlineData("CAST(SYSUTCDATETIME() AS DATE)")]
    [InlineData("DATEADD(DAY, 1, CAST(SYSUTCDATETIME() AS DATE))")]
    public async Task ThePurgeRefusesADateThatIsNotInThePastAsync(string before)
    {
        // Calling the purge with the refused date (the date is computed in its own variable, since EXEC takes no expression)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection,
            $"DECLARE @before DATE = {before}, @result NVARCHAR(MAX); EXEC [Logs].[SP_Logs_Audit_Purge] @Before = @before, @Result = @result OUTPUT;",
            SqlBatch.CheckFailed, token);
    }

    /// <summary>
    /// The purge refuses to start when the key of its gate is already set in the session: a key set read-only could not be closed afterwards
    /// </summary>
    [Fact]
    public async Task ThePurgeRefusesAGateAlreadySetInTheSessionAsync()
    {
        // Setting the key read-only, then calling the purge, in a session of its own (a read-only key stays until the session ends)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(builder => builder.Pooling = false, token);
        await SqlBatch.ExecuteAsync(connection, "EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = 0, @read_only = 1;", token);

        // Check: the refusal of the purge itself (51000), not the error of a key that cannot be changed (15664)
        await SqlBatch.ShouldFailWithAsync(connection, "DECLARE @result NVARCHAR(MAX); EXEC [Logs].[SP_Logs_Audit_Purge] @Before = '2001-01-01', @Result = @result OUTPUT;",
            SqlBatch.CheckFailed, token);
    }

    /// <summary>
    /// A key set read-only to NULL passes the check of the purge, but the engine refuses to open the gate: the purge fails before any delete
    /// </summary>
    [Fact]
    public async Task ThePurgeFailsBeforeAnyDeleteWhenTheKeyIsReadOnlyNullAsync()
    {
        // Writing an old row, then setting the key read-only to NULL in a session of its own
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection owner = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        string tableName = await CreateTargetTableAsync(owner, token);
        try
        {
            await WriteOldRowAsync(owner, tableName, "2001-04-01T10:00:00", marker, token);
            await using (SqlConnection connection = await TestDatabase.OpenAsync(builder => builder.Pooling = false, token))
            {
                await SqlBatch.ShouldFailWithAsync(connection, """
                    EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = NULL, @read_only = 1;
                    DECLARE @result NVARCHAR(MAX);
                    EXEC [Logs].[SP_Logs_Audit_Purge] @Before = '2001-12-31', @Result = @result OUTPUT;
                    """, ReadOnlyKey, token);
            }

            // Check
            (await CountMarkedRowsAsync(owner, marker, token)).ShouldBe(1);
        }
        finally
        {
            await DropTargetTableAsync(owner, tableName);
            await PurgeAsync(owner, "'2001-12-31'", CancellationToken.None);
        }
    }

    /// <summary>
    /// The purge deletes the rows written before its date and only them, returns what it deleted, records itself in the audit, and leaves the
    /// gate closed
    /// </summary>
    [Fact]
    public async Task ThePurgeDeletesOnlyTheRowsBeforeItsDateAndRecordsItselfAsync()
    {
        // Writing a row on the 1st and on the 3rd of January 2001, then purging before the 2nd
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string olderMarker = Guid.NewGuid().ToString("N");
        string newerMarker = Guid.NewGuid().ToString("N");
        string tableName = await CreateTargetTableAsync(connection, token);
        try
        {
            await WriteOldRowAsync(connection, tableName, "2001-01-01T10:00:00", olderMarker, token);
            await WriteOldRowAsync(connection, tableName, "2001-01-03T10:00:00", newerMarker, token);
            string startedAt = (string)(await SqlBatch.ScalarAsync(connection, "SELECT CONVERT(NVARCHAR(30), SYSUTCDATETIME(), 126);", token))!;
            string result = await PurgeAsync(connection, "'2001-01-02'", token);

            // Reading the result and what is left (the row of this purge only: an earlier run of the test wrote the same JSON)
            using JsonDocument document = JsonDocument.Parse(result);
            int purgeRows = (int)(await SqlBatch.ScalarAsync(connection,
                $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [NewValue] = N'{result.Replace("'", "''", StringComparison.Ordinal)}' AND [OldValue] IS NULL AND [ActionDT] >= '{startedAt}';", token))!;
            object? gate = await SqlBatch.ScalarAsync(connection, "SELECT SESSION_CONTEXT(N'AllowAuditPurge');", token);

            // Checks
            document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(_purgeProperties);
            document.RootElement.GetProperty("Before").GetString().ShouldBe("2001-01-02");
            document.RootElement.GetProperty("Rows").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
            document.RootElement.GetProperty("NewestActionDT").GetString().ShouldBe("2001-01-01T10:00:00");
            (await CountMarkedRowsAsync(connection, olderMarker, token)).ShouldBe(0);
            (await CountMarkedRowsAsync(connection, newerMarker, token)).ShouldBe(1);
            purgeRows.ShouldBe(1, "The purge must write one audit row of itself, holding its result");
            gate.ShouldBe(DBNull.Value);
        }
        finally
        {
            // Purging the rows of 2001 left by the test, then dropping its table
            await PurgeAsync(connection, "'2001-12-31'", CancellationToken.None);
            await DropTargetTableAsync(connection, tableName);
        }
    }

    /// <summary>
    /// The purge never deletes the rows that record a purge (the rows about Logs.Audit itself), whatever their date
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the old row about Logs.Audit written by this test stays in the audit for good, as the rows of real purges do.</para>
    /// </remarks>
    [Fact]
    public async Task ThePurgeNeverDeletesTheRowsOfEarlierPurgesAsync()
    {
        // Writing an old row about Logs.Audit, as an earlier purge would have, then purging after its date
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        await WriteOldRowAsync(connection, "Audit", "2001-02-01T10:00:00", marker, token);
        await PurgeAsync(connection, "'2001-12-31'", token);

        // Check
        (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(1);
    }

    /// <summary>
    /// No user but dbo can delete an audit row, even with the key of the gate set: neither a member of db_datawriter, nor a member of db_owner
    /// </summary>
    /// <param name="roleName">Fixed role of the user</param>
    /// <remarks>
    /// <para><b>Purpose:</b> the gate is not a barrier, since anyone can set its key; the barrier is the DENY of the table to public (security
    /// review of doc/dev/database.md, finding B2).</para>
    /// </remarks>
    [Theory]
    [InlineData("db_datawriter")]
    [InlineData("db_owner")]
    public async Task AUserWithTheGateSetStillCannotDeleteTheAuditAsync(string roleName)
    {
        // Writing a row, then trying to delete it as a member of the role, with the key set
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        await SqlBatch.ExecuteAsync(connection, InsertBatch($"'{SystemActorId}', N'Logs', N'Audit', NULL, N'{{\"marker\":\"{marker}\"}}'"), token);
        await AsMemberOfAsync(connection, roleName, () => SqlBatch.ShouldFailWithAsync(connection,
            $"EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = 1; DELETE FROM [Logs].[Audit] WHERE [NewValue] LIKE N'%{marker}%';",
            SqlBatch.PermissionDenied, token), token);

        // Check
        (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(1);
    }

    /// <summary>
    /// A member of the role AuditPurgeRole purges the audit, without any right on the table itself
    /// </summary>
    [Fact]
    public async Task AMemberOfThePurgeRoleCanPurgeAsync()
    {
        // Writing an old row, then purging as a member of the role
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        string tableName = await CreateTargetTableAsync(connection, token);
        try
        {
            await WriteOldRowAsync(connection, tableName, "2001-03-01T10:00:00", marker, token);
            await AsMemberOfAsync(connection, "AuditPurgeRole", async () =>
            {
                await PurgeAsync(connection, "'2001-12-31'", token);
                await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [Logs].[Audit];", SqlBatch.PermissionDenied, token);
            }, token);

            // Check
            (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(0);
        }
        finally
        {
            await DropTargetTableAsync(connection, tableName);
        }
    }

    /// <summary>
    /// A purge with nothing to delete still records itself, with zero rows and no dates
    /// </summary>
    [Fact]
    public async Task APurgeWithNothingToDeleteStillRecordsItselfAsync()
    {
        // Purging before a date where no row can exist
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string result = await PurgeAsync(connection, "'1900-01-02'", token);
        int purgeRows = (int)(await SqlBatch.ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [NewValue] = N'{result}' AND [ActionDT] >= CAST(SYSUTCDATETIME() AS DATE);", token))!;

        // Checks
        result.ShouldBe("{\"Before\":\"1900-01-02\",\"Rows\":0}");
        purgeRows.ShouldBeGreaterThanOrEqualTo(1);
    }

    /// <summary>
    /// A purge that fails after its gate is open rolls everything back and leaves the gate closed in the session
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> a temporary AFTER DELETE trigger fails the delete made inside the INSTEAD OF trigger; it is dropped in every case.</para>
    /// </remarks>
    [Fact]
    public async Task AFailedPurgeDeletesNothingAndLeavesTheGateClosedAsync()
    {
        // Writing an old row, and a trigger that makes every real delete of the audit fail
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string marker = Guid.NewGuid().ToString("N");
        string triggerName = $"TR_Test_Audit_FailDelete_{Guid.NewGuid():N}";
        string tableName = await CreateTargetTableAsync(connection, token);
        try
        {
            await WriteOldRowAsync(connection, tableName, "2001-06-01T10:00:00", marker, token);
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE TRIGGER [Logs].[{triggerName}] ON [Logs].[Audit] AFTER DELETE AS THROW {ForcedFailure}, ''Forced failure'', 1;');", token);
            await SqlBatch.ShouldFailWithAsync(connection, "DECLARE @result NVARCHAR(MAX); EXEC [Logs].[SP_Logs_Audit_Purge] @Before = '2001-12-31', @Result = @result OUTPUT;",
                ForcedFailure, token);
            object? gate = await SqlBatch.ScalarAsync(connection, "SELECT SESSION_CONTEXT(N'AllowAuditPurge');", token);

            // Checks
            gate.ShouldBe(DBNull.Value);
            (await CountMarkedRowsAsync(connection, marker, token)).ShouldBe(1);
        }
        finally
        {
            // Dropping the trigger, then purging the rows of 2001 left by the test
            await SqlBatch.ExecuteAsync(connection, $"IF OBJECT_ID(N'[Logs].[{triggerName}]', N'TR') IS NOT NULL DROP TRIGGER [Logs].[{triggerName}];", CancellationToken.None);
            await PurgeAsync(connection, "'2001-12-31'", CancellationToken.None);
            await DropTargetTableAsync(connection, tableName);
        }
    }

    /// <summary>
    /// A gate left open in a session is cleared when its pooled connection is given to the next caller
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the threat model relies on the reset of a pooled connection (sp_reset_connection) to clear the session context; this
    /// test proves it instead of trusting it.</para>
    /// <para><b>Behavior:</b> a pool of its own, of one connection (a unique application name, Max Pool Size = 1), so that the second open gets the
    /// same session, which the test checks by its session id.</para>
    /// </remarks>
    [Fact]
    public async Task AGateLeftOpenIsClearedWhenThePooledConnectionIsReusedAsync()
    {
        // Opening a pool of one connection, opening the gate, giving the connection back
        CancellationToken token = TestContext.Current.CancellationToken;
        string applicationName = $"AuditGateTest_{Guid.NewGuid():N}";
        void Configure(SqlConnectionStringBuilder builder)
        {
            builder.ApplicationName = applicationName;
            builder.Pooling = true;
            builder.MaxPoolSize = 1;
        }

        short firstSession;
        await using (SqlConnection first = await TestDatabase.OpenAsync(Configure, token))
        {
            firstSession = (short)(await SqlBatch.ScalarAsync(first, "SELECT @@SPID;", token))!;
            await SqlBatch.ExecuteAsync(first, "EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = 1;", token);
        }

        // Opening again from the same pool
        await using SqlConnection second = await TestDatabase.OpenAsync(Configure, token);
        short secondSession = (short)(await SqlBatch.ScalarAsync(second, "SELECT @@SPID;", token))!;
        object? gate = await SqlBatch.ScalarAsync(second, "SELECT SESSION_CONTEXT(N'AllowAuditPurge');", token);
        SqlConnection.ClearPool(second);

        // Checks
        secondSession.ShouldBe(firstSession, "The pool of one connection must give the same session back");
        gate.ShouldBe(DBNull.Value);
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    // #N/A

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
