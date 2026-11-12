/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Platform/SettingsProceduresTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the procedures of the settings: the write pattern (table-valued parameter, one JSON result per row, audit) and the history
 */

// Using
using System.Data;
using System.Text.Json;
using BoardGamesPlatform.TestKit;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Platform;

/// <summary>
/// Tests used to check the group of procedures of Platform.Settings, the view that reads it, and its temporal history
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the settings prove the write pattern of the platform (doc/dev/database.md, Write pattern): the rows are checked again by
/// the procedure, each input row comes back once with its result, the valid rows are written with their audit in the same transaction, and the
/// former values stay in the history.</para>
/// <para><b>Behavior:</b> the table-valued parameters are passed from ADO.NET (SqlDbType.Structured), as the Very Back will pass them. Every code
/// a test writes starts with "Test." and ends with a GUID, so that the tests never meet; the rows are deleted at the end of each test, as the
/// owner of the database (their history and their audit rows stay, as they would for any setting). Skipped when no throwaway database is
/// configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
[Collection(AuditSchemaChanges.Name)]
public sealed class SettingsProceduresTests
{
    #region Members

    #region Constants

    /// <summary>
    /// The identifier of the actor "the platform itself" (Functions.SystemActorId)
    /// </summary>
    private const string SystemActorId = "5778C6C3-5A1A-4C0A-8878-F36CEECB523A";

    /// <summary>
    /// Error number raised by the temporary trigger that makes the audit fail after the write
    /// </summary>
    private const int ForcedFailure = 50998;

    #endregion

    #region Variables

    /// <summary>
    /// Expected properties of a result of the insert, in this order
    /// </summary>
    private static readonly string[] _insertProperties = new string[] { "Id", "Code", "Kind", "Value", "Comment", "IsSuccess", "Error" };

    /// <summary>
    /// Expected properties of a result of the update, in this order
    /// </summary>
    private static readonly string[] _updateProperties = new string[] { "Id", "Code", "Kind", "Value", "IsSuccess", "Error" };

    /// <summary>
    /// Expected properties of an audit value of a setting, in this order
    /// </summary>
    private static readonly string[] _auditProperties = new string[] { "Id", "Code", "Kind", "Value", "Comment" };

    /// <summary>
    /// Expected columns of the view of the settings, in this order
    /// </summary>
    private static readonly string[] _viewColumns = new string[] { "Code", "Kind", "Value", "ValidFrom" };

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
    /// Method used to make a code of a setting that no other test uses
    /// </summary>
    /// <returns>
    /// A valid code, "Test.S" followed by a GUID
    /// </returns>
    private static string NewCode()
    {
        // Function's return
        return $"Test.S{Guid.NewGuid():N}";
    }

    /// <summary>
    /// Method used to call a write procedure of the settings with a table-valued parameter
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="procedure">[Cannot be null] Schema-qualified name of the procedure</param>
    /// <param name="typeName">[Cannot be null] Schema-qualified name of the table-valued parameter</param>
    /// <param name="rows">[Cannot be null] The rows, already shaped as the type</param>
    /// <param name="actor">Actor of the call [Can be null]: null is sent as SQL NULL</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The JSON result of the procedure
    /// </returns>
    private static async Task<string> CallAsync(SqlConnection connection, string procedure, string typeName, DataTable rows, Guid? actor, CancellationToken cancellationToken)
    {
        // Setting the call, schema-qualified
        await using SqlCommand command = new SqlCommand(procedure, connection) { CommandType = CommandType.StoredProcedure };
        SqlParameter rowsParameter = command.Parameters.Add("@Rows", SqlDbType.Structured);
        rowsParameter.TypeName = typeName;
        rowsParameter.Value = rows;
        command.Parameters.Add("@IdActor", SqlDbType.UniqueIdentifier).Value = actor.HasValue ? actor.Value : DBNull.Value;
        SqlParameter result = command.Parameters.Add("@Result", SqlDbType.NVarChar, -1);
        result.Direction = ParameterDirection.Output;

        // Calling the procedure
        await command.ExecuteNonQueryAsync(cancellationToken);

        // Function's return
        return result.Value.ShouldBeOfType<string>();
    }

