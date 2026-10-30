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
using BoardGamesPlatform.TestKit;
using Microsoft.AspNetCore.SignalR.Client;

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
/// <para><b>Behavior:</b> runs on the database named by the environment variable <see cref="TestDatabase.ConnectionStringVariable"/> (the same
/// as the SQL tests, see tools/database/README.md), and is reported as skipped without one. A database whose name does not start with TEST_ or
/// DEV_ is refused by the guard shared with the other SQL tests (<see cref="TestDatabase"/>), whatever the connection string says.</para>
/// </remarks>
public sealed class SqlPingTests
{
    #region Members

    #region Constants

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

    // #N/A

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
        string? connectionString = TestDatabase.ReadConnectionString();
        if (connectionString is null)
            Assert.Skip(TestDatabase.SkipReason);

        // Refusing any database that is not a throwaway one (the connection only proves the name of the database)
        await using (await TestDatabase.OpenAsync(connectionString, token))
        {
        }

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
