/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkClientTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the client side of a link, independently of the tier that uses it
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check <see cref="LinkClient"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the client speaks to any tier under the name it is given: the tests call "the Back", through a real link server on
/// a fixed test port of the loopback address. The reconnection and the connect timeout are proven by the tests of the Very Back client of
/// the Back.</para>
/// <para><b>Behavior:</b> each test is limited in time and waits for responses, never for a fixed delay.</para>
/// </remarks>
public sealed class LinkClientTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Name of the peer given to the client in the tests
    /// </summary>
    private const string PeerName = "the Back";

    /// <summary>
    /// Address of the peer
    /// </summary>
    private const string LoopbackHost = "127.0.0.1";

    #endregion

    #region Variables

    /// <summary>
    /// Time answered by the Ping operation of the tests
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));

    /// <summary>
    /// Connect timeout of the clients of the tests
    /// </summary>
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(5);

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
    /// Method used to start a link server that answers the Ping operation of the Back on a test port of the loopback address
    /// </summary>
    /// <param name="stoppingToken">Token that stops the server</param>
    /// <param name="port">Test port the server listens on</param>
    /// <returns>
    /// The task of the running server
    /// </returns>
    private static Task StartPeerAsync(CancellationToken stoppingToken, out int port)
    {
        // Starting the listener and the server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher().Register<PingRequest, PingResponse>(BackOperations.Ping,
            (_, _) => Task.FromResult(new PingResponse(_pingTime)));
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), PeerName, NullLogger.Instance,
            LinkServer.DefaultAcceptFailurePause);

        // Function's return
        return server.RunAsync(stoppingToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A request is answered by the peer, and the opening of the connection is logged under the name and the address of the peer
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestIsAnsweredAndTheOpeningIsLoggedAsync()
    {
        // Starting the peer, then sending a Ping
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task peer = StartPeerAsync(stopping.Token, out int port);
        CapturingLogger<LinkClientTests> logger = new CapturingLogger<LinkClientTests>();
        await using (LinkClient client = new LinkClient(LoopbackHost, port, _connectTimeout, PeerName, new TcpTransportConnector(), logger))
        {
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken);

            // Checks
            response.ServerTime.ShouldBe(_pingTime);
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == $"Connection to the Back opened ({LoopbackHost}:{port})");
        }

        // Stopping the peer
        await stopping.CancelAsync();
        await peer.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// After the disposal, a request is refused with an ObjectDisposedException naming the peer
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestAfterTheDisposalIsRefusedAsync()
    {
        // Disposing a client that never connected
        LinkClient client = new LinkClient(LoopbackHost, 7821, _connectTimeout, PeerName, new TcpTransportConnector(), NullLogger.Instance);
        await client.DisposeAsync();

        // Checks (disposing twice is harmless)
        ObjectDisposedException exception = await Should.ThrowAsync<ObjectDisposedException>(
            () => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), TestContext.Current.CancellationToken));
        exception.Message.ShouldContain(PeerName);
        await client.DisposeAsync();
    }

    /// <summary>
    /// The constructor refuses a missing dependency, a blank host or name, a port out of range and a timeout out of range
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact]
    public async Task TheConstructorChecksItsParametersAsync()
    {
        // Preparing valid parameters
        TcpTransportConnector connector = new TcpTransportConnector();
        ILogger logger = NullLogger.Instance;

        // Checks
        Should.Throw<ArgumentNullException>(() => new LinkClient(null!, 7821, _connectTimeout, PeerName, connector, logger));
        Should.Throw<ArgumentException>(() => new LinkClient(" ", 7821, _connectTimeout, PeerName, connector, logger));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient(LoopbackHost, 0, _connectTimeout, PeerName, connector, logger));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient(LoopbackHost, 65536, _connectTimeout, PeerName, connector, logger));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient(LoopbackHost, 7821, TimeSpan.Zero, PeerName, connector, logger));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient(LoopbackHost, 7821, LinkClient.MaxConnectTimeout + TimeSpan.FromTicks(1), PeerName, connector, logger));
        Should.Throw<ArgumentNullException>(() => new LinkClient(LoopbackHost, 7821, _connectTimeout, null!, connector, logger));
        Should.Throw<ArgumentException>(() => new LinkClient(LoopbackHost, 7821, _connectTimeout, "", connector, logger));
        Should.Throw<ArgumentNullException>(() => new LinkClient(LoopbackHost, 7821, _connectTimeout, PeerName, null!, logger));
        Should.Throw<ArgumentNullException>(() => new LinkClient(LoopbackHost, 7821, _connectTimeout, PeerName, connector, null!));

        // The limits themselves are accepted
        await using LinkClient upperLimits = new LinkClient(LoopbackHost, 65535, LinkClient.MaxConnectTimeout, PeerName, connector, logger);
        await using LinkClient lowerLimits = new LinkClient(LoopbackHost, 1, TimeSpan.FromTicks(1), PeerName, connector, logger);
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