    /// <summary>
    /// Method used to build the rows of an insert
    /// </summary>
    /// <param name="rows">[Cannot be null] Code, kind, value and comment [comment can be null] of each row</param>
    /// <returns>
    /// The rows, shaped as Types.TVP_Platform_Settings_Insert
    /// </returns>
    private static DataTable InsertRows(params (string Code, string Kind, string Value, string? Comment)[] rows)
    {
        // Setting the shape of the type
        DataTable table = new DataTable();
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Kind", typeof(string));
        table.Columns.Add("Value", typeof(string));
        table.Columns.Add("Comment", typeof(string));
        foreach ((string code, string kind, string value, string? comment) in rows)
            table.Rows.Add(code, kind, value, (object?)comment ?? DBNull.Value);

        // Function's return
        return table;
    }

    /// <summary>
    /// Method used to build the rows of an update
    /// </summary>
    /// <param name="rows">[Cannot be null] Code and new value of each row</param>
    /// <returns>
    /// The rows, shaped as Types.TVP_Platform_Settings_Update
    /// </returns>
    private static DataTable UpdateRows(params (string Code, string Value)[] rows)
    {
        // Setting the shape of the type
        DataTable table = new DataTable();
        table.Columns.Add("Code", typeof(string));
        table.Columns.Add("Value", typeof(string));
        foreach ((string code, string value) in rows)
            table.Rows.Add(code, value);

        // Function's return
        return table;
    }

    /// <summary>
    /// Method used to insert settings as the platform
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="rows">[Cannot be null] The rows to insert</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The JSON result of the insert
    /// </returns>
    private static Task<string> InsertAsync(SqlConnection connection, DataTable rows, CancellationToken cancellationToken)
    {
        // Function's return
        return CallAsync(connection, "[StorProc].[SP_Platform_Settings_Insert]", "[Types].[TVP_Platform_Settings_Insert]", rows, Guid.Parse(SystemActorId), cancellationToken);
    }

    /// <summary>
    /// Method used to update settings as the platform
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="rows">[Cannot be null] The rows to update</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The JSON result of the update
    /// </returns>
    private static Task<string> UpdateAsync(SqlConnection connection, DataTable rows, CancellationToken cancellationToken)
    {
        // Function's return
        return CallAsync(connection, "[StorProc].[SP_Platform_Settings_Update]", "[Types].[TVP_Platform_Settings_Update]", rows, Guid.Parse(SystemActorId), cancellationToken);
    }

