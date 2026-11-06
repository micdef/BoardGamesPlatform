/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Correlation/RequestCancellationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that a request canceled by the client stops its handler on the server, and the rules of the control channel
 */

// Using
using System.Net;
using System.Text;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Correlation;

/// <summary>
/// Tests used to check the cancellation of a request across a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a request abandoned by its caller must stop the work it started on the server, and a busy server must still receive
/// that cancellation; a request left running for nobody wastes a slot and the work of every tier behind.</para>
/// <para><b>Behavior:</b> each test opens a loopback connection on a test port of the block of this project. The server end is the request
/// server under test; the client end is either a request client or a raw frame codec, used to play a client that breaks the rules. Waits are
/// driven by events, never by fixed delays.</para>
/// </remarks>
public sealed class RequestCancellationTests
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
    /// Method used to write a cancellation as a raw client
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to write to</param>
    /// <param name="correlationId">Identifier of the request to cancel</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the frame is written
    /// </returns>
    private static Task WriteCancelAsync(ITransportConnection connection, ulong correlationId, CancellationToken cancellationToken)
    {
        // Function's return
        return new FrameCodec().WriteAsync(connection.Stream, new Frame(Frame.NoCorrelation, MessageSerializer.WriteControl(new CancelMessage(correlationId))),
            cancellationToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// The cancellation is written as a control message holding the identifier of the request
    /// </summary>
    [Fact]
    public void TheCancellationFollowsTheWireFormat()
    {
        // Checks
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(new CancelMessage(42))).ShouldBe("{\"control\":\"cancel\",\"correlationId\":42}");
        MessageSerializer.ReadControl(Encoding.UTF8.GetBytes("{\"control\":\"cancel\",\"correlationId\":18446744073709551615}"))
            .ShouldBeOfType<CancelMessage>().CorrelationId.ShouldBe(ulong.MaxValue);
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadControl(Encoding.UTF8.GetBytes("{\"control\":\"cancel\",\"correlationId\":-1}")));
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadControl(Encoding.UTF8.GetBytes("{\"control\":\"cancel\"}")));
    }

    /// <summary>
    /// A request canceled by its caller stops the handler on the server, which answers nothing, and the connection goes on
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ACanceledRequestStopsItsHandlerOnTheServerAsync()
    {
        // Serving a connection with a handler that waits for its token on the first request, and echoes the others
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ReadOnlyMemory<byte>> WaitingAsync(ReadOnlyMemory<byte> request, CancellationToken requestToken)
        {
            if (request.Span[0] != 1)
                return request;
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, requestToken);
            }
            catch (OperationCanceledException)
            {
                stopped.TrySetResult();
                throw;
            }
            return request;
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), WaitingAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Canceling the first request once its handler runs
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<ReadOnlyMemory<byte>> first = client.SendAsync(new byte[] { 1 }, caller.Token);
        await started.Task.WaitAsync(token);
        await caller.CancelAsync();

        // Checks: the handler stops, the caller gets the cancellation, and the next request is answered on the same connection
        await stopped.Task.WaitAsync(token);
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        (await client.SendAsync(new byte[] { 2 }, token)).ToArray().ShouldBe(new byte[] { 2 });
        await client.DisposeAsync();
        await serving;
    }

    /// <summary>
    /// A cancellation whose callback blocks never stalls the read loop: the next request of the client is answered while the callback still
    /// runs, and the service ends cleanly once it returns
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ABlockingCancellationCallbackDoesNotStallTheReadLoopAsync()
    {
        // Serving a connection whose first request registers a callback that blocks until the test releases it, the others being echoed
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource blocking = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim release = new ManualResetEventSlim(false);
        async Task<ReadOnlyMemory<byte>> BlockingAsync(ReadOnlyMemory<byte> request, CancellationToken requestToken)
        {
            if (request.Span[0] != 1)
                return request;
            using CancellationTokenRegistration registration = requestToken.Register(() =>
            {
                blocking.TrySetResult();
                release.Wait(TimeSpan.FromMilliseconds(TestTimeout));
            });
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, requestToken);
            return request;
        }
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), BlockingAsync).ServeAsync(served, token);

        // Canceling the first request, whose callback then blocks, and sending a second one
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 1 }), token);
        await started.Task.WaitAsync(token);
        await WriteCancelAsync(raw, 1, token);
        await blocking.Task.WaitAsync(token);
        await codec.WriteAsync(raw.Stream, new Frame(2, new byte[] { 2 }), token);

        // Checks: the second request is answered while the callback blocks, and the service ends once it is released
        Frame answered = (await codec.ReadAsync(raw.Stream, token))!;
        answered.CorrelationId.ShouldBe(2UL);
        answered.Payload.ToArray().ShouldBe(new byte[] { 2 });
        release.Set();
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A busy server still reads: a request beyond its maximum is answered "busy" at once, and the cancellation of a request in progress
    /// reaches its handler although every slot is taken
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ABusyServerStillReceivesTheCancellationAsync()
    {
        // Serving a connection with one slot, held by a handler that waits for its token
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ReadOnlyMemory<byte>> WaitingAsync(ReadOnlyMemory<byte> request, CancellationToken requestToken)
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, requestToken);
            }
            catch (OperationCanceledException)
            {
                stopped.TrySetResult();
                throw;
            }
            return request;
        }
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), WaitingAsync, maxConcurrentRequests: 1).ServeAsync(served, token);

        // Sending a first request that holds the slot, then a second one
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 1 }), token);
        await started.Task.WaitAsync(token);
        await codec.WriteAsync(raw.Stream, new Frame(2, new byte[] { 2 }), token);
        Frame busy = (await codec.ReadAsync(raw.Stream, token))!;

        // Canceling the first request while the slot is still taken
        await WriteCancelAsync(raw, 1, token);
        await stopped.Task.WaitAsync(token);

        // Checks: "busy" for the second request, and closing ends the service cleanly, nothing being left in progress
        busy.CorrelationId.ShouldBe(2UL);
        Encoding.UTF8.GetString(busy.Payload.Span).ShouldBe("{\"error\":{\"code\":\"server.busy\"}}");
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A cancellation for a request that already ended, or that never existed, is ignored
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ACancellationOfAnUnknownRequestIsIgnoredAsync()
    {
        // Serving a connection with an echo handler, played by a raw client
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request)).ServeAsync(served, token);

        // Canceling identifiers that are not in progress, then sending a request
        await WriteCancelAsync(raw, 7, token);
        await WriteCancelAsync(raw, ulong.MaxValue, token);
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 9 }), token);
        Frame response = (await codec.ReadAsync(raw.Stream, token))!;
        await WriteCancelAsync(raw, 1, token);

        // Checks: the request is answered, and closing ends the service cleanly
        response.CorrelationId.ShouldBe(1UL);
        response.Payload.ToArray().ShouldBe(new byte[] { 9 });
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A request whose identifier is still in progress is a protocol violation: its response would reach the wrong request
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AnIdentifierStillInProgressIsAViolationAsync()
    {
        // Serving a connection with a handler that never answers, played by a raw client that reuses an identifier
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        async Task<ReadOnlyMemory<byte>> NeverAsync(ReadOnlyMemory<byte> request, CancellationToken requestToken)
        {
            await Task.Delay(Timeout.Infinite, requestToken);
            return request;
        }
        Task serving = new RequestServer(new FrameCodec(), NeverAsync).ServeAsync(served, token);
        await codec.WriteAsync(raw.Stream, new Frame(5, new byte[] { 1 }), token);
        await codec.WriteAsync(raw.Stream, new Frame(5, new byte[] { 2 }), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => serving);
    }

    /// <summary>
    /// After the handshake, a control message other than a cancellation, or a malformed one, is a protocol violation
    /// </summary>
    /// <param name="json">Control message the client may not send now</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData("{\"control\":\"hello\",\"minVersion\":1,\"maxVersion\":1,\"maxPayloadLength\":1048576}")]
    [InlineData("{\"control\":\"helloRefused\",\"code\":\"internal\"}")]
    [InlineData("{\"control\":\"cancel\",\"correlationId\":\"1\"}")]
    [InlineData("not json")]
    public async Task AnUnexpectedControlMessageIsAViolationAsync(string json)
    {
        // Serving a connection, played by a raw client
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request)).ServeAsync(served, token);
        await new FrameCodec().WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, Encoding.UTF8.GetBytes(json)), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => serving);
    }

    /// <summary>
    /// The client gives its place back when a request ends: with a limit of one, a canceled request lets the next one go
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheClientNeverExceedsItsLimitAndFreesThePlaceOfACanceledRequestAsync()
    {
        // Playing a server that records the requests it receives and never answers the first one
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec, maxConcurrentRequests: 1);
        using CancellationTokenSource caller = CancellationTokenSource.CreateLinkedTokenSource(token);

        // Sending two requests: the second one waits for the place of the first
        Task<ReadOnlyMemory<byte>> first = client.SendAsync(new byte[] { 1 }, caller.Token);
        Frame firstFrame = (await codec.ReadAsync(raw.Stream, token))!;
        Task<ReadOnlyMemory<byte>> second = client.SendAsync(new byte[] { 2 }, token);
        second.IsCompleted.ShouldBeFalse();

        // Canceling the first: its cancellation reaches the server, then the second request is sent and answered
        await caller.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => first);
        Frame cancel = (await codec.ReadAsync(raw.Stream, token))!;
        Frame secondFrame = (await codec.ReadAsync(raw.Stream, token))!;
        await codec.WriteAsync(raw.Stream, secondFrame, token);

        // Checks
        cancel.CorrelationId.ShouldBe(Frame.NoCorrelation);
        MessageSerializer.ReadControl(cancel.Payload.Span).ShouldBeOfType<CancelMessage>().CorrelationId.ShouldBe(firstFrame.CorrelationId);
        (await second).ToArray().ShouldBe(new byte[] { 2 });
    }

    /// <summary>
    /// A request canceled before it is written is never sent, and no cancellation is sent for it
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestCanceledBeforeItsWriteIsNeverSentAsync()
    {
        // Playing a server that records the frames it receives
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Sending a request with a token already canceled, then a normal one
        using CancellationTokenSource canceled = new CancellationTokenSource();
        await canceled.CancelAsync();
        await Should.ThrowAsync<OperationCanceledException>(() => client.SendAsync(new byte[] { 1 }, canceled.Token));
        Task<ReadOnlyMemory<byte>> next = client.SendAsync(new byte[] { 2 }, token);
        Frame received = (await codec.ReadAsync(raw.Stream, token))!;
        await codec.WriteAsync(raw.Stream, received, token);

        // Checks: the first frame received is the second request, not the canceled one nor a cancellation
        received.CorrelationId.ShouldNotBe(Frame.NoCorrelation);
        received.Payload.ToArray().ShouldBe(new byte[] { 2 });
        (await next).ToArray().ShouldBe(new byte[] { 2 });
    }

    /// <summary>
    /// The code catalogue holds the code of a busy server
    /// </summary>
    [Fact]
    public void TheBusyCodeIsWellFormed()
    {
        // Check
        ErrorCodes.IsWellFormed(ErrorCodes.ServerBusy).ShouldBeTrue();
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
