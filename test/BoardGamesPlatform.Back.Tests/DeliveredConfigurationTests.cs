/*
 *	@file	test/BoardGamesPlatform.Back.Tests/DeliveredConfigurationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the Back listens on the port of its delivered configuration
 */

// Using
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests;

/// <summary>
/// Tests used to check the Back with the configuration it is delivered with
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the other tests use test ports; this one proves that the port written in the delivered <c>appsettings.json</c>,
/// the one the firewall rules open, is the port the Back really listens on.</para>
/// <para><b>Behavior:</b> the host is built with the part of Program.cs that sets up the settings and the listener (<c>AddPlatformBack</c>), from
/// the delivered <c>appsettings.json</c> only (no environment variable, no user secret); the Very Back is not needed, since the Back reaches it only on the first request. It needs that port free: a
/// Back running on the machine makes the test fail at once, with a message saying so.</para>
/// </remarks>
public sealed class DeliveredConfigurationTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of the test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

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
    /// The Back listens on the address and the port of its delivered configuration, and accepts a connection there
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheBackListensOnThePortOfItsDeliveredConfigurationAsync()
    {
        // Taking the lock of the block of this project first: two runs of this project (two worktrees) must not both pass the check of the
        // delivered port, then collide on it
        _ = TestPorts.Block;

        // Reading the delivered configuration, then checking its port is free
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string deliveredSettings = Path.Combine(RepositoryPaths.GetSourceFolder(), "BoardGamesPlatform.Back", "appsettings.json");
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddJsonFile(deliveredSettings, optional: false, reloadOnChange: false);
        int deliveredPort = builder.Configuration.GetValue<int>("Back:Port");
        string? deliveredAddress = builder.Configuration["Back:ListenAddress"];
        TestPortBlock.EnsureDeliveredPortFree(deliveredPort, "Back");

        // Building the host as Program.cs does
        builder.Services.AddLogging();
        builder.AddPlatformBack();
        using IHost host = builder.Build();
        await host.StartAsync(cancellationToken);
        try
        {
            // Checks: the listener is bound to the delivered end point, and a client reaches it there
            BackListenerService listener = host.Services.GetServices<IHostedService>().OfType<BackListenerService>().Single();
            listener.LocalEndPoint.Port.ShouldBe(deliveredPort);
            listener.LocalEndPoint.Address.ShouldBe(IPAddress.Parse(deliveredAddress.ShouldNotBeNull()));
            using TcpClient client = new TcpClient();
            await client.ConnectAsync(listener.LocalEndPoint, cancellationToken);
            client.Connected.ShouldBeTrue();
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
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
