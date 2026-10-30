/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/GatewayFactory.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Starts the real Gateway in process, with its real Back client pointed at a port of the test
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Gateway.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Support;

/// <summary>
/// The Gateway started in process for an end-to-end test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Gateway tier of the chain: its own Program, its own SignalR hub and its own Back client, nothing replaced.</para>
/// <para><b>Behavior:</b> the only settings added are the address and the port of the Back, in memory. The hub is reached through the test
/// server (no network), by long polling, the transport that works in memory.</para>
/// </remarks>
internal sealed class GatewayFactory : WebApplicationFactory<Program>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Port the Back client of the Gateway connects to
    /// </summary>
    private readonly int _backPort;

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
    /// Constructor
    /// </summary>
    /// <param name="backPort">Port of the Back, on the loopback address</param>
    public GatewayFactory(int backPort)
    {
        // Setting
        _backPort = backPort;
    }

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
    /// Method used to build a connection to the hub of the started Gateway
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the headless front of the end-to-end tests: it does what a real front does, without a browser.</para>
    /// <para><b>Behavior:</b> the connection is built, not started; the caller starts and disposes it.</para>
    /// </remarks>
    /// <returns>
    /// The connection to the hub, not started
    /// </returns>
    public HubConnection CreateHubConnection()
    {
        // Reaching the hub through the test server, by long polling
        TestServer server = Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, PlatformHubContract.DefaultPath), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pointing the real Back client of the Gateway at the Back of the test
        Dictionary<string, string?> settings = new Dictionary<string, string?>
        {
            ["Back:Host"] = "127.0.0.1",
            ["Back:Port"] = _backPort.ToString(CultureInfo.InvariantCulture),
        };
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
