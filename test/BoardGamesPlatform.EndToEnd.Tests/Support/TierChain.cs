/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/TierChain.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Starts the Very Back, the Back and the Gateway in one process, and gives the headless front that calls them
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Back;
using BoardGamesPlatform.VeryBack;
using BoardGamesPlatform.VeryBack.Procedures;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Support;

/// <summary>
/// The tiers of the walking skeleton, started in one process with their real code
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place that knows how to start the chain front, Gateway, Back, Very Back, procedure, so that each end-to-end test
/// only states what it proves.</para>
/// <para><b>Behavior:</b> the tiers start in dependency order (Very Back, Back, Gateway) and stop in the reverse order. The Very Back and
/// the Back are built with <c>AddPlatformVeryBack</c> and <c>AddPlatformBack</c>, as their Program.cs build them, from settings in memory, and
/// listen on two fixed ports of the block of the project, on the loopback address. The Gateway is its own Program, in memory, with its real
/// Back client pointed at the port of the Back. Only the Ping procedure may be replaced by a fake: with a fake, the chain stops at the
/// procedure; without one, the real <see cref="SqlPingProcedure"/> calls the database of the connection string.</para>
/// </remarks>
internal sealed class TierChain : IAsyncDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// A connection string the SQL client can read, for the runs with a fake procedure: no connection is ever opened with it
    /// </summary>
    private const string UnusedConnectionString = "Server=localhost;Database=TEST_BoardGamesPlatformE2E;Integrated Security=true";

    #endregion

    #region Variables

    /// <summary>
    /// Host of the Very Back; null once stopped
    /// </summary>
    private IHost? _veryBack;

    /// <summary>
    /// Host of the Back; null once stopped
    /// </summary>
    private IHost? _back;

    /// <summary>
    /// The Gateway; null once stopped
    /// </summary>
    private GatewayFactory? _gateway;

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

    /// <summary>
    /// Constructor; use <see cref="StartAsync"/> to build a started chain
    /// </summary>
    /// <param name="veryBack">[Cannot be null] Started host of the Very Back</param>
    /// <param name="back">[Cannot be null] Started host of the Back</param>
    /// <param name="gateway">[Cannot be null] The Gateway, in process</param>
    private TierChain(IHost veryBack, IHost back, GatewayFactory gateway)
    {
        // Setting
        _veryBack = veryBack;
        _back = back;
        _gateway = gateway;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build a host of the Very Back as Program.cs does, from settings in memory
    /// </summary>
    /// <param name="port">Port the Very Back listens on</param>
    /// <param name="procedure">[Can be null] Fake that replaces the Ping procedure; null for the real one</param>
    /// <param name="connectionString">[Can be null] Main connection string; null for one that is never opened</param>
    /// <returns>
    /// The host, not started
    /// </returns>
    private static IHost BuildVeryBack(int port, IPingProcedure? procedure, string? connectionString)
    {
        // Building the host with the registrations of Program.cs
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VeryBack:ListenAddress"] = "127.0.0.1",
            ["VeryBack:Port"] = port.ToString(CultureInfo.InvariantCulture),
            ["ConnectionStrings:Main"] = connectionString ?? UnusedConnectionString,
        });
        builder.Services.AddLogging();
        builder.AddPlatformVeryBack();

        // Replacing the Ping procedure by the fake, the only thing a test may replace
        if (procedure is not null)
        {
            builder.Services.RemoveAll<IPingProcedure>();
            builder.Services.AddSingleton(procedure);
        }

        // Function's return
        return builder.Build();
    }

    /// <summary>
    /// Method used to build a host of the Back as Program.cs does, from settings in memory
    /// </summary>
    /// <param name="port">Port the Back listens on</param>
    /// <param name="veryBackPort">Port of the Very Back</param>
    /// <returns>
    /// The host, not started
    /// </returns>
    private static IHost BuildBack(int port, int veryBackPort)
    {
        // Building the host with the registrations of Program.cs
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Back:ListenAddress"] = "127.0.0.1",
            ["Back:Port"] = port.ToString(CultureInfo.InvariantCulture),
            ["VeryBack:Host"] = "127.0.0.1",
            ["VeryBack:Port"] = veryBackPort.ToString(CultureInfo.InvariantCulture),
        });
        builder.Services.AddLogging();
        builder.AddPlatformBack();

        // Function's return
        return builder.Build();
    }

    /// <summary>
    /// Method used to stop a host and release it
    /// </summary>
    /// <param name="host">[Cannot be null] Host to stop</param>
    /// <returns>
    /// A task that completes when the host is stopped and disposed
    /// </returns>
    private static async Task StopAsync(IHost host)
    {
        // Stopping the host, then releasing it
        try
        {
            await host.StopAsync();
        }
        finally
        {
            host.Dispose();
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to start the tiers, in dependency order
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a running chain for a test.</para>
    /// <para><b>Behavior:</b> starts the Very Back, then the Back, then the Gateway. The listeners of the Very Back and of the Back are bound
    /// when their host has started. If a tier fails to start, the tiers already started are stopped before the exception is thrown.</para>
    /// </remarks>
    /// <param name="procedure">[Can be null] Fake that replaces the Ping procedure; null for the real one, which needs the connection string</param>
    /// <param name="connectionString">[Can be null] Main connection string of the Very Back, for the real procedure</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The started chain, owned by the caller
    /// </returns>
    public static async Task<TierChain> StartAsync(IPingProcedure? procedure, string? connectionString, CancellationToken cancellationToken)
    {
        // Taking the ports of the block: the Very Back, then the Back
        int veryBackPort = TestPorts.Block.Next();
        int backPort = TestPorts.Block.Next();

        // Starting the Very Back, then the Back, then the Gateway; a failure stops what is already started
        IHost veryBack = BuildVeryBack(veryBackPort, procedure, connectionString);
        IHost? back = null;
        GatewayFactory? gateway = null;
        try
        {
            await veryBack.StartAsync(cancellationToken);
            back = BuildBack(backPort, veryBackPort);
            await back.StartAsync(cancellationToken);
            gateway = new GatewayFactory(backPort);
            _ = gateway.Server;
        }
        catch
        {
            // Stopping in the reverse order
            if (gateway is not null)
                await gateway.DisposeAsync();
            if (back is not null)
                await StopAsync(back);
            await StopAsync(veryBack);
            throw;
        }

        // Function's return
        return new TierChain(veryBack, back, gateway);
    }

    /// <summary>
    /// Method used to build the headless front: a SignalR connection to the hub of the Gateway
    /// </summary>
    /// <returns>
    /// The connection, not started; the caller starts and disposes it
    /// </returns>
    /// <exception cref="InvalidOperationException">If the Gateway is stopped</exception>
    public HubConnection CreateFront()
    {
        // Check
        GatewayFactory gateway = _gateway ?? throw new InvalidOperationException("The Gateway is stopped");

        // Function's return
        return gateway.CreateHubConnection();
    }

    /// <summary>
    /// Method used to stop the Very Back alone, as when its process dies
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves what the other tiers do when the tier below them is gone.</para>
    /// <para><b>Behavior:</b> stops and disposes the host of the Very Back; its port is closed. Does nothing if it is already stopped.</para>
    /// </remarks>
    /// <returns>
    /// A task that completes when the Very Back is stopped
    /// </returns>
    public async Task StopVeryBackAsync()
    {
        // Stopping the Very Back
        IHost? veryBack = _veryBack;
        _veryBack = null;
        if (veryBack is not null)
            await StopAsync(veryBack);
    }

    /// <summary>
    /// Method used to stop the tiers, in the reverse order of the start: the Gateway, the Back, then the Very Back
    /// </summary>
    /// <returns>
    /// A task that completes when every tier is stopped
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Stopping the Gateway, then the Back
        GatewayFactory? gateway = _gateway;
        _gateway = null;
        if (gateway is not null)
            await gateway.DisposeAsync();
        IHost? back = _back;
        _back = null;
        if (back is not null)
            await StopAsync(back);

        // Stopping the Very Back last
        await StopVeryBackAsync();
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
