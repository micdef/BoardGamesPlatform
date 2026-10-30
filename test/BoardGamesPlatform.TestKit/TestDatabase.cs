/*
 *	@file	test/BoardGamesPlatform.TestKit/TestDatabase.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	The one guard of the tests that use a real SQL Server: which database they may run on
 */

// Using
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Helper used by every test that runs on the throwaway SQL database
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place for the rule "the SQL tests only run on a TEST_ or DEV_ database", so that no test project can forget it or
/// drift from it. The tests create and drop objects, and the end-to-end test calls a procedure: none of them may touch another database.</para>
/// <para><b>Behavior:</b> <see cref="ReadConnectionString"/> reads the environment variable <see cref="ConnectionStringVariable"/>; a test
/// skips itself with <see cref="SkipReason"/> when it returns null (a library cannot skip: the skip belongs to the xUnit project).
/// <see cref="OpenAsync"/> opens a connection and refuses, with an exception, any database whose real name (read from the open connection, not
/// from the text, which a server default could override) does not start with TEST_ or DEV_, in upper case. The server itself is not checked
/// (same limit as the deployment script): a TEST_ database must never be created on a production server.</para>
/// </remarks>
public static class TestDatabase
{
    #region Members

    #region Constants

    /// <summary>
    /// Environment variable holding the connection string of the throwaway database, for example
    /// "Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=True;TrustServerCertificate=True"
    /// </summary>
    public const string ConnectionStringVariable = "BOARDGAMESPLATFORM_TEST_DATABASE";

    /// <summary>
    /// Reason given when a test is skipped because no throwaway database is configured
    /// </summary>
    public const string SkipReason = "No SQL test database: set " + ConnectionStringVariable + " after deploying one with tools/database/Deploy-TestDatabase.ps1";

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
    /// Method used to read the connection string of the throwaway database
    /// </summary>
    /// <returns>
    /// The connection string of the environment variable, or null when it is missing or blank (the test must then skip itself with <see cref="SkipReason"/>)
    /// </returns>
    public static string? ReadConnectionString()
    {
        // Reading the variable
        string? connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        // Function's return
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }

    /// <summary>
    /// Method used to tell whether a database name is a throwaway one
    /// </summary>
    /// <param name="databaseName">[Can be null] Name of the database</param>
    /// <returns>
    /// True  ==> The name starts with TEST_ or DEV_, in upper case<br/>
    /// False ==> Any other name, including null and empty
    /// </returns>
    public static bool IsThrowawayName(string? databaseName)
    {
        // Function's return
        return databaseName is not null
            && (databaseName.StartsWith("TEST_", StringComparison.Ordinal) || databaseName.StartsWith("DEV_", StringComparison.Ordinal));
    }

    /// <summary>
    /// Method used to open a connection to the throwaway database, refusing any other
    /// </summary>
    /// <param name="connectionString">[Cannot be null] Connection string of the test database, from <see cref="ReadConnectionString"/></param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The open connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection string is null, empty or white spaces</exception>
    /// <exception cref="InvalidOperationException">If the database of the open connection does not start with TEST_ or DEV_</exception>
    public static async Task<SqlConnection> OpenAsync(string connectionString, CancellationToken cancellationToken)
    {
        // Check
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentNullException(nameof(connectionString), $"The value of the parameter: {nameof(connectionString)} cannot be null or empty");

        // Opening the connection
        SqlConnection connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);

            // Check: a throwaway database only, read from the open connection
            if (!IsThrowawayName(connection.Database))
                throw new InvalidOperationException($"The SQL tests only run on a TEST_ or DEV_ database, not on '{connection.Database}'");
        }
        catch
        {
            // Never leave a connection to a refused database open
            await connection.DisposeAsync();
            throw;
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
