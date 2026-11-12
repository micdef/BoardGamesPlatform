/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Platform/SettingsTableConstraintsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the table Platform.Settings refuses an invalid row by itself, without its procedures
 */

// Using
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Platform;

/// <summary>
/// Tests used to check the constraints of Platform.Settings, the last layer of the control in depth
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the procedures reject an invalid row with its reason before it reaches the table, so their tests never reach the
/// constraints; a constraint dropped by mistake would leave them green. These tests write directly in the table, as its owner, so that only the
/// constraint can refuse the row.</para>
/// <para><b>Behavior:</b> each row is written in a transaction rolled back by the same batch, so that a constraint that no longer refuses it
/// leaves no setting and no history behind. Skipped when no throwaway database is configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
[Collection(AuditSchemaChanges.Name)]
public sealed class SettingsTableConstraintsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Error number of SQL Server when a CHECK constraint refuses a row
    /// </summary>
    private const int ConstraintViolation = 547;

    /// <summary>
    /// Error number of SQL Server when a unique constraint refuses a row
    /// </summary>
    private const int UniqueViolation = 2627;

    #endregion

    #region Variables

    /// <summary>
    /// Every constraint of the table, sorted: type and name
    /// </summary>
    private static readonly string[] _constraints = new string[]
    {
        "C|CK_Platform_Settings_Code",
        "C|CK_Platform_Settings_Id",
        "C|CK_Platform_Settings_Kind",
        "C|CK_Platform_Settings_Value",
        "PK|PK_Platform_Settings_Id",
        "UQ|UK_Platform_Settings_Code",
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

    /// <summary>
    /// Method used to build a batch that writes one row directly in the table, then rolls it back
    /// </summary>
    /// <param name="id">T-SQL value of the identifier</param>
    /// <param name="code">T-SQL value of the code</param>
    /// <param name="kind">T-SQL value of the kind</param>
    /// <param name="value">T-SQL value of the value</param>
    /// <returns>
    /// The batch; the error of a constraint ends the statement only, so the rollback always runs
    /// </returns>
    private static string DirectInsert(string id, string code, string kind, string value)
    {
        // Function's return
        return $"""
            SET XACT_ABORT OFF;
            BEGIN TRANSACTION;
            INSERT INTO [Platform].[Settings] ([Id], [Code], [Kind], [Value]) VALUES ({id}, {code}, {kind}, {value});
            ROLLBACK TRANSACTION;
            """;
    }

    #endregion

    #region Publics

    /// <summary>
    /// The table refuses, even to its owner, an empty identifier, an invalid code, an unknown kind and a value that does not fit its kind
    /// </summary>
    /// <param name="id">T-SQL value of the identifier</param>
    /// <param name="code">T-SQL value of the code</param>
    /// <param name="kind">T-SQL value of the kind</param>
    /// <param name="value">T-SQL value of the value</param>
    [Theory]
    [InlineData("'00000000-0000-0000-0000-000000000000'", "N'Test.ZeroId'", "N'Text'", "N'x'")]
    [InlineData("NEWID()", "N'NoDot'", "N'Text'", "N'x'")]
    [InlineData("NEWID()", "N'Test.Date'", "N'Date'", "N'x'")]
    [InlineData("NEWID()", "N'Test.Maybe'", "N'Boolean'", "N'maybe'")]
    public async Task TheTableRefusesAnInvalidRowEvenToTheOwnerAsync(string id, string code, string kind, string value)
    {
        // Writing the row directly
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, DirectInsert(id, code, kind, value), ConstraintViolation, token);
    }

    /// <summary>
    /// The table refuses a second row with a code that already exists
    /// </summary>
    [Fact]
    public async Task TheTableRefusesADuplicateCodeAsync()
    {
        // Writing a second registration switch directly (the deployment writes the first one)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);

        // Check
        await SqlBatch.ShouldFailWithAsync(connection, DirectInsert("NEWID()", "N'Registration.IsOpen'", "N'Boolean'", "N'true'"), UniqueViolation, token);
    }

    /// <summary>
    /// The table holds exactly its constraints, every CHECK enabled and trusted
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a constraint covered by another one cannot be reached by a row (an unknown kind is refused by the constraint of the
    /// value too); this test catches it dropped, disabled, or re-enabled without checking the existing rows (WITH NOCHECK: not trusted).</para>
    /// </remarks>
    [Fact]
    public async Task TheTableHoldsExactlyItsConstraintsEnabledAndTrustedAsync()
    {
        // Reading the constraints of the table, and the CHECK constraints disabled or not trusted
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        List<string> constraints = new List<string>();
        await using (SqlCommand command = new SqlCommand(
            "SELECT RTRIM([type]), [name] FROM [sys].[objects] WHERE [parent_object_id] = OBJECT_ID(N'[Platform].[Settings]') AND [type] IN ('C', 'PK', 'UQ', 'F');", connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                constraints.Add($"{reader.GetString(0)}|{reader.GetString(1)}");
        }
        int weak = (int)(await SqlBatch.ScalarAsync(connection,
            "SELECT COUNT(*) FROM [sys].[check_constraints] WHERE [parent_object_id] = OBJECT_ID(N'[Platform].[Settings]') AND ([is_disabled] = 1 OR [is_not_trusted] = 1);", token))!;

        // Checks
        constraints.Order(StringComparer.Ordinal).ShouldBe(_constraints);
        weak.ShouldBe(0, "Every CHECK constraint of the table must be enabled and trusted");
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
        object? written = await SqlBatch.ScalarAsync(connection, $"""
            SET XACT_ABORT OFF;
            BEGIN TRANSACTION;
            INSERT INTO [Platform].[Settings] ([Code], [Kind], [Value]) VALUES (N'Test.S{Guid.NewGuid():N}', N'Boolean', N'true');
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
