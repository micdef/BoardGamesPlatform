/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/GatewayHost.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Starts the real Gateway in process with a fake Back client, and builds the SignalR connection the website would use
 */

// Extern alias
extern alias GatewayApp;

// Using
using BoardGamesPlatform.Gateway.Contracts;
using GatewayApp::BoardGamesPlatform.Gateway.BackLink;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// The Gateway started in process for a test of the website
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place that knows how to start the real Gateway (its Program) and how to reach its hub like the website does.</para>
/// <para><b>Behavior:</b> the Back client of the Gateway is replaced by the one given. The hub is reached through the in-memory test
/// server, by long polling, the transport that works in memory: no socket and no port are used. <b>WebSockets, the transport a real
/// website uses first, are therefore NOT tested here.</b> The environment and the settings the Gateway reads are pinned, so that a variable
/// of the machine (such as <c>Gateway__HubPath</c>) cannot change the result.</para>
/// </remarks>
public sealed class GatewayHost : WebApplicationFactory<GatewayApp::Program>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Back client that replaces the one of the Gateway
    /// </summary>
    private readonly IBackClient _backClient;

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
    /// <param name="backClient">[Cannot be null] Back client that replaces the one of the Gateway</param>
    /// <exception cref="ArgumentNullException">If the Back client is null</exception>
    public GatewayHost(IBackClient backClient)
    {
        // Check
        if (backClient is null)
            throw new ArgumentNullException(nameof(backClient), $"The value of the parameter: {nameof(backClient)} cannot be null");

        // Setting
        _backClient = backClient;
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
    /// <para><b>Purpose:</b> what the website does, without a network.</para>
    /// <para><b>Behavior:</b> the connection is built, not started; the caller starts and disposes it.</para>
    /// </remarks>
    /// <param name="path">[Cannot be null] Path of the hub</param>
    /// <param name="probe">[Can be null] Probe that counts, fails or holds the negotiation requests</param>
    /// <returns>
    /// The connection, not started
    /// </returns>
    public HubConnection CreateHubConnection(string path, ConnectionProbe? probe = null)
    {
        // Reaching the hub through the test server, by long polling
        TestServer server = Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path), options =>
            {
                options.HttpMessageHandlerFactory = _ => probe is null ? server.CreateHandler() : new ProbingHandler(server.CreateHandler(), probe);
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
        // Pinning the environment and the settings the Gateway reads; added last, they win over the environment variables of the machine
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:HubPath"] = PlatformHubContract.DefaultPath,
                ["Observability:OtlpEndpoint"] = string.Empty
            }));

        // Replacing the Back client
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IBackClient>();
            services.AddSingleton(_backClient);
        });
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