    /// <summary>
    /// Method used to count the rows of a setting in the table and in the audit
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, as the owner of the database</param>
    /// <param name="code">[Cannot be null] Code of the setting</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The number of rows of the setting, and the number of audit rows that name it
    /// </returns>
    private static async Task<(int Settings, int Audit)> CountAsync(SqlConnection connection, string code, CancellationToken cancellationToken)
    {
        // Counting
        int settings = (int)(await SqlBatch.ScalarAsync(connection, $"SELECT COUNT(*) FROM [Platform].[Settings] WHERE [Code] = N'{code}';", cancellationToken))!;
        int audit = (int)(await SqlBatch.ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [SchemaName] = N'Platform' AND [TableName] = N'Settings' AND [NewValue] LIKE N'%\"{code}\"%';", cancellationToken))!;

        // Function's return
        return (settings, audit);
    }

    /// <summary>
    /// Method used to delete the settings written by a test, as the owner of the database
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="codes">[Cannot be null] Codes of the settings</param>
    /// <returns>
    /// A task that completes when the rows are deleted
    /// </returns>
    private static async Task DeleteAsync(SqlConnection connection, params string[] codes)
    {
        // Deleting the rows (their history and their audit rows stay)
        string list = string.Join(", ", codes.Select(code => $"N'{code}'"));
        await SqlBatch.ExecuteAsync(connection, $"DELETE FROM [Platform].[Settings] WHERE [Code] IN ({list});", CancellationToken.None);
    }

    /// <summary>
    /// Method used to read the result of one code in a JSON result
    /// </summary>
    /// <param name="document">[Cannot be null] The parsed result</param>
    /// <param name="code">[Cannot be null] Code of the row</param>
    /// <returns>
    /// The result object of that row; the test fails when there is not exactly one
    /// </returns>
    private static JsonElement ResultOf(JsonDocument document, string code)
    {
        // Function's return
        return document.RootElement.EnumerateArray().Where(row => row.GetProperty("Code").GetString() == code).ShouldHaveSingleItem();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The insert writes every valid row, returns one result per input row in the order of the codes, and writes one audit row per setting with
    /// the values of the row only
    /// </summary>
    [Fact]
    public async Task TheInsertWritesTheRowsWithTheirAuditAndOneResultPerRowAsync()
    {
        // Inserting two settings
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string first = NewCode();
        string second = NewCode();
        try
        {
            string result = await InsertAsync(connection, InsertRows((second, "Integer", "-42", null), (first, "Text", "Hello", "A comment")), token);

            // Reading the results, the rows and the audit
            using JsonDocument document = JsonDocument.Parse(result);
            List<JsonElement> rows = document.RootElement.EnumerateArray().ToList();
            string? audit = (string?)await SqlBatch.ScalarAsync(connection,
                $"SELECT [NewValue] FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NULL AND [NewValue] LIKE N'%\"{second}\"%';", token);
            using JsonDocument auditDocument = JsonDocument.Parse(audit.ShouldNotBeNull());
            string? commentedAudit = (string?)await SqlBatch.ScalarAsync(connection,
                $"SELECT [NewValue] FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NULL AND [NewValue] LIKE N'%\"{first}\"%';", token);
            using JsonDocument commentedDocument = JsonDocument.Parse(commentedAudit.ShouldNotBeNull());

            // Checks: shape and order of the results, the values written, the audit
            rows.Count.ShouldBe(2);
            rows.Select(row => row.GetProperty("Code").GetString()).ShouldBe(new string[] { first, second }.Order(StringComparer.OrdinalIgnoreCase));
            rows.ShouldAllBe(row => row.EnumerateObject().Select(property => property.Name).SequenceEqual(_insertProperties));
            rows.ShouldAllBe(row => row.GetProperty("IsSuccess").GetBoolean() && row.GetProperty("Error").ValueKind == JsonValueKind.Null);
            ResultOf(document, second).GetProperty("Comment").ValueKind.ShouldBe(JsonValueKind.Null);
            (await CountAsync(connection, first, token)).ShouldBe((1, 1));
            (await CountAsync(connection, second, token)).ShouldBe((1, 1));
            auditDocument.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(_auditProperties);
            auditDocument.RootElement.GetProperty("Id").GetGuid().ShouldBe(ResultOf(document, second).GetProperty("Id").GetGuid());
            auditDocument.RootElement.GetProperty("Value").GetString().ShouldBe("-42");
            commentedDocument.RootElement.GetProperty("Comment").GetString().ShouldBe("A comment");
        }
        finally
        {
            await DeleteAsync(connection, first, second);
        }
    }

    /// <summary>
    /// The insert rejects a row that breaks a rule, with its reason, and writes neither the row nor an audit row
    /// </summary>
    /// <param name="code">Code of the row; "{new}" is replaced by a fresh valid code</param>
    /// <param name="kind">Kind of the row</param>
    /// <param name="value">Value of the row</param>
    /// <param name="expectedError">Reason expected in the result</param>
    [Theory]
    [InlineData("NoDot", "Text", "x", "Code is not valid")]
    [InlineData("1Starts.WithADigit", "Text", "x", "Code is not valid")]
    [InlineData("Has space.Inside", "Text", "x", "Code is not valid")]
    [InlineData("Accént.Inside", "Text", "x", "Code is not valid")]
    [InlineData("Ends.With.", "Text", "x", "Code is not valid")]
    [InlineData("Has_underscore.x", "Text", "x", "Code is not valid")]
    [InlineData("a-b.c", "Text", "x", "Code is not valid")]
    [InlineData("{new}", "Date", "x", "Kind is not Boolean, Integer or Text")]
    [InlineData("{new}", "Boolean", "True", "Value does not fit its kind")]
    [InlineData("{new}", "Boolean", "1", "Value does not fit its kind")]
    [InlineData("{new}", "Integer", "012", "Value does not fit its kind")]
    [InlineData("{new}", "Integer", " 12", "Value does not fit its kind")]
    [InlineData("{new}", "Integer", "9223372036854775808", "Value does not fit its kind")]
    [InlineData("{new}", "Text", "   ", "Value does not fit its kind")]
    [InlineData("Registration.IsOpen", "Boolean", "true", "Code already exists")]
    [InlineData("{new}", "Boolean", "true ", "Value does not fit its kind")]
    [InlineData("{new}", "Integer", "5 ", "Value does not fit its kind")]
    [InlineData("{new}", "boolean", "true", "Kind is not Boolean, Integer or Text")]
    [InlineData("{new}", "Boolean ", "true", "Kind is not Boolean, Integer or Text")]
    [InlineData("{new} ", "Text", "x", "Code is not valid")]
    public async Task TheInsertRejectsARowThatBreaksARuleAsync(string code, string kind, string value, string expectedError)
    {
        // Inserting the row ("{new}" is a fresh valid code; "{new} " the same followed by a space)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string actualCode = code.StartsWith("{new}", StringComparison.Ordinal) ? NewCode() + code["{new}".Length..] : code;
        (int Settings, int Audit) before = await CountAsync(connection, actualCode, token);
        string result = await InsertAsync(connection, InsertRows((actualCode, kind, value, null)), token);

        // Reading the result
        using JsonDocument document = JsonDocument.Parse(result);
        JsonElement row = document.RootElement.EnumerateArray().ShouldHaveSingleItem();

        // Checks
        row.GetProperty("IsSuccess").GetBoolean().ShouldBeFalse();
        row.GetProperty("Id").ValueKind.ShouldBe(JsonValueKind.Null);
        row.GetProperty("Error").GetString().ShouldBe(expectedError);
        row.GetProperty("Value").GetString().ShouldBe(value);
        (await CountAsync(connection, actualCode, token)).ShouldBe(before);
    }

    /// <summary>
    /// A row that breaks two rules comes out once, with the reason of its first failing rule; the valid rows of the same call are written
    /// </summary>
    [Fact]
    public async Task ARowThatBreaksTwoRulesComesOutOnceAndTheValidRowsAreWrittenAsync()
    {
        // Inserting a valid row, and a row with an invalid code and an unknown kind
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string valid = NewCode();
        string invalid = $"Invalid code {Guid.NewGuid():N}";
        try
        {
            string result = await InsertAsync(connection, InsertRows((valid, "Boolean", "true", null), (invalid, "Date", "x", null)), token);
            using JsonDocument document = JsonDocument.Parse(result);

            // Checks
            document.RootElement.GetArrayLength().ShouldBe(2);
            ResultOf(document, invalid).GetProperty("Error").GetString().ShouldBe("Code is not valid");
            ResultOf(document, valid).GetProperty("IsSuccess").GetBoolean().ShouldBeTrue();
            (await CountAsync(connection, valid, token)).ShouldBe((1, 1));
        }
        finally
        {
            await DeleteAsync(connection, valid);
        }
    }

    /// <summary>
    /// The insert and the update refuse the whole call without any row, with more than 1 000 rows, or with an unknown actor, and write nothing
    /// </summary>
    /// <param name="rowCount">Number of rows of the call</param>
    /// <param name="actor">Actor of the call [Can be null]</param>
    [Theory]
    [InlineData(0, SystemActorId)]
    [InlineData(1001, SystemActorId)]
    [InlineData(1, null)]
    [InlineData(1, "00000000-0000-0000-0000-000000000000")]
    [InlineData(1, "B2A1C9E4-0000-4000-8000-000000000001")]
    public async Task ACallWithoutRowsWithTooManyRowsOrWithAnUnknownActorIsRefusedAsync(int rowCount, string? actor)
    {
        // Building the rows
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string[] codes = Enumerable.Range(0, rowCount).Select(_ => NewCode()).ToArray();
        DataTable insertRows = InsertRows(codes.Select(code => (code, "Text", "x", (string?)null)).ToArray());
        DataTable updateRows = UpdateRows(codes.Select(code => (code, "y")).ToArray());
        Guid? actorId = actor is null ? null : Guid.Parse(actor);

        // Checks: both calls are refused with 51000, and nothing is written
        SqlException insert = await Should.ThrowAsync<SqlException>(() => CallAsync(connection, "[StorProc].[SP_Platform_Settings_Insert]",
            "[Types].[TVP_Platform_Settings_Insert]", insertRows, actorId, token));
        SqlException update = await Should.ThrowAsync<SqlException>(() => CallAsync(connection, "[StorProc].[SP_Platform_Settings_Update]",
            "[Types].[TVP_Platform_Settings_Update]", updateRows, actorId, token));
        insert.Number.ShouldBe(SqlBatch.CheckFailed);
        update.Number.ShouldBe(SqlBatch.CheckFailed);
        int written = (int)(await SqlBatch.ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [Platform].[Settings] WHERE [Code] IN (N'{string.Join("', N'", codes.DefaultIfEmpty(string.Empty))}');", token))!;
        written.ShouldBe(0);
    }

    /// <summary>
    /// A failure after the write, in the audit, rolls back the setting and its audit together: there is no write without its audit
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> a temporary AFTER INSERT trigger fails the insert of the audit row of this test only, so that the tests that run at the
    /// same time are not touched; it is dropped in every case.</para>
    /// </remarks>
    [Fact]
    public async Task AFailureOfTheAuditRollsTheWriteBackAsync()
    {
        // Making the audit of this setting fail, then inserting it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string code = NewCode();
        string triggerName = $"TR_Test_Audit_FailInsert_{Guid.NewGuid():N}";
        try
        {
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE TRIGGER [Logs].[{triggerName}] ON [Logs].[Audit] AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE [NewValue] LIKE N''%{code}%'') THROW {ForcedFailure}, ''Forced failure'', 1;');", token);
            SqlException exception = await Should.ThrowAsync<SqlException>(() => InsertAsync(connection, InsertRows((code, "Text", "x", null)), token));

            // Checks
            exception.Number.ShouldBe(ForcedFailure);
            (await CountAsync(connection, code, token)).ShouldBe((0, 0));
        }
        finally
        {
            // Dropping the trigger, then the setting even when the drop failed
            try
            {
                await SqlBatch.ExecuteAsync(connection, $"IF OBJECT_ID(N'[Logs].[{triggerName}]', N'TR') IS NOT NULL DROP TRIGGER [Logs].[{triggerName}];", CancellationToken.None);
            }
            finally
            {
                await DeleteAsync(connection, code);
            }
        }
    }

    /// <summary>
    /// A failure of the audit of an update rolls back the new value: the setting keeps its value, gets no history row and no audit row of the
    /// update
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the same temporary AFTER INSERT trigger as for the insert, armed after the insert of the setting, so that it fails the
    /// audit of the update only; it is dropped in every case.</para>
    /// </remarks>
    [Fact]
    public async Task AFailureOfTheAuditRollsTheUpdateBackAsync()
    {
        // Inserting a setting, making its next audit fail, then updating it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string code = NewCode();
        string triggerName = $"TR_Test_Audit_FailUpdate_{Guid.NewGuid():N}";
        try
        {
            await InsertAsync(connection, InsertRows((code, "Integer", "1", null)), token);
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE TRIGGER [Logs].[{triggerName}] ON [Logs].[Audit] AFTER INSERT AS IF EXISTS (SELECT 1 FROM inserted WHERE [OldValue] IS NOT NULL AND [NewValue] LIKE N''%{code}%'') THROW {ForcedFailure}, ''Forced failure'', 1;');", token);
            SqlException exception = await Should.ThrowAsync<SqlException>(() => UpdateAsync(connection, UpdateRows((code, "2")), token));
            string value = (string)(await SqlBatch.ScalarAsync(connection, $"SELECT [Value] FROM [Platform].[Settings] WHERE [Code] = N'{code}';", token))!;
            int historyRows = (int)(await SqlBatch.ScalarAsync(connection, $"SELECT COUNT(*) FROM [History].[Platform_Settings] WHERE [Code] = N'{code}';", token))!;
            int updateAudits = (int)(await SqlBatch.ScalarAsync(connection,
                $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NOT NULL AND [NewValue] LIKE N'%\"{code}\"%';", token))!;

            // Checks
            exception.Number.ShouldBe(ForcedFailure);
            value.ShouldBe("1");
            historyRows.ShouldBe(0);
            updateAudits.ShouldBe(0);
        }
        finally
        {
            // Dropping the trigger, then the setting even when the drop failed
            try
            {
                await SqlBatch.ExecuteAsync(connection, $"IF OBJECT_ID(N'[Logs].[{triggerName}]', N'TR') IS NOT NULL DROP TRIGGER [Logs].[{triggerName}];", CancellationToken.None);
            }
            finally
            {
                await DeleteAsync(connection, code);
            }
        }
    }

    /// <summary>
    /// The insert and the update accept a call of exactly 1 000 rows, the limit of the contract
    /// </summary>
    [Fact]
    public async Task ACallOfExactly1000RowsIsAcceptedAsync()
    {
        // Inserting 1 000 settings, then updating all of them
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string[] codes = Enumerable.Range(0, 1000).Select(_ => NewCode()).ToArray();
        try
        {
            string insert = await InsertAsync(connection, InsertRows(codes.Select(code => (code, "Text", "x", (string?)null)).ToArray()), token);
            string update = await UpdateAsync(connection, UpdateRows(codes.Select(code => (code, "y")).ToArray()), token);
            using JsonDocument insertDocument = JsonDocument.Parse(insert);
            using JsonDocument updateDocument = JsonDocument.Parse(update);

            // Checks
            insertDocument.RootElement.GetArrayLength().ShouldBe(1000);
            updateDocument.RootElement.GetArrayLength().ShouldBe(1000);
            insertDocument.RootElement.EnumerateArray().ShouldAllBe(row => row.GetProperty("IsSuccess").GetBoolean());
            updateDocument.RootElement.EnumerateArray().ShouldAllBe(row => row.GetProperty("IsSuccess").GetBoolean());
        }
        finally
        {
            await DeleteAsync(connection, codes);
        }
    }

    /// <summary>
    /// A change of the case of a text only is a change of its value: the update accepts it
    /// </summary>
    [Fact]
    public async Task AChangeOfCaseOnlyIsAChangeOfTheValueAsync()
    {
        // Inserting a text, then updating it with the same letters in upper case
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string code = NewCode();
        try
        {
            await InsertAsync(connection, InsertRows((code, "Text", "Same", null)), token);
            string result = await UpdateAsync(connection, UpdateRows((code, "SAME")), token);
            using JsonDocument document = JsonDocument.Parse(result);
            string value = (string)(await SqlBatch.ScalarAsync(connection, $"SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'{code}';", token))!;

            // Checks
            ResultOf(document, code).GetProperty("IsSuccess").GetBoolean().ShouldBeTrue();
            value.ShouldBe("SAME");
        }
        finally
        {
            await DeleteAsync(connection, code);
        }
    }

    /// <summary>
    /// The table-valued parameters refuse by themselves a blank code or kind, and twice the same code in one call, compared without case
    /// </summary>
    /// <param name="sql">The batch that fills the table-valued parameter</param>
    /// <param name="expectedError">Error number expected: 547 for a CHECK constraint, 2627 for the unique constraint</param>
    [Theory]
    [InlineData("DECLARE @rows [Types].[TVP_Platform_Settings_Insert]; INSERT INTO @rows ([Code], [Kind], [Value]) VALUES (N' ', N'Text', N'x');", 547)]
    [InlineData("DECLARE @rows [Types].[TVP_Platform_Settings_Insert]; INSERT INTO @rows ([Code], [Kind], [Value]) VALUES (N'Test.Blank', N' ', N'x');", 547)]
    [InlineData("DECLARE @rows [Types].[TVP_Platform_Settings_Insert]; INSERT INTO @rows ([Code], [Kind], [Value]) VALUES (N'Test.Twice', N'Text', N'x'), (N'TEST.twice', N'Text', N'y');", 2627)]
    [InlineData("DECLARE @rows [Types].[TVP_Platform_Settings_Update]; INSERT INTO @rows ([Code], [Value]) VALUES (N' ', N'x');", 547)]
    [InlineData("DECLARE @rows [Types].[TVP_Platform_Settings_Update]; INSERT INTO @rows ([Code], [Value]) VALUES (N'Test.Twice', N'x'), (N'TEST.twice', N'y');", 2627)]
    public async Task TheTableValuedParametersRefuseABlankOrDuplicateRowAsync(string sql, int expectedError)
    {
        // Filling the parameter
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, sql, expectedError, token);
    }

    /// <summary>
    /// The view of the settings exposes exactly the code, the kind, the value and the start of its validity, in this order
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the identifier and the comment are not part of the contract of the application (doc/dev/database.md); a column added to
    /// the view by mistake would be read by the application.</para>
    /// </remarks>
    [Fact]
    public async Task TheViewExposesExactlyItsContractAsync()
    {
        // Reading the columns of the view
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        List<string> columns = new List<string>();
        await using (SqlCommand command = new SqlCommand("SELECT [name] FROM [sys].[columns] WHERE [object_id] = OBJECT_ID(N'[Views].[V_Platform_Settings]') ORDER BY [column_id];", connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                columns.Add(reader.GetString(0));
        }

        // Check
        columns.ShouldBe(_viewColumns);
    }

    /// <summary>
    /// The post-deployment script, run again, keeps the value an administrator gave to the registration switch and writes no audit row: a
    /// redeployment never reopens the registration
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> SSDT runs this script after every publish; the test runs its text against the database, after opening the switch
    /// through the update procedure, then closes it again in every case.</para>
    /// </remarks>
    [Fact]
    public async Task ARedeploymentKeepsTheValueOfTheRegistrationSwitchAsync()
    {
        // Opening the switch, then running the post-deployment script again
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string script = File.ReadAllText(Path.Combine(RepositoryPaths.GetRoot(), "src", "BoardGamesPlatform.Database", "Scripts", "Script.PostDeployment.sql"));
        const string InsertAudits = "SELECT COUNT(*) FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NULL AND [NewValue] LIKE N'%\"Registration.IsOpen\"%';";
        try
        {
            (await UpdateAsync(connection, UpdateRows(("Registration.IsOpen", "true")), token)).ShouldContain("\"IsSuccess\":true");
            int auditsBefore = (int)(await SqlBatch.ScalarAsync(connection, InsertAudits, token))!;
            await SqlBatch.ExecuteAsync(connection, script, token);
            string value = (string)(await SqlBatch.ScalarAsync(connection, "SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'Registration.IsOpen';", token))!;
            int auditsAfter = (int)(await SqlBatch.ScalarAsync(connection, InsertAudits, token))!;

            // Checks
            value.ShouldBe("true");
            auditsAfter.ShouldBe(auditsBefore);
        }
        finally
        {
            await UpdateAsync(connection, UpdateRows(("Registration.IsOpen", "false")), CancellationToken.None);
        }
    }

    /// <summary>
    /// The update changes the value, keeps the former value in the history with its period, and audits the row before and after
    /// </summary>
    [Fact]
    public async Task TheUpdateKeepsTheFormerValueInTheHistoryAndAuditsBothAsync()
    {
        // Inserting a setting, then changing its value
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string code = NewCode();
        try
        {
            await InsertAsync(connection, InsertRows((code, "Integer", "1", null)), token);
            DateTime between = (DateTime)(await SqlBatch.ScalarAsync(connection, "SELECT SYSUTCDATETIME();", token))!;
            string result = await UpdateAsync(connection, UpdateRows((code, "2")), token);

            // Reading the result, the current value, the value at the time between both writes, the history and the audit
            using JsonDocument document = JsonDocument.Parse(result);
            JsonElement row = document.RootElement.EnumerateArray().ShouldHaveSingleItem();
            string current = (string)(await SqlBatch.ScalarAsync(connection, $"SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'{code}';", token))!;
            string formerValue = (string)(await SqlBatch.ScalarAsync(connection,
                $"SELECT [Value] FROM [Platform].[Settings] FOR SYSTEM_TIME AS OF '{between:yyyy-MM-ddTHH:mm:ss.fffffff}' WHERE [Code] = N'{code}';", token))!;
            int historyRows = (int)(await SqlBatch.ScalarAsync(connection,
                $"SELECT COUNT(*) FROM [History].[Platform_Settings] WHERE [Code] = N'{code}' AND [Value] = N'1' AND [ValidTo] <= SYSUTCDATETIME();", token))!;
            string? oldValue = (string?)await SqlBatch.ScalarAsync(connection,
                $"SELECT [OldValue] FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NOT NULL AND [NewValue] LIKE N'%\"{code}\"%';", token);
            using JsonDocument oldDocument = JsonDocument.Parse(oldValue.ShouldNotBeNull());
            string? newValue = (string?)await SqlBatch.ScalarAsync(connection,
                $"SELECT [NewValue] FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NOT NULL AND [NewValue] LIKE N'%\"{code}\"%';", token);
            using JsonDocument newDocument = JsonDocument.Parse(newValue.ShouldNotBeNull());

            // Checks
            row.EnumerateObject().Select(property => property.Name).ShouldBe(_updateProperties);
            row.GetProperty("IsSuccess").GetBoolean().ShouldBeTrue();
            row.GetProperty("Kind").GetString().ShouldBe("Integer");
            current.ShouldBe("2");
            formerValue.ShouldBe("1");
            historyRows.ShouldBe(1);
            oldDocument.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(_auditProperties);
            oldDocument.RootElement.GetProperty("Value").GetString().ShouldBe("1");
            newDocument.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(_auditProperties);
            newDocument.RootElement.GetProperty("Value").GetString().ShouldBe("2");
        }
        finally
        {
            await DeleteAsync(connection, code);
        }
    }

    /// <summary>
    /// The update rejects an unknown code, a value that does not fit the kind of the setting, and an unchanged value, and writes nothing for them
    /// </summary>
    [Fact]
    public async Task TheUpdateRejectsUnknownCodesWrongValuesAndUnchangedValuesAsync()
    {
        // Inserting a Boolean and a Text setting, then updating them with wrong rows, and a code that does not exist
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string boolean = NewCode();
        string text = NewCode();
        string unknown = NewCode();
        try
        {
            await InsertAsync(connection, InsertRows((boolean, "Boolean", "false", null), (text, "Text", "Same", null)), token);
            string result = await UpdateAsync(connection, UpdateRows((boolean, "yes"), (text, "Same"), (unknown, "x")), token);
            using JsonDocument document = JsonDocument.Parse(result);
            int changedAudits = (int)(await SqlBatch.ScalarAsync(connection,
                $"SELECT COUNT(*) FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NOT NULL AND ([NewValue] LIKE N'%\"{boolean}\"%' OR [NewValue] LIKE N'%\"{text}\"%');", token))!;

            // Checks
            document.RootElement.GetArrayLength().ShouldBe(3);
            ResultOf(document, boolean).GetProperty("Error").GetString().ShouldBe("Value does not fit the kind of the setting");
            ResultOf(document, text).GetProperty("Error").GetString().ShouldBe("Value is unchanged");
            ResultOf(document, unknown).GetProperty("Error").GetString().ShouldBe("Code does not exist");
            ResultOf(document, unknown).GetProperty("Kind").ValueKind.ShouldBe(JsonValueKind.Null);
            document.RootElement.EnumerateArray().ShouldAllBe(row => !row.GetProperty("IsSuccess").GetBoolean());
            changedAudits.ShouldBe(0);
        }
        finally
        {
            await DeleteAsync(connection, boolean, text);
        }
    }

    /// <summary>
    /// The deployment writes the registration switch, closed, with its audit row
    /// </summary>
    [Fact]
    public async Task TheDeploymentWritesTheRegistrationSwitchClosedAsync()
    {
        // Reading the setting through the view, and its audit row
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string? value = (string?)await SqlBatch.ScalarAsync(connection,
            "SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'Registration.IsOpen' AND [Kind] = N'Boolean';", token);
        int audits = (int)(await SqlBatch.ScalarAsync(connection,
            "SELECT COUNT(*) FROM [Logs].[Audit] WHERE [TableName] = N'Settings' AND [OldValue] IS NULL AND [NewValue] LIKE N'%\"Registration.IsOpen\"%';", token))!;

        // Checks: closed (the test of the redeployment of this class opens it for a moment and closes it again, never while this test runs); its
        // audit row is there once, unless a purge of the audit removed it since the deployment: the audit of the insert itself is proven by the
        // tests of the insert, since the post-deployment script writes through the same procedure
        value.ShouldBe("false");
        audits.ShouldBeInRange(0, 1);
    }

    /// <summary>
    /// A member of the application role writes the settings through the procedures and reads them through the view, but not the table
    /// </summary>
    [Fact]
    public async Task AMemberOfTheApplicationRoleWritesAndReadsTheSettingsOnlyThroughTheContractAsync()
    {
        // Creating a user, member of the role, then acting as it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string code = NewCode();
        string userName = $"TestApplicationUser_{Guid.NewGuid():N}";
        await SqlBatch.ExecuteAsync(connection, $"CREATE USER [{userName}] WITHOUT LOGIN; ALTER ROLE [ApplicationRole] ADD MEMBER [{userName}];", token);
        try
        {
            await SqlBatch.ExecuteAsync(connection, $"EXECUTE AS USER = N'{userName}';", token);
            string insert;
            string update;
            string? value;
            try
            {
                insert = await InsertAsync(connection, InsertRows((code, "Boolean", "false", null)), token);
                update = await UpdateAsync(connection, UpdateRows((code, "true")), token);
                value = (string?)await SqlBatch.ScalarAsync(connection, $"SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'{code}';", token);
                await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [Platform].[Settings];", SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [History].[Platform_Settings];", SqlBatch.PermissionDenied, token);
            }
            finally
            {
                await SqlBatch.ExecuteAsync(connection, "REVERT;", CancellationToken.None);
            }

            // Checks
            insert.ShouldContain("\"IsSuccess\":true");
            update.ShouldContain("\"IsSuccess\":true");
            value.ShouldBe("true");
        }
        finally
        {
            await SqlBatch.ExecuteAsync(connection, $"ALTER ROLE [ApplicationRole] DROP MEMBER [{userName}]; DROP USER [{userName}];", CancellationToken.None);
            await DeleteAsync(connection, code);
        }
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
