/*
 *	@file	test/BoardGamesPlatform.Back.Tests/Services/BackListenerServiceTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the listener of the Gateway in the Back, with a fake Very Back on real loopback sockets
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.Back.Settings;
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests.Services;

/// <summary>
/// Tests used to check <see cref="BackListenerService"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves the Ping operation of the Back end to end up to a fake Very Back, and that the Back never runs without
/// listening. The rules of the accept loop itself are proven by the tests of the link server, in the protocol tests.</para>
/// <para><b>Behavior:</b> every listener binds port 0 on the loopback address; each test is limited in time and waits for responses or for the
/// close of a connection, never for a fixed delay.</para>
/// </remarks>
public sealed class BackListenerServiceTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a network test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Address used by the clients to reach the services of the tests
    /// </summary>
    private const string LoopbackHost = "127.0.0.1";

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
    /// Method used to build a client of the Very Back for a port
    /// </summary>
    /// <param name="veryBackPort">Port of the Very Back</param>
    /// <returns>
    /// The client, owned by the caller
    /// </returns>
    private static VeryBackClient CreateVeryBackClient(int veryBackPort)
    {
        // Function's return
        return new VeryBackClient(new VeryBackClientOptions { Port = veryBackPort }, new TcpTransportConnector(), NullLogger<VeryBackClient>.Instance);
    }

    /// <summary>
    /// Method used to start a Back listening on port 0 of the loopback address
    /// </summary>
    /// <param name="veryBackClient">[Cannot be null] Client of the Very Back used by the Back</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The started service
    /// </returns>
    private static async Task<BackListenerService> StartBackAsync(VeryBackClient veryBackClient, CancellationToken cancellationToken)
    {
        // Creating and starting the service
        BackListenerService service = new BackListenerService(Options.Create(new BackOptions { ListenAddress = LoopbackHost, Port = 0 }), veryBackClient,
            NullLogger<BackListenerService>.Instance);
        await service.StartAsync(cancellationToken);

        // Function's return
        return service;
    }

    /// <summary>
    /// Method used to connect a request client, as the Gateway does, to a started Back
    /// </summary>
    /// <param name="service">[Cannot be null] Started service</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The client, owned by the caller
    /// </returns>
    private static async Task<RequestClient> ConnectAsGatewayAsync(BackListenerService service, CancellationToken cancellationToken)
    {
        // Connecting
        ITransportConnection connection = await new TcpTransportConnector().ConnectAsync(LoopbackHost, service.LocalEndPoint.Port, cancellationToken);

        // Function's return
        return new RequestClient(connection, new FrameCodec());
    }

    #endregion

    #region Publics

    /// <summary>
    /// A Ping sent by the Gateway is answered with the time given by the Very Back, offset included
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task APingIsAnsweredWithTheTimeOfTheVeryBackAsync()
    {
        // Starting a fake Very Back and the Back, then pinging as the Gateway
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using FakeVeryBack veryBack = new FakeVeryBack();
        await using VeryBackClient veryBackClient = CreateVeryBackClient(veryBack.Port);
        BackListenerService service = await StartBackAsync(veryBackClient, cancellationToken);
        try
        {
            await using RequestClient gateway = await ConnectAsGatewayAsync(service, cancellationToken);
            PingResponse response = await gateway.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken);

            // Checks
            response.ServerTime.ShouldBe(FakeVeryBack.PingTime);
            response.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(-5));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// When the Very Back cannot be reached, the Ping fails instead of hanging, and the Back keeps serving new connections of the Gateway
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task APingFailsWhenTheVeryBackIsDownAndTheBackKeepsServingAsync()
    {
        // Taking the port of a Very Back that stops at once (no port is picked in advance), then starting the Back
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakeVeryBack stopped = new FakeVeryBack();
        int veryBackPort = stopped.Port;
        await stopped.DisposeAsync();
        await using VeryBackClient veryBackClient = CreateVeryBackClient(veryBackPort);
        BackListenerService service = await StartBackAsync(veryBackClient, cancellationToken);
        try
        {
            // Checks: each connection of the Gateway is served, and its Ping fails with the close of the connection
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                await using RequestClient gateway = await ConnectAsGatewayAsync(service, cancellationToken);
                await Should.ThrowAsync<IOException>(() => gateway.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken));
                await gateway.Completion;
            }
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A listener lost without a stop request fails the service, and the exit code tells the service manager
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AListenerLostWithoutAStopRequestFailsTheServiceAsync()
    {
        // Starting the service on a listener that is disposed under it
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => throw new ObjectDisposedException("listener"));
        CapturingLogger<BackListenerService> logger = new CapturingLogger<BackListenerService>();
        await using VeryBackClient veryBackClient = CreateVeryBackClient(7822);
        using BackListenerService service = new BackListenerService(Options.Create(new BackOptions()), veryBackClient, logger, _ => listener, TimeSpan.Zero);
        await service.StartAsync(cancellationToken);

        // Checks
        Task execution = service.ExecuteTask.ShouldNotBeNull();
        await Should.ThrowAsync<InvalidOperationException>(() => execution.WaitAsync(cancellationToken));
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Critical && entry.Message == "The listener of the Back was closed without a stop request; the Back stops");
        HostExitCode.FromHostedServices(new IHostedService[] { service }).ShouldBe(HostExitCode.Failure);
    }

    /// <summary>
    /// A listen address that other machines can reach is accepted, with a warning; the loopback address gives none
    /// </summary>
    /// <param name="address">Address the listener claims to listen on</param>
    /// <param name="warned">Whether a warning is expected</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData("0.0.0.0", true)]
    [InlineData("127.0.0.1", false)]
    public async Task AnAddressOtherThanLoopbackIsLoggedAsAWarningAsync(string address, bool warned)
    {
        // Starting the service on an idle listener with that address, then stopping it once its loop waits for a connection
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Parse(address), 7821), _ => false);
        CapturingLogger<BackListenerService> logger = new CapturingLogger<BackListenerService>();
        await using VeryBackClient veryBackClient = CreateVeryBackClient(7822);
        using BackListenerService service = new BackListenerService(Options.Create(new BackOptions()), veryBackClient, logger, _ => listener, TimeSpan.Zero);
        await service.StartAsync(cancellationToken);
        await listener.Waiting.WaitAsync(cancellationToken);
        await service.StopAsync(cancellationToken);

        // Checks
        logger.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("not a loopback address")).ShouldBe(warned);
        service.ExecuteTask.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
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
