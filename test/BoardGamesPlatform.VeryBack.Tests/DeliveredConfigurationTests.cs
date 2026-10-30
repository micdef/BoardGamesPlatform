/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/DeliveredConfigurationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the Very Back listens on the port of its delivered configuration
 */

// Using
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.TestKit;
using BoardGamesPlatform.VeryBack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests;

/// <summary>
/// Tests used to check the Very Back with the configuration it is delivered with
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the other tests use test ports; this one proves that the port written in the delivered <c>appsettings.json</c>,
/// the one the firewall rules open, is the port the Very Back really listens on.</para>
/// <para><b>Behavior:</b> the host is built as Program.cs builds it, from the delivered <c>appsettings.json</c> only (no environment
/// variable, no user secret), plus a connection string the SQL client can read, which is never opened. It needs that port free: a Very Back
/// running on the machine makes the test fail at once, with a message saying so.</para>
/// </remarks>
public sealed class DeliveredConfigurationTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of the test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Connection string the SQL client reads; the secret is not delivered, and this test never opens it
    /// </summary>
    private const string ReadableConnectionString = "Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=true";

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
    /// The Very Back listens on the address and the port of its delivered configuration, and accepts a connection there
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheVeryBackListensOnThePortOfItsDeliveredConfigurationAsync()
    {
        // Reading the delivered configuration, then checking its port is free
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string deliveredSettings = Path.Combine(RepositoryPaths.GetSourceFolder(), "BoardGamesPlatform.VeryBack", "appsettings.json");
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddJsonFile(deliveredSettings, optional: false, reloadOnChange: false);
        int deliveredPort = builder.Configuration.GetValue<int>("VeryBack:Port");
        string? deliveredAddress = builder.Configuration["VeryBack:ListenAddress"];
        TestPortBlock.EnsureDeliveredPortFree(deliveredPort, "Very Back");

        // Building the host as Program.cs does, with the secret that is not delivered
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Main"] = ReadableConnectionString });
        builder.Services.AddLogging();
        builder.AddPlatformVeryBack();
        using IHost host = builder.Build();
        await host.StartAsync(cancellationToken);
        try
        {
            // Checks: the listener is bound to the delivered end point, and a client reaches it there
            VeryBackListenerService listener = host.Services.GetServices<IHostedService>().OfType<VeryBackListenerService>().Single();
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
