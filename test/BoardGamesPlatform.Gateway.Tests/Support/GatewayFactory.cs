/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/GatewayFactory.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Starts the Gateway in process, with an optional fake Back client and hub path
 */

// Using
using BoardGamesPlatform.Gateway.BackLink;
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
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// The Gateway started in process for a test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place that knows how to start the real Gateway (its Program) in a test and how to reach its hub.</para>
/// <para><b>Behavior:</b> without a Back client, the Gateway keeps the one it registers itself. With one, the test client replaces it.
/// The hub path and the port of the Back are set by configuration when given. The hub is reached through the test server, by long polling, the transport that works
/// in memory.</para>
/// </remarks>
public sealed class GatewayFactory : WebApplicationFactory<Program>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Back client that replaces the one of the Gateway; null to keep the Gateway's own
    /// </summary>
    private readonly IBackClient? _backClient;

    /// <summary>
    /// Value of the Gateway:HubPath setting; null to leave the setting out
    /// </summary>
    private readonly string? _hubPath;

    /// <summary>
    /// Value of the Back:Port setting, as text so that a test can give an invalid one; null to leave the setting out
    /// </summary>
    private readonly string? _backPort;

    /// <summary>
    /// Other settings added to the configuration, by key; null for none
    /// </summary>
    private readonly IReadOnlyDictionary<string, string?>? _extraSettings;

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
    /// <param name="backClient">[Can be null] Back client that replaces the one of the Gateway</param>
    /// <param name="hubPath">[Can be null] Value of the Gateway:HubPath setting</param>
    /// <param name="backPort">[Can be null] Value of the Back:Port setting</param>
    /// <param name="extraSettings">[Can be null] Other settings to add to the configuration, by key (for example Back:Host)</param>
    public GatewayFactory(IBackClient? backClient = null, string? hubPath = null, string? backPort = null,
        IReadOnlyDictionary<string, string?>? extraSettings = null)
    {
        // Setting
        _backClient = backClient;
        _hubPath = hubPath;
        _backPort = backPort;
        _extraSettings = extraSettings;
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
    /// <para><b>Purpose:</b> what a front does, without a network.</para>
    /// <para><b>Behavior:</b> the connection is built, not started; the caller starts and disposes it.</para>
    /// </remarks>
    /// <param name="path">[Cannot be null] Path of the hub</param>
    /// <returns>
    /// The connection, not started
    /// </returns>
    public HubConnection CreateHubConnection(string path)
    {
        // Reaching the hub through the test server, by long polling
        TestServer server = Server;
        return new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, path), options =>
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
        // Setting the hub path and the port of the Back
        Dictionary<string, string?> settings = new Dictionary<string, string?>();
        if (_hubPath is not null)
            settings["Gateway:HubPath"] = _hubPath;
        if (_backPort is not null)
            settings["Back:Port"] = _backPort;
        if (_extraSettings is not null)
        {
            foreach (KeyValuePair<string, string?> setting in _extraSettings)
                settings[setting.Key] = setting.Value;
        }
        if (settings.Count > 0)
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(settings));

        // Replacing the Back client
        if (_backClient is not null)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IBackClient>();
                services.AddSingleton(_backClient);
            });
        }
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
