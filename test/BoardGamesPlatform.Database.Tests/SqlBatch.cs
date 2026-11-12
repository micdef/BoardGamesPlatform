/*
 *	@file	test/BoardGamesPlatform.Database.Tests/SqlBatch.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Runs the T-SQL batches of the SQL tests, and checks the errors they must raise
 */

// Using
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests;

/// <summary>
/// Helper used to run T-SQL batches in the SQL tests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> every SQL test runs batches, reads one value, or expects one error number; keeping these three steps in one place makes
/// each test read as the rule it proves.</para>
/// <para><b>Behavior:</b> the batches are written by the tests from constants and GUIDs, never from an input; a refusal is proved by its error
/// number only (229 for a permission, 50001 for the audit, 51000 for a check), never by its message.</para>
/// </remarks>
internal static class SqlBatch
{
    #region Members

    #region Constants

    /// <summary>
    /// Error number of SQL Server when a permission is denied on an object
    /// </summary>
    public const int PermissionDenied = 229;

    /// <summary>
    /// Error number of the triggers of the audit, when a change of an audit row is refused
    /// </summary>
    public const int AuditImmutable = 50001;

    /// <summary>
    /// Error number of the procedures, when a call is refused by one of their checks
    /// </summary>
    public const int CheckFailed = 51000;

    #endregion

    #region Variables

    // #N/A

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
    /// Method used to run a T-SQL batch that returns nothing
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Batch to run, built by the test from constants and GUIDs</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the batch has run
    /// </returns>
    public static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the batch
        await using SqlCommand command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run a T-SQL query that returns one value
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Query to run</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The first column of the first row, or null when the query returns no row
    /// </returns>
    public static async Task<object?> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the query
        await using SqlCommand command = new SqlCommand(sql, connection);

        // Function's return
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run a T-SQL batch that must fail with a given error number
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Batch that must fail</param>
    /// <param name="errorNumber">Expected error number of SQL Server</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The exception raised, so that the test can check more of it
    /// </returns>
    public static async Task<SqlException> ShouldFailWithAsync(SqlConnection connection, string sql, int errorNumber, CancellationToken cancellationToken)
    {
        // Running the batch: only the expected error number proves the refusal
        SqlException exception = await Should.ThrowAsync<SqlException>(() => ExecuteAsync(connection, sql, cancellationToken));
        exception.Number.ShouldBe(errorNumber, $"Expected the error {errorNumber} for: {sql}");

        // Function's return
        return exception;
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
