/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Logs/AuditTableConstraintsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the table Logs.Audit refuses an invalid row by itself, without its procedure
 */

// Using
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Logs;

/// <summary>
/// Tests used to check the CHECK constraints of Logs.Audit, the last layer of the control in depth
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the table-valued parameter and the procedure refuse an invalid row first, so their tests never reach the constraints of
/// the table; a constraint dropped by mistake would leave them green. These tests write directly in the table, as its owner (dbo is not bound
/// by the DENY to public), so that only the constraint can refuse the row.</para>
/// <para><b>Behavior:</b> each row is written in a transaction rolled back by the same batch: a constraint that no longer refuses it leaves no
/// row in the audit, whose rows about Logs.Audit can never be purged. Skipped when no throwaway database is configured (see
/// <see cref="TestDatabase"/>).</para>
/// </remarks>
public sealed class AuditTableConstraintsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// The identifier of the actor "the platform itself" (Functions.SystemActorId), as a T-SQL value
    /// </summary>
    private const string SystemActor = "'5778C6C3-5A1A-4C0A-8878-F36CEECB523A'";

    /// <summary>
    /// The empty GUID, as a T-SQL value
    /// </summary>
    private const string EmptyGuid = "'00000000-0000-0000-0000-000000000000'";

    /// <summary>
    /// Error number of SQL Server when a CHECK constraint refuses a row
    /// </summary>
    private const int ConstraintViolation = 547;

    #endregion

    #region Variables

    /// <summary>
    /// Every constraint and trigger of the table, sorted: type and name
    /// </summary>
    private static readonly string[] _objects = new string[]
    {
        "C|CK_Logs_Audit_Id",
        "C|CK_Logs_Audit_IdActor",
        "C|CK_Logs_Audit_LoginName",
        "C|CK_Logs_Audit_NewValue",
        "C|CK_Logs_Audit_OldValue",
        "C|CK_Logs_Audit_SchemaName",
        "C|CK_Logs_Audit_TableName",
        "C|CK_Logs_Audit_Values",
        "PK|PK_Logs_Audit_Id",
        "TR|TR_Logs_Audit_IOfDelete",
        "TR|TR_Logs_Audit_IOfUpdate",
    };

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// The table refuses, even to its owner, an empty identifier or actor, a blank login, an unknown schema or table, a view, a row without any
    /// value, and a value that is not JSON
    /// </summary>
    /// <param name="id">T-SQL value of the identifier</param>
    /// <param name="actor">T-SQL value of the actor</param>
    /// <param name="loginName">T-SQL value of the login</param>
    /// <param name="schemaName">T-SQL value of the schema</param>
    /// <param name="tableName">T-SQL value of the table</param>
    /// <param name="oldValue">T-SQL value of the old value</param>
    /// <param name="newValue">T-SQL value of the new value</param>
    [Theory]
    [InlineData(EmptyGuid, SystemActor, "ORIGINAL_LOGIN()", "N'Logs'", "N'Audit'", "NULL", "N'{}'")]
    [InlineData("NEWID()", EmptyGuid, "ORIGINAL_LOGIN()", "N'Logs'", "N'Audit'", "NULL", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "N' '", "N'Logs'", "N'Audit'", "NULL", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'NoSuchSchema'", "N'Audit'", "NULL", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'Logs'", "N'NoSuchTable'", "NULL", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'INFORMATION_SCHEMA'", "N'TABLES'", "NULL", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'Logs'", "N'Audit'", "NULL", "NULL")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'Logs'", "N'Audit'", "N'not json'", "N'{}'")]
    [InlineData("NEWID()", SystemActor, "ORIGINAL_LOGIN()", "N'Logs'", "N'Audit'", "NULL", "N'not json'")]
    public async Task TheTableRefusesAnInvalidRowEvenToTheOwnerAsync(string id, string actor, string loginName, string schemaName, string tableName, string oldValue, string newValue)
    {
        // Writing the row directly, in a transaction that the batch rolls back whatever happens (the error of a constraint ends the statement only)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string sql = $"""
            SET XACT_ABORT OFF;
            BEGIN TRANSACTION;
            INSERT INTO [Logs].[Audit] ([Id], [IdActor], [LoginName], [SchemaName], [TableName], [OldValue], [NewValue])
            VALUES ({id}, {actor}, {loginName}, {schemaName}, {tableName}, {oldValue}, {newValue});
            ROLLBACK TRANSACTION;
            """;

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, sql, ConstraintViolation, token);
    }

    /// <summary>
    /// The table holds exactly its constraints and its two triggers, every CHECK enabled and trusted, every trigger enabled
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a CHECK re-enabled WITH NOCHECK (not trusted) or a disabled trigger leaves the table open without any refusal for the
    /// tests above to catch; the temporary triggers of the other test classes (TR_Test_…) exist only during their test and are not read.</para>
    /// </remarks>
    [Fact]
    public async Task TheTableHoldsExactlyItsConstraintsAndTriggersEnabledAsync()
    {
        // Reading the constraints and the triggers of the table, and those disabled or not trusted
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        List<string> objects = new List<string>();
        await using (SqlCommand command = new SqlCommand(
            "SELECT RTRIM([type]), [name] FROM [sys].[objects] WHERE [parent_object_id] = OBJECT_ID(N'[Logs].[Audit]') AND [type] IN ('C', 'PK', 'UQ', 'F', 'TR') AND [name] NOT LIKE N'TR[_]Test[_]%';", connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                objects.Add($"{reader.GetString(0)}|{reader.GetString(1)}");
        }
        int weak = (int)(await SqlBatch.ScalarAsync(connection, """
            SELECT (SELECT COUNT(*) FROM [sys].[check_constraints] WHERE [parent_object_id] = OBJECT_ID(N'[Logs].[Audit]') AND ([is_disabled] = 1 OR [is_not_trusted] = 1))
                + (SELECT COUNT(*) FROM [sys].[triggers] WHERE [parent_id] = OBJECT_ID(N'[Logs].[Audit]') AND [is_disabled] = 1);
            """, token))!;

        // Checks
        objects.Order(StringComparer.Ordinal).ShouldBe(_objects);
        weak.ShouldBe(0, "Every CHECK constraint of the audit must be enabled and trusted, and every trigger enabled");
    }

    /// <summary>
    /// The same direct write, with valid values, is accepted by the table
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> positive control: if the batch failed for another reason (a typo, a right), the refusals above would prove nothing.
    /// The row is rolled back.</para>
    /// </remarks>
    [Fact]
    public async Task TheTableAcceptsAValidRowOfItsOwnerAsync()
    {
        // Writing a valid row directly, then rolling it back
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        object? written = await SqlBatch.ScalarAsync(connection, $$"""
            SET XACT_ABORT OFF;
            BEGIN TRANSACTION;
            INSERT INTO [Logs].[Audit] ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
            VALUES ({{SystemActor}}, N'Logs', N'Audit', N'{}', N'{}');
            DECLARE @rows INT = @@ROWCOUNT;
            ROLLBACK TRANSACTION;
            SELECT @rows;
            """, token);

        // Check
        written.ShouldBe(1);
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
