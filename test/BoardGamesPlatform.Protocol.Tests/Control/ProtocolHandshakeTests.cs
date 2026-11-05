/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Control/ProtocolHandshakeTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the handshake that opens every connection: agreement on the version, check of the limits, refusals and timeouts
 */

// Using
using System.Net;
using System.Text;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Tests.Messages;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Control;

/// <summary>
/// Tests used to check the handshake and the control messages
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> two tiers that cannot understand each other must find out before the first request, with a code; a client that
/// never speaks must not hold a connection of the server.</para>
/// <para><b>Behavior:</b> each test opens a loopback connection on a test port of the block of this project. One end runs the handshake under
/// test; the other end runs its counterpart, or a raw frame codec to play a peer that breaks the rules.</para>
/// </remarks>
public sealed class ProtocolHandshakeTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
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

    /// <summary>
    /// Method used to open a loopback connection and return both of its ends
    /// </summary>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The client end and the server end of the connection
    /// </returns>
    private static async Task<(ITransportConnection Client, ITransportConnection Server)> ConnectAsync(CancellationToken cancellationToken)
    {
        // Connecting a client to a listener on a test port
        await using TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        Task<ITransportConnection> accepting = listener.AcceptAsync(cancellationToken);
        ITransportConnection client = await new TcpTransportConnector().ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, cancellationToken);
        ITransportConnection server = await accepting;

        // Function's return
        return (client, server);
    }

    /// <summary>
    /// Method used to write a control message given as JSON text, as a raw peer
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to write to</param>
    /// <param name="json">[Cannot be null] JSON text of the message</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the frame is written
    /// </returns>
    private static Task WriteControlTextAsync(ITransportConnection connection, string json, CancellationToken cancellationToken)
    {
        // Function's return
        return new FrameCodec().WriteAsync(connection.Stream, new Frame(Frame.NoCorrelation, Encoding.UTF8.GetBytes(json)), cancellationToken);
    }

    /// <summary>
    /// Method used to read the next frame as a raw peer, and return its payload as text
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to read from</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The payload of the frame as text; null when the connection ended
    /// </returns>
    private static async Task<string?> ReadTextAsync(ITransportConnection connection, CancellationToken cancellationToken)
    {
        // Reading one frame
        Frame? frame = await new FrameCodec().ReadAsync(connection.Stream, cancellationToken);

        // Function's return
        return frame is null ? null : Encoding.UTF8.GetString(frame.Payload.Span);
    }

    #endregion

    #region Publics

    #region Agreement

    /// <summary>
    /// A client and a server of the same build agree on version 1, the client learns the limit of the server, and requests then flow
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task BothEndsAgreeAndRequestsFlowAsync()
    {
        // Running both ends of the handshake
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 7, ProtocolHandshake.DefaultServerTimeout, token);
        HandshakeResult client = await ProtocolHandshake.RunClientAsync(clientEnd, new FrameCodec(), token);
        HandshakeResult accepted = await server;

        // Sending a request on the connection, once agreed
        Task serving = new RequestServer(new FrameCodec(), (request, serviceToken) => Task.FromResult(request)).ServeAsync(served, token);
        await using RequestClient requests = new RequestClient(clientEnd, new FrameCodec());
        ReadOnlyMemory<byte> echo = await requests.SendAsync(new byte[] { 4, 2 }, token);

        // Checks
        client.Version.ShouldBe(1);
        client.MaxConcurrentRequests.ShouldBe(7);
        accepted.Version.ShouldBe(1);
        echo.ToArray().ShouldBe(new byte[] { 4, 2 });
        await requests.DisposeAsync();
        await serving;
    }

    /// <summary>
    /// The messages of the handshake follow the wire format, the kind first
    /// </summary>
    [Fact]
    public void TheControlMessagesFollowTheWireFormat()
    {
        // Checks
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(new HelloMessage(1, 1, 1048576)))
            .ShouldBe("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":1048576}");
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(new HelloAckMessage(1, 1048576, 64)))
            .ShouldBe("{\"control\":\"helloAck\",\"version\":1,\"maxPayloadLength\":1048576,\"maxConcurrentRequests\":64}");
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(new HelloRefusedMessage(ErrorCodes.ProtocolVersionUnsupported)))
            .ShouldBe("{\"control\":\"helloRefused\",\"code\":\"protocol.versionUnsupported\"}");
    }

    /// <summary>
    /// A control message is read with the type of its kind
    /// </summary>
    [Fact]
    public void AControlMessageIsReadWithTheTypeOfItsKind()
    {
        // Reading a hello
        ControlMessage message = MessageSerializer.ReadControl(Encoding.UTF8.GetBytes("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":2,\"maxPayloadLength\":512}"));

        // Checks
        HelloMessage hello = message.ShouldBeOfType<HelloMessage>();
        hello.MinVersion.ShouldBe(1);
        hello.MaxVersion.ShouldBe(2);
        hello.MaxPayloadLength.ShouldBe(512);
    }

    /// <summary>
    /// A control message that does not follow the rules is a protocol violation
    /// </summary>
    /// <param name="json">Malformed control message</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"control\":1}")]
    [InlineData("{\"control\":\"unknown\"}")]
    [InlineData("{\"control\":\"Hello\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":512}")]
    [InlineData("{\"minVersion\":1,\"control\":\"hello\",\"maxVersion\":1,\"maxPayloadLength\":512}")]
    [InlineData("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":1}")]
    [InlineData("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":512,\"extra\":true}")]
    [InlineData("{\"control\":\"hello\",\"control\":\"helloAck\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":512}")]
    [InlineData("{\"control\":\"hello\",\"minVersion\":\"1\",\"maxVersion\":1,\"maxPayloadLength\":512}")]
    [InlineData("{\"control\":\"helloRefused\"}")]
    [InlineData("{\"control\":\"helloRefused\",\"code\":null}")]
    public void AMalformedControlMessageIsRefused(string json)
    {
        // Check
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadControl(Encoding.UTF8.GetBytes(json)));
    }

    #endregion

    #region Refusals

    /// <summary>
    /// A client that speaks no version of the server is refused with a code, and the server closes the connection
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AClientWithoutACommonVersionIsRefusedAsync()
    {
        // Playing a client that only speaks versions 2 and 3
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, ProtocolHandshake.DefaultServerTimeout, token);
        await WriteControlTextAsync(raw, "{\"control\":\"hello\",\"minVersion\":2,\"maxVersion\":3,\"maxPayloadLength\":1048576}", token);

        // Checks: the refusal reaches the client, the server fails so that its caller closes
        (await ReadTextAsync(raw, token)).ShouldBe("{\"control\":\"helloRefused\",\"code\":\"protocol.versionUnsupported\"}");
        await Should.ThrowAsync<InvalidDataException>(() => server);
    }

    /// <summary>
    /// Two ends configured with different largest frames are refused at once, before any request, and the client gets the code
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DifferentLargestFramesAreRefusedAsync()
    {
        // Running a client with a smaller largest payload than the server
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection client = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, ProtocolHandshake.DefaultServerTimeout, token);

        // Checks
        RemoteErrorException refusal = await Should.ThrowAsync<RemoteErrorException>(() => ProtocolHandshake.RunClientAsync(client, new FrameCodec(2048), token));
        refusal.Code.ShouldBe(ErrorCodes.ProtocolLimitsMismatch);
        await Should.ThrowAsync<InvalidDataException>(() => server);
    }

    /// <summary>
    /// A request sent before the hello is a protocol violation: the server answers nothing and fails
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestBeforeTheHelloIsAViolationAsync()
    {
        // Playing a client that sends a request at once
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, ProtocolHandshake.DefaultServerTimeout, token);
        await new FrameCodec().WriteAsync(raw.Stream, new Frame(1, MessageSerializer.WriteRequest("Ping", new object())), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => server);
    }

    /// <summary>
    /// A control message other than the hello, first, is a protocol violation
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AnotherControlMessageFirstIsAViolationAsync()
    {
        // Playing a client that sends an acknowledgement instead of a hello
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, ProtocolHandshake.DefaultServerTimeout, token);
        await WriteControlTextAsync(raw, "{\"control\":\"helloAck\",\"version\":1,\"maxPayloadLength\":1048576,\"maxConcurrentRequests\":64}", token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => server);
    }

    /// <summary>
    /// A hello whose range of versions is empty or starts below 1 is a protocol violation
    /// </summary>
    /// <param name="minVersion">Oldest version announced</param>
    /// <param name="maxVersion">Newest version announced</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(2, 1)]
    public async Task AnInvalidRangeOfVersionsIsAViolationAsync(int minVersion, int maxVersion)
    {
        // Playing a client with that range
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task<HandshakeResult> server = ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, ProtocolHandshake.DefaultServerTimeout, token);
        await new FrameCodec().WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, MessageSerializer.WriteControl(new HelloMessage(minVersion, maxVersion, FrameCodec.DefaultMaxPayloadLength))), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => server);
    }

    /// <summary>
    /// A client that never says hello is given up after the timeout, so that it does not hold a connection of the server
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ASilentClientIsGivenUpAfterTheTimeoutAsync()
    {
        // Connecting a client that says nothing
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection silent = clientEnd;
        await using ITransportConnection served = serverEnd;

        // Check
        await Should.ThrowAsync<TimeoutException>(() => ProtocolHandshake.RunServerAsync(served, new FrameCodec(), 64, TimeSpan.FromMilliseconds(200), token));
    }

    /// <summary>
    /// The client checks the answer again: a version it did not offer, another largest payload, a limit below one or a request frame are
    /// refused
    /// </summary>
    /// <param name="answer">Answer of a server that breaks the rules</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData("{\"control\":\"helloAck\",\"version\":2,\"maxPayloadLength\":1048576,\"maxConcurrentRequests\":64}")]
    [InlineData("{\"control\":\"helloAck\",\"version\":1,\"maxPayloadLength\":2048,\"maxConcurrentRequests\":64}")]
    [InlineData("{\"control\":\"helloAck\",\"version\":1,\"maxPayloadLength\":1048576,\"maxConcurrentRequests\":0}")]
    [InlineData("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":1048576}")]
    [InlineData("{\"control\":\"helloRefused\",\"code\":\"Not a code\"}")]
    public async Task TheClientRefusesAWrongAnswerAsync(string answer)
    {
        // Playing a server that answers the hello wrongly
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection client = clientEnd;
        await using ITransportConnection raw = serverEnd;
        Task<HandshakeResult> handshake = ProtocolHandshake.RunClientAsync(client, new FrameCodec(), token);
        (await ReadTextAsync(raw, token)).ShouldNotBeNull();
        await WriteControlTextAsync(raw, answer, token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => handshake);
    }

    /// <summary>
    /// A server that answers the hello with a request frame breaks the handshake
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheClientRefusesARequestFrameAsAnswerAsync()
    {
        // Playing a server that answers with a frame on a request identifier
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection client = clientEnd;
        await using ITransportConnection raw = serverEnd;
        Task<HandshakeResult> handshake = ProtocolHandshake.RunClientAsync(client, new FrameCodec(), token);
        (await ReadTextAsync(raw, token)).ShouldNotBeNull();
        await new FrameCodec().WriteAsync(raw.Stream, new Frame(1, MessageSerializer.WriteControl(new HelloAckMessage(1, FrameCodec.DefaultMaxPayloadLength, 64))), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => handshake);
    }

    /// <summary>
    /// Through the links, a refused handshake reaches the caller as a link failure that keeps the code of the refusal
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALinkClientRefusedByTheServerFailsWithTheCodeAsync()
    {
        // Running a link server whose largest payload differs from the one of the link client
        CancellationToken token = TestContext.Current.CancellationToken;
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        int port = listener.LocalEndPoint.Port;
        using CancellationTokenSource stopping = new CancellationTokenSource();
        RequestServer requestServer = new RequestServer(new FrameCodec(2048), (request, serviceToken) => Task.FromResult(request));
        Task running = new LinkServer(listener, requestServer, "the test server", NullLogger.Instance, TimeSpan.Zero).RunAsync(stopping.Token);
        try
        {
            await using LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the test server", new TcpTransportConnector(), NullLogger.Instance);

            // Check
            LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(
                () => client.SendAsync<EmptyBody, EmptyBody>("Ping", new EmptyBody(), token));
            failure.Code.ShouldBe(ErrorCodes.ProtocolLimitsMismatch);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
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
