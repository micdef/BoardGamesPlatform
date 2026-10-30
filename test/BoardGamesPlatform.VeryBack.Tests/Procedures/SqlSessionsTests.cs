/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Procedures/SqlSessionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the Very Back leaves no session open on SQL Server once it is stopped
 */

// Using
using System.Data;
using System.Globalization;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Procedures;

/// <summary>
/// Tests used to check the SQL sessions of the Very Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the SQL client keeps a session open after a connection is closed (its pool). A stop or a redeployment of the Very Back
/// must not leave sessions behind on SQL Server.</para>
/// <para><b>Behavior:</b> runs on the database named by <see cref="TestDatabase.ConnectionStringVariable"/> (skipped without one, and refusing
/// any database that is not TEST_ or DEV_). The Very Back host is built as Program.cs does, with the real procedure and a name of application
/// unique to the test, so that parallel tests never see each other's sessions. The sessions are counted in sys.dm_exec_sessions, which shows
/// every session only to a login that has <c>VIEW SERVER STATE</c> (SQL Server 2019 and earlier) or <c>VIEW SERVER PERFORMANCE STATE</c> (SQL
/// Server 2022 and later); without it a login sees only its own session. The test checks the permission first (HAS_PERMS_BY_NAME) and is skipped
/// with a clear reason when it is missing. It runs in three steps: sessions open after a Ping, still open after the stop (the pool keeps them),
/// closed after the disposal of the host (the pool is cleared).</para>
/// </remarks>
public sealed class SqlSessionsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of the database test, in milliseconds
    /// </summary>
    private const int TestTimeout = 60000;

    /// <summary>
    /// Address the Very Back listens on in the test
    /// </summary>
    private const string LoopbackHost = "127.0.0.1";

    /// <summary>
    /// Application name of the connections of the test that are not the ones of the Very Back
    /// </summary>
    private const string CounterApplicationName = "BoardGamesPlatform.Tests.SessionCounter";

    /// <summary>
    /// Query that tells (1 or 0) whether the login sees every session: VIEW SERVER PERFORMANCE STATE from SQL Server 2022, VIEW SERVER STATE before
    /// </summary>
    private const string PermissionQuery = "SELECT ISNULL(CASE WHEN CAST(SERVERPROPERTY('ProductMajorVersion') AS int) >= 16 "
        + "THEN HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER PERFORMANCE STATE') ELSE HAS_PERMS_BY_NAME(NULL, NULL, 'VIEW SERVER STATE') END, 0)";

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
    /// Method used to count the sessions of an application on SQL Server (Asynchronous)
    /// </summary>
    /// <param name="connectionString">[Cannot be null] Connection string of the test database</param>
    /// <param name="applicationName">[Cannot be null] program_name of the sessions to count</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The number of sessions open with that name
    /// </returns>
    private static async Task<int> CountSessionsAsync(string connectionString, string applicationName, CancellationToken cancellationToken)
    {
        // Counting on a connection without pool, so that the count leaves no session behind either
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(connectionString) { Pooling = false, ApplicationName = CounterApplicationName };
        await using SqlConnection connection = await TestDatabase.OpenAsync(builder.ConnectionString, cancellationToken);
        await using SqlCommand command = new SqlCommand("SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE program_name = @Name", connection);
        command.Parameters.Add(new SqlParameter("@Name", SqlDbType.NVarChar, 128) { Value = applicationName });

        // Function's return
        return (int)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    #endregion

    #region Publics

    /// <summary>
    /// The sessions of the Very Back are open after a Ping and gone once the host is stopped and disposed (skipped without a test database)
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task NoSessionStaysOpenOnceTheHostIsStoppedAsync()
    {
        // Skipping the test when no database is configured
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string? connectionString = TestDatabase.ReadConnectionString();
        if (connectionString is null)
            Assert.Skip(TestDatabase.SkipReason);

        // Refusing any database that is not a throwaway one (on a connection without pool, which leaves no session behind), then skipping when
        // the login cannot see the sessions of others: that is a missing permission, not a bug of the Very Back
        string checkConnectionString = new SqlConnectionStringBuilder(connectionString) { Pooling = false, ApplicationName = CounterApplicationName }.ConnectionString;
        await using (SqlConnection check = await TestDatabase.OpenAsync(checkConnectionString, cancellationToken))
        {
            await using SqlCommand permission = new SqlCommand(PermissionQuery, check);
            if ((int)(await permission.ExecuteScalarAsync(cancellationToken))! != 1)
                Assert.Skip("The login of the test database cannot see the sessions of others in sys.dm_exec_sessions: grant it VIEW SERVER STATE (SQL Server 2019 and earlier) or VIEW SERVER PERFORMANCE STATE (SQL Server 2022 and later)");
        }

        // Giving the Very Back a name of application nobody else uses
        string applicationName = $"BoardGamesPlatform.VeryBack.Tests.{Guid.NewGuid():N}";
        string veryBackConnectionString = new SqlConnectionStringBuilder(connectionString) { ApplicationName = applicationName, Pooling = true }.ConnectionString;

        // Building the host as Program.cs does, with the real procedure, on a port of the block of this project
        int port = TestPorts.Block.Next();
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VeryBack:ListenAddress"] = LoopbackHost,
            ["VeryBack:Port"] = port.ToString(CultureInfo.InvariantCulture),
            ["ConnectionStrings:Main"] = veryBackConnectionString,
        });
        builder.Services.AddLogging();
        builder.AddPlatformVeryBack();
        int sessionsWhileRunning;
        int sessionsAfterStopBeforeDispose;
        IHost host = builder.Build();
        try
        {
            await host.StartAsync(cancellationToken);

            // Pinging, then counting while the host still runs: the pool keeps at least one session open
            ITransportConnection connection = await new TcpTransportConnector().ConnectAsync(LoopbackHost, port, cancellationToken);
            await using (RequestClient client = new RequestClient(connection, new FrameCodec()))
            {
                await client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);
            }
            sessionsWhileRunning = await CountSessionsAsync(connectionString, applicationName, cancellationToken);

            // Stopping, as the host does when the Very Back ends, then counting before the disposal: a normal stop alone leaves the sessions
            await host.StopAsync(CancellationToken.None);
            sessionsAfterStopBeforeDispose = await CountSessionsAsync(connectionString, applicationName, cancellationToken);
        }
        finally
        {
            // Disposing the host disposes the procedure, which clears its pool
            host.Dispose();
        }

        // Checks: the sessions survive the stop (the cause), and are closed by the disposal, which SQL Server may take a moment to show
        sessionsWhileRunning.ShouldBeGreaterThanOrEqualTo(1);
        sessionsAfterStopBeforeDispose.ShouldBeGreaterThanOrEqualTo(1);
        int sessionsAfterStop = await CountSessionsAsync(connectionString, applicationName, cancellationToken);
        for (int attempt = 0; sessionsAfterStop != 0 && attempt < 20; attempt++)
        {
            await Task.Delay(250, cancellationToken);
            sessionsAfterStop = await CountSessionsAsync(connectionString, applicationName, cancellationToken);
        }
        sessionsAfterStop.ShouldBe(0);
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
