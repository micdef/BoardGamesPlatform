/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Ping/SqlPingTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Proves the Ping through the five tiers down to the real SQL Server, when a test database is configured
 */

// Using
using BoardGamesPlatform.EndToEnd.Tests.Support;
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Ping;

/// <summary>
/// Tests used to check the whole chain on a real SQL Server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of version 0.3.0 against the real database: the time a front receives is the one of SQL Server,
/// obtained through the real Ping procedure.</para>
/// <para><b>Behavior:</b> runs on the database named by the environment variable <see cref="TestDatabaseVariable"/> (the same as the SQL
/// tests, see tools/database/README.md), and is reported as skipped without one. A database whose name does not start with TEST_ or DEV_ is
/// refused, whatever the connection string says: the name is read from an open connection, as the SQL tests do.</para>
/// </remarks>
public sealed class SqlPingTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Environment variable holding the connection string of the throwaway database, shared with the SQL tests
    /// </summary>
    private const string TestDatabaseVariable = "BOARDGAMESPLATFORM_TEST_DATABASE";

    /// <summary>
    /// Longest duration of the test, in milliseconds
    /// </summary>
    private const int TestTimeout = 60000;

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

    /// <summary>
    /// Method used to check that the connection string names a throwaway database
    /// </summary>
    /// <param name="connectionString">[Cannot be null] Connection string of the test database</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    /// <exception cref="InvalidOperationException">If the database of the open connection does not start with TEST_ or DEV_</exception>
    private static async Task RequireThrowawayDatabaseAsync(string connectionString, CancellationToken cancellationToken)
    {
        // Opening a connection, to read the database it really reaches (not the text, which a server default could override)
        await using SqlConnection connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Check
        if (!connection.Database.StartsWith("TEST_", StringComparison.Ordinal) && !connection.Database.StartsWith("DEV_", StringComparison.Ordinal))
            throw new InvalidOperationException($"The SQL tests only run on a TEST_ or DEV_ database, not on '{connection.Database}'");
    }

    #endregion

    #region Publics

    /// <summary>
    /// A Ping of the front returns the time of the real SQL Server, close to the clock of this machine
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task PingReturnsTheTimeOfTheSqlServerAsync()
    {
        // Skipping the test when no database is configured
        CancellationToken token = TestContext.Current.CancellationToken;
        string? connectionString = Environment.GetEnvironmentVariable(TestDatabaseVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            $"No SQL test database: set {TestDatabaseVariable} after deploying one with tools/database/Deploy-TestDatabase.ps1");
        await RequireThrowawayDatabaseAsync(connectionString!, token);

        // Starting the five tiers with the real procedure, and a front
        await using TierChain chain = await TierChain.StartAsync(null, connectionString, token);
        await using HubConnection front = chain.CreateFront();
        await front.StartAsync(token);

        // Calling Ping
        PingResponse response = await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);

        // Check: the clock of the server is that of this machine, within a day
        (response.ServerTime - DateTimeOffset.UtcNow).Duration().ShouldBeLessThan(TimeSpan.FromDays(1));
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
