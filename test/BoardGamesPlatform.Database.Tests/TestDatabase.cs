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
using Guard = BoardGamesPlatform.TestKit.TestDatabase;

// Namespace
namespace BoardGamesPlatform.Database.Tests;

/// <summary>
/// Helper used to open a connection to the throwaway database of the SQL tests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the SQL tests run against a real SQL Server, on a database deployed by tools/database/Deploy-TestDatabase.ps1. Without
/// one (in the CI for example), they must be reported as skipped, never as passed.</para>
/// <para><b>Behavior:</b> the rule itself (the variable, and the refusal of any database that does not start with TEST_ or DEV_) lives in
/// <see cref="Guard"/>, in the TestKit, shared by every test project that uses a real SQL Server. This class only adds the skip, which belongs to
/// an xUnit project.</para>
/// </remarks>
internal static class TestDatabase
{
    #region Members

    #region Constants

    // #N/A

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
        string? connectionString = Guard.ReadConnectionString();
        if (connectionString is null)
            Assert.Skip(Guard.SkipReason);

        // Function's return
        return await Guard.OpenAsync(connectionString, cancellationToken);
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
