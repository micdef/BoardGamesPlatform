/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkLimitsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the limits of the links: the request timeout of the client, the places of the server and the pace of its refusal logs
 */

// Using
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check the limits of the link client and of the link server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a peer that never answers must not hold a caller forever, and a peer that opens connections without end must not
/// make a tier hold them all.</para>
/// <para><b>Behavior:</b> each test runs a link server on a test port of the block of this project, on the loopback address.</para>
/// </remarks>
public sealed class LinkLimitsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Name of the peer in the logs
    /// </summary>
    private const string PeerName = "the Back";

    #endregion

    #region Variables

    /// <summary>
    /// Time the Ping of the tests answers
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.FromHours(1));

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
    /// Method used to start a link server on a test port, with a Ping handler and a maximum of connections
    /// </summary>
    /// <param name="handler">[Cannot be null] Handler of the Ping operation</param>
    /// <param name="maxConnections">Maximum of connections served at the same time</param>
    /// <param name="logger">[Cannot be null] Logger of the server</param>
    /// <param name="stoppingToken">Token that stops the server</param>
    /// <param name="port">Test port the server listens on</param>
    /// <returns>
    /// The task of the running server
    /// </returns>
    private static Task StartServer(Func<PingRequest, CancellationToken, Task<PingResponse>> handler, int maxConnections, ILogger logger,
        CancellationToken stoppingToken, out int port)
    {
        // Starting the listener and the server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register(BackOperations.Ping, handler);
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), PeerName, logger, TimeSpan.Zero,
            ProtocolHandshake.DefaultServerTimeout, maxConnections);

        // Function's return
        return server.RunAsync(stoppingToken);
    }

    /// <summary>
    /// Method used to start a link server on a test port, with a Ping handler that answers at once and the given limits
    /// </summary>
    /// <param name="limits">[Cannot be null] Limits of the connections</param>
    /// <param name="handshakeTimeout">Time allowed to a client for its handshake</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the refusal logs</param>
    /// <param name="logger">[Cannot be null] Logger of the server</param>
    /// <param name="stoppingToken">Token that stops the server</param>
    /// <param name="port">Test port the server listens on</param>
    /// <returns>
    /// The task of the running server
    /// </returns>
    private static Task StartServer(LinkServerLimits limits, TimeSpan handshakeTimeout, TimeProvider timeProvider, ILogger logger, CancellationToken stoppingToken,
        out int port)
    {
        // Starting the listener and the server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register<PingRequest, PingResponse>(BackOperations.Ping,
            (request, requestToken) => Task.FromResult(new PingResponse(_pingTime)));
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), PeerName, logger, TimeSpan.Zero, handshakeTimeout,
            limits, timeProvider);

        // Function's return
        return server.RunAsync(stoppingToken);
    }

    /// <summary>
    /// Method used to open connections that say nothing, so that they stay in their handshake
    /// </summary>
    /// <param name="port">Port of the server</param>
    /// <param name="count">Number of connections</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The connections, to dispose
    /// </returns>
    private static async Task<List<ITransportConnection>> OpenSilentAsync(int port, int count, CancellationToken cancellationToken)
    {
        // Opening the connections one after the other
        List<ITransportConnection> connections = new List<ITransportConnection>();
        for (int index = 0; index < count; index++)
            connections.Add(await new TcpTransportConnector().ConnectAsync("127.0.0.1", port, cancellationToken));

        // Function's return
        return connections;
    }

    /// <summary>
    /// Method used to check that the server closes a new connection before any byte
    /// </summary>
    /// <param name="port">Port of the server</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the close is seen
    /// </returns>
    private static async Task ShouldBeRefusedAsync(int port, CancellationToken cancellationToken)
    {
        // Opening the connection and reading: the server closes it without a byte
        await using ITransportConnection refused = await new TcpTransportConnector().ConnectAsync("127.0.0.1", port, cancellationToken);
        Frame? nothing = await new FrameCodec().ReadAsync(refused.Stream, cancellationToken);
        nothing.ShouldBeNull();
    }

    /// <summary>
    /// Method used to open a connection that says nothing from a local address of the loopback network (127.0.0.0/8), as another source
    /// </summary>
    /// <param name="local">[Cannot be null] Local address of the connection</param>
    /// <param name="port">Port of the server</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The connected socket, to dispose
    /// </returns>
    private static async Task<Socket> OpenSilentFromAsync(IPAddress local, int port, CancellationToken cancellationToken)
    {
        // Binding the local address, then connecting
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(local, 0));
        await socket.ConnectAsync(new IPEndPoint(IPAddress.Loopback, port), cancellationToken);

        // Function's return
        return socket;
    }

    /// <summary>
    /// Method used to check that the server closes a connection without a byte
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection opened by the test</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the close is seen
    /// </returns>
    private static async Task ShouldBeClosedAsync(ITransportConnection connection, CancellationToken cancellationToken)
    {
        // Reading: the server closes the connection without a byte (or resets it)
        Exception? end = await Record.ExceptionAsync(async () => (await new FrameCodec().ReadAsync(connection.Stream, cancellationToken)).ShouldBeNull());
        (end is null or IOException).ShouldBeTrue(end?.ToString());
    }

    /// <summary>
    /// Method used to check that the server closes a socket without a byte
    /// </summary>
    /// <param name="socket">[Cannot be null] Socket opened by the test</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the close is seen
    /// </returns>
    private static async Task ShouldBeClosedAsync(Socket socket, CancellationToken cancellationToken)
    {
        // Reading: the server closes the socket without a byte (or resets it)
        Exception? end = await Record.ExceptionAsync(async () => (await socket.ReceiveAsync(new byte[1], SocketFlags.None, cancellationToken)).ShouldBe(0));
        (end is null or SocketException).ShouldBeTrue(end?.ToString());
    }
    /// <summary>
    /// Method used to dispose connections
    /// </summary>
    /// <param name="connections">[Cannot be null] Connections to dispose</param>
    /// <returns>
    /// A task that completes once every connection is disposed
    /// </returns>
    private static async Task DisposeAllAsync(List<ITransportConnection> connections)
    {
        foreach (ITransportConnection connection in connections)
            await connection.DisposeAsync();
    }

    #endregion

    #region Publics

    /// <summary>
    /// A request that gets no answer within the request timeout fails with the code "link.timeout", and its work stops on the peer
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestWithoutAnswerTimesOutAndIsCanceledOnThePeerAsync()
    {
        // Starting a server whose handler waits until its request is canceled
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<PingResponse> WaitingAsync(PingRequest request, CancellationToken requestToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, requestToken);
            }
            catch (OperationCanceledException)
            {
                canceled.TrySetResult();
                throw;
            }
            return new PingResponse(_pingTime);
        }
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(WaitingAsync, LinkServer.DefaultMaxConnections, NullLogger.Instance, stopping.Token, out int port);
        try
        {
            // Sending a Ping with a short request timeout
            await using LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), PeerName,
                new TcpTransportConnector(), NullLogger.Instance);
            LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(
                () => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));

            // Checks
            failure.Code.ShouldBe(ErrorCodes.LinkTimeout);
            await canceled.Task.WaitAsync(token);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// The request timeout must be greater than zero and at most its maximum
    /// </summary>
    [Fact]
    public void TheRequestTimeoutIsChecked()
    {
        // Checks
        TcpTransportConnector connector = new TcpTransportConnector();
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient("127.0.0.1", 7821, TimeSpan.FromSeconds(5), TimeSpan.Zero, PeerName, connector, NullLogger.Instance));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient("127.0.0.1", 7821, TimeSpan.FromSeconds(5), LinkClient.MaxRequestTimeout + TimeSpan.FromTicks(1),
            PeerName, connector, NullLogger.Instance));
    }

    /// <summary>
    /// Beyond its maximum of connections, the server closes a new connection at once and logs it, while the connections it serves go on
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AConnectionBeyondTheMaximumIsRefusedAsync()
    {
        // Starting a server that serves one connection at most, and opening that connection
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer((request, requestToken) => Task.FromResult(new PingResponse(_pingTime)), 1, logger, stopping.Token, out int port);
        try
        {
            await using RequestClient first = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);

            // Opening a second connection: it is closed before any byte
            await using ITransportConnection second = await new TcpTransportConnector().ConnectAsync("127.0.0.1", port, token);
            Frame? nothing = await new FrameCodec().ReadAsync(second.Stream, token);

            // Checks
            nothing.ShouldBeNull();
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("refused: 1 connections are already served"));
            (await first.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// The maximum of connections must be one or more
    /// </summary>
    [Fact]
    public void TheMaximumOfConnectionsIsChecked()
    {
        // Check
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => false);
        RequestServer server = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServer(listener, server, PeerName, NullLogger.Instance, TimeSpan.Zero,
            ProtocolHandshake.DefaultServerTimeout, 0));
    }

    /// <summary>
    /// Every limit is checked: one or more, at most the places of the handshakes for one source, an interval of zero or more; nulls are refused
    /// </summary>
    [Fact]
    public void TheLimitsAreChecked()
    {
        // Checks of the limits
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServerLimits(0, 4, 2, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServerLimits(8, 0, 1, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServerLimits(8, 4, 0, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServerLimits(8, 4, 5, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServerLimits(8, 4, 2, TimeSpan.FromTicks(-1)));
        LinkServerLimits.Default.MaxConnections.ShouldBe(LinkServer.DefaultMaxConnections);
        LinkServerLimits.Default.MaxHandshakesPerSource.ShouldBeLessThan(LinkServerLimits.Default.MaxPendingHandshakes);

        // Checks of the server
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => false);
        RequestServer server = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request));
        Should.Throw<ArgumentNullException>(() => new LinkServer(listener, server, PeerName, NullLogger.Instance, TimeSpan.Zero, ProtocolHandshake.DefaultServerTimeout,
            null!, TimeProvider.System));
        Should.Throw<ArgumentNullException>(() => new LinkServer(listener, server, PeerName, NullLogger.Instance, TimeSpan.Zero, ProtocolHandshake.DefaultServerTimeout,
            LinkServerLimits.Default, null!));
    }

    /// <summary>
    /// Connections that never finish their handshake never take the served place: the client tier is still served
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task SilentHandshakesNeverTakeTheServedPlacesAsync()
    {
        // Starting a server with one served place, and taking two places of the handshakes with silent connections
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(1, 3, 3, TimeSpan.Zero), ProtocolHandshake.DefaultServerTimeout, TimeProvider.System, NullLogger.Instance,
            stopping.Token, out int port);
        List<ITransportConnection> silent = await OpenSilentAsync(port, 2, token);
        try
        {
            // Check: the client opens its connection and is served
            await using RequestClient client = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await DisposeAllAsync(silent);
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// A source that holds its part of the places of the handshakes makes room with its own oldest connection: a new connection of the source
    /// is taken, its oldest one closed, and the close is logged
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ASourceBeyondItsPartClosesItsOldestHandshakeAsync()
    {
        // Starting a server that gives two places of the handshakes to a source, and taking them
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(8, 4, 2, TimeSpan.Zero), ProtocolHandshake.DefaultServerTimeout, TimeProvider.System, logger, stopping.Token,
            out int port);
        List<ITransportConnection> silent = await OpenSilentAsync(port, 3, token);
        try
        {
            // Checks: the oldest is closed, and the close is logged
            await ShouldBeClosedAsync(silent[0], token);
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("its source had 2 connections in their handshake"));
        }
        finally
        {
            await DisposeAllAsync(silent);
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// When every place of the handshakes is taken by several sources, a new connection is taken and the oldest connection of all closed
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task EveryHandshakePlaceTakenClosesTheOldestAsync()
    {
        // Starting a server with two places of the handshakes, taken by two sources
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(8, 2, 2, TimeSpan.Zero), ProtocolHandshake.DefaultServerTimeout, TimeProvider.System, logger, stopping.Token,
            out int port);
        using Socket first = await OpenSilentFromAsync(IPAddress.Parse("127.0.0.2"), port, token);
        using Socket second = await OpenSilentFromAsync(IPAddress.Parse("127.0.0.3"), port, token);
        try
        {
            // A third source comes
            using Socket third = await OpenSilentFromAsync(IPAddress.Parse("127.0.0.4"), port, token);

            // Checks: the oldest is closed, and the close is logged
            await ShouldBeClosedAsync(first, token);
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("2 connections were in their handshake"));
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// A source that holds connections in their handshake without end never keeps another source from being served
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFullSourceNeverKeepsAnotherSourceFromBeingServedAsync()
    {
        // Starting a server with four places of the handshakes, two per source, and a source that opens three silent connections
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(8, 4, 2, TimeSpan.Zero), ProtocolHandshake.DefaultServerTimeout, TimeProvider.System, NullLogger.Instance,
            stopping.Token, out int port);
        IPAddress hostile = IPAddress.Parse("127.0.0.2");
        using Socket first = await OpenSilentFromAsync(hostile, port, token);
        using Socket second = await OpenSilentFromAsync(hostile, port, token);
        using Socket third = await OpenSilentFromAsync(hostile, port, token);
        try
        {
            // Check: the client tier, from its own source, is served
            await using RequestClient client = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }
    /// <summary>
    /// A connection closed at the end of its handshake timeout gives its place back: the next connection is served
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ATimedOutHandshakeGivesItsPlaceBackAsync()
    {
        // Starting a server with one place of the handshakes and a short timeout, and taking the place
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(8, 1, 1, TimeSpan.Zero), TimeSpan.FromMilliseconds(300), TimeProvider.System, NullLogger.Instance,
            stopping.Token, out int port);
        List<ITransportConnection> silent = await OpenSilentAsync(port, 1, token);
        try
        {
            // Waiting for the server to close the silent connection at the end of its timeout
            Frame? nothing = await new FrameCodec().ReadAsync(silent[0].Stream, token);
            nothing.ShouldBeNull();

            // Check: the place is free again; the close is seen a little before the place is given back, so the client may have to try again
            RequestClient? client = null;
            while (client is null)
            {
                try
                {
                    client = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);
                }
                catch (Exception exception) when (exception is IOException)
                {
                    await Task.Delay(20, token);
                }
            }
            await using (client)
                (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await DisposeAllAsync(silent);
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// A connection whose handshake ends when the served places are all taken is closed, and the refusal is logged; the served one goes on
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AHandshakeThatEndsWithoutAServedPlaceIsClosedAsync()
    {
        // Starting a server with one served place; a first connection starts its handshake, then a client takes the served place
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(1, 2, 2, TimeSpan.Zero), ProtocolHandshake.DefaultServerTimeout, TimeProvider.System, logger, stopping.Token,
            out int port);
        List<ITransportConnection> late = await OpenSilentAsync(port, 1, token);
        try
        {
            // The client is served (its Ping answered means its served place is taken)
            await using RequestClient client = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);

            // The first connection ends its handshake: it is closed at once
            FrameCodec codec = new FrameCodec();
            await ProtocolHandshake.RunClientAsync(late[0], codec, token);
            Frame? nothing = await codec.ReadAsync(late[0].Stream, token);

            // Checks
            nothing.ShouldBeNull();
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("refused: 1 connections are already served"));
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await DisposeAllAsync(late);
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// A refusal is logged at most once per interval: the refusals in between are counted, and their number is logged with the next log
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheRefusalsAreLoggedAtMostOncePerIntervalAsync()
    {
        // Starting a server with one served place and a fake clock, and taking the place
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        FakeTimeProvider clock = new FakeTimeProvider();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(new LinkServerLimits(1, 1, 1, TimeSpan.FromSeconds(10)), ProtocolHandshake.DefaultServerTimeout, clock, logger, stopping.Token,
            out int port);
        try
        {
            await using RequestClient client = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);

            // Three refusals within the interval, then one after it
            for (int index = 0; index < 3; index++)
                await ShouldBeRefusedAsync(port, token);
            clock.Advance(TimeSpan.FromSeconds(10));
            await ShouldBeRefusedAsync(port, token);

            // Checks: two refusals logged, the second one with the number of the two in between
            logger.Entries.Count(entry => entry.Message.Contains("refused: 1 connections are already served")).ShouldBe(2);
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("2 other connections were refused or closed in their handshake without a log of their own"));
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
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
