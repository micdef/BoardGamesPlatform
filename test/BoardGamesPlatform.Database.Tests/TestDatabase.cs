/*
 *	@file	test/BoardGamesPlatform.Database.Tests/TestDatabase.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Opens the throwaway database of the SQL tests, or skips the test when none is configured
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
/// Helper used to open a connection to the throwaway database of the SQL tests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the SQL tests run against a real SQL Server, on a database deployed by tools/database/Deploy-TestDatabase.ps1. Without
/// one (in the CI for example), they must be reported as skipped, never as passed.</para>
/// <para><b>Behavior:</b> reads the connection string from the environment variable <see cref="ConnectionStringVariable"/>. When it is missing,
/// the calling test is skipped. The database of the open connection must start with TEST_ or DEV_ (upper case): the tests create and drop
/// objects, so a connection to another database is closed and refused instead of being used. The server itself is not checked (same limit as
/// the deployment script): a TEST_ database must never be created on a production server.</para>
/// </remarks>
internal static class TestDatabase
{
    #region Members

    #region Constants

    /// <summary>
    /// Environment variable holding the connection string of the throwaway database, for example
    /// "Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=True;TrustServerCertificate=True"
    /// </summary>
    public const string ConnectionStringVariable = "BOARDGAMESPLATFORM_TEST_DATABASE";

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
    /// Method used to open a connection to the throwaway database, or to skip the calling test when none is configured
    /// </summary>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The open connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="InvalidOperationException">If the database of the connection string does not start with TEST_ or DEV_</exception>
    public static async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        // Skipping the test when no database is configured
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            $"No SQL test database: set {ConnectionStringVariable} after deploying one with tools/database/Deploy-TestDatabase.ps1");

        // Opening the connection
        SqlConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check: a throwaway database only, read from the open connection (not from the text, which a server default could override)
        if (!connection.Database.StartsWith("TEST_", StringComparison.Ordinal) && !connection.Database.StartsWith("DEV_", StringComparison.Ordinal))
        {
            string database = connection.Database;
            await connection.DisposeAsync();
            throw new InvalidOperationException($"The SQL tests only run on a TEST_ or DEV_ database, not on '{database}'");
        }

        // Function's return
        return connection;
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
