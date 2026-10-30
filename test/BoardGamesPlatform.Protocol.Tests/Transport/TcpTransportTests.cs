/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/TcpTransportTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the plain TCP transport on real loopback sockets
 */

// Using
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport;

/// <summary>
/// Tests used to check the plain TCP listener, connector and connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> first loopback integration tests of the platform: frames travel through a real TCP connection, as between two tiers.</para>
/// <para><b>Behavior:</b> every listener binds a fixed test port of the block of this project on the loopback address (see
/// <see cref="TestPorts"/>), never a port handed out by the system. Each test is limited in time, so that a missing frame fails the test instead of blocking the run.</para>
/// </remarks>
public sealed class TcpTransportTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a network test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Address used by the client to reach the listeners of the tests
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
    /// Method used to start a listener on the loopback address, on a port chosen by the system
    /// </summary>
    /// <returns>
    /// The started listener
    /// </returns>
    private static TcpTransportListener StartLoopbackListener()
    {
        // Function's return
        return StartLoopbackListener(TestPorts.Block.Next());
    }

    /// <summary>
    /// Method used to start a listener on the loopback address and a given port
    /// </summary>
    /// <param name="port">Test port to listen on</param>
    /// <returns>
    /// The started listener
    /// </returns>
    private static TcpTransportListener StartLoopbackListener(int port)
    {
        // Function's return
        return TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, port));
    }

    #endregion

    #region Publics

    #region Listener

    /// <summary>
    /// A listener listens on the requested address and port
    /// </summary>
    [Fact]
    public async Task AListenerListensOnTheRequestedAddressAndPortAsync()
    {
        // Starting the listener on a test port
        int port = TestPorts.Block.Next();
        await using TcpTransportListener listener = StartLoopbackListener(port);

        // Checks
        listener.LocalEndPoint.Port.ShouldBe(port);
        listener.LocalEndPoint.Address.ShouldBe(IPAddress.Loopback);
    }

    /// <summary>
    /// Waiting for a connection can be canceled
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AWaitForAConnectionCanBeCanceledAsync()
    {
        // Waiting for a connection that never comes, then canceling the wait
        await using TcpTransportListener listener = StartLoopbackListener();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        Task<ITransportConnection> waiting = listener.AcceptAsync(cancellation.Token);
        await cancellation.CancelAsync();

        // Check (a TaskCanceledException is an OperationCanceledException too)
        Exception exception = await Should.ThrowAsync<Exception>(() => waiting);
        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    /// <summary>
    /// A disposed listener refuses to wait for a connection, and disposing it twice is harmless
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ADisposedListenerRefusesToWaitAsync()
    {
        // Disposing the listener twice
        TcpTransportListener listener = StartLoopbackListener();
        await listener.DisposeAsync();
        await listener.DisposeAsync();

        // Check
        await Should.ThrowAsync<ObjectDisposedException>(() => listener.AcceptAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Disposing the listener during a wait ends the wait with an object-disposed exception, not with a socket error
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> an accept loop stops on this exception and keeps going on a socket error: a stop must never look like a failure.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingDuringAWaitEndsItAsADisposalAsync()
    {
        // Waiting for a connection, then disposing the listener
        TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> waiting = listener.AcceptAsync(TestContext.Current.CancellationToken);
        await listener.DisposeAsync();

        // Check
        await Should.ThrowAsync<ObjectDisposedException>(() => waiting);
    }

    /// <summary>
    /// After a canceled wait, the listener still accepts connections
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AListenerStaysUsableAfterACanceledWaitAsync()
    {
        // Canceling a first wait
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TcpTransportListener listener = StartLoopbackListener();
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<ITransportConnection> canceled = listener.AcceptAsync(cancellation.Token);
        await cancellation.CancelAsync();
        await Should.ThrowAsync<Exception>(() => canceled);

        // Accepting a client with a second wait
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        await using ITransportConnection client = await new TcpTransportConnector().ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Check
        server.Stream.CanRead.ShouldBeTrue();
    }

    /// <summary>
    /// A port already bound cannot be bound a second time
    /// </summary>
    [Fact]
    public async Task ABoundPortCannotBeBoundTwiceAsync()
    {
        // Binding a port, then the same port again
        await using TcpTransportListener listener = StartLoopbackListener();

        // Check
        Should.Throw<SocketException>(() => TcpTransportListener.Start(listener.LocalEndPoint));
    }

    #endregion

    #region Connection

    /// <summary>
    /// A frame sent by the client reaches the server, and the answer of the server reaches the client
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves the whole chain of a link: listener, connector, connection stream and frame codec.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task AFrameMakesARoundTripOverLoopbackAsync()
    {
        // Connecting a client to a listener
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        await using TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        await using ITransportConnection client = await new TcpTransportConnector().ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Sending a request, then answering it with the same correlation identifier
        await codec.WriteAsync(client.Stream, new Frame(42, new byte[] { 1, 2, 3 }), token);
        Frame? request = await codec.ReadAsync(server.Stream, token);
        request.ShouldNotBeNull();
        await codec.WriteAsync(server.Stream, new Frame(request.CorrelationId, new byte[] { 4, 5 }), token);
        Frame? response = await codec.ReadAsync(client.Stream, token);

        // Checks
        request.Payload.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        response.ShouldNotBeNull();
        response.CorrelationId.ShouldBe(42UL);
        response.Payload.ToArray().ShouldBe(new byte[] { 4, 5 });
    }

    /// <summary>
    /// Each end of a connection knows the address of the other end
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task EachEndKnowsTheAddressOfTheOtherAsync()
    {
        // Connecting a client to a listener
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        await using ITransportConnection client = await new TcpTransportConnector().ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Checks (both ends report the same address form: IPv4, never ::ffff:127.0.0.1)
        IPEndPoint serverSide = client.RemoteEndPoint.ShouldBeOfType<IPEndPoint>();
        serverSide.ShouldBe(listener.LocalEndPoint);
        IPEndPoint clientSide = server.RemoteEndPoint.ShouldBeOfType<IPEndPoint>();
        clientSide.Address.ShouldBe(IPAddress.Loopback);
        clientSide.Port.ShouldNotBe(listener.LocalEndPoint.Port);
    }

    /// <summary>
    /// A frame of the maximum default length crosses a real socket whole, whatever the way TCP cuts it
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALargeFrameCrossesARealSocketAsync()
    {
        // Connecting a client to a listener
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        await using TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        await using ITransportConnection client = await new TcpTransportConnector().ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Reading on the server while the client writes 1 MiB (the write cannot complete until the reader takes the bytes)
        byte[] payload = Enumerable.Range(0, FrameCodec.DefaultMaxPayloadLength).Select(value => (byte)(value % 251)).ToArray();
        Task<Frame?> reading = codec.ReadAsync(server.Stream, token);
        await codec.WriteAsync(client.Stream, new Frame(7, payload), token);
        Frame? frame = await reading;

        // Checks
        frame.ShouldNotBeNull();
        frame.CorrelationId.ShouldBe(7UL);
        frame.Payload.ToArray().ShouldBe(payload);
    }

    /// <summary>
    /// A connection disables the Nagle delay of its socket, and disposing it twice is harmless
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a request is a small frame written in one call; with the Nagle delay, it could wait for the acknowledgement of
    /// the previous segment before leaving.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task AConnectionDisablesTheNagleDelayAsync()
    {
        // Connecting a plain TCP client, then wrapping it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        using TcpClient tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        bool nagleDisabledBefore = tcpClient.NoDelay;
        TcpTransportConnection connection = new TcpTransportConnection(tcpClient);
        await using ITransportConnection server = await accepting;

        // Checks
        nagleDisabledBefore.ShouldBeFalse();
        tcpClient.NoDelay.ShouldBeTrue();
        await connection.DisposeAsync();
        await connection.DisposeAsync();
        connection.Stream.CanRead.ShouldBeFalse();
    }

    /// <summary>
    /// Connecting to a port where nothing listens fails with a socket error
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the port comes from a listener that is disposed at once; it is only read, never reserved for a later use.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task ConnectingWhereNothingListensFailsAsync()
    {
        // Taking a test port of the block where nothing listens
        int closedPort = TestPorts.Block.Next();

        // Check
        await Should.ThrowAsync<SocketException>(() => new TcpTransportConnector().ConnectAsync(LoopbackHost, closedPort, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// When the client closes its connection between two frames, the server sees a clean end
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AClientClosingBetweenFramesIsACleanEndForTheServerAsync()
    {
        // Connecting a client to a listener, then closing the client
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TcpTransportListener listener = StartLoopbackListener();
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        ITransportConnection client = await new TcpTransportConnector().ConnectAsync(LoopbackHost, listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;
        await client.DisposeAsync();

        // Check
        Frame? frame = await new FrameCodec().ReadAsync(server.Stream, token);
        frame.ShouldBeNull();
    }

    #endregion

    #region Connector arguments

    /// <summary>
    /// The connector refuses a missing host before opening anything
    /// </summary>
    /// <param name="host">Missing host</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMissingHostIsRefusedAsync(string? host)
    {
        // Check
        await Should.ThrowAsync<ArgumentNullException>(() => new TcpTransportConnector().ConnectAsync(host!, 7821, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The connector refuses a port outside 1 to 65535 before opening anything
    /// </summary>
    /// <param name="port">Invalid port</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task AnInvalidPortIsRefusedAsync(int port)
    {
        // Check
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => new TcpTransportConnector().ConnectAsync(LoopbackHost, port, TestContext.Current.CancellationToken));
    }

    #endregion

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
