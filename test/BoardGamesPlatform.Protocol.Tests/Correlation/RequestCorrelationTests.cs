/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Correlation/RequestCorrelationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that requests and responses are paired on one connection, and that every failure closes the link cleanly
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Correlation;

/// <summary>
/// Tests used to check the request client and the request server on loopback connections
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> several requests share one connection; a response given to the wrong request, or a request left waiting forever,
/// would be a silent and serious failure.</para>
/// <para><b>Behavior:</b> each test opens a loopback connection on a port chosen by the system. One end is the class under test; the other end
/// is either its counterpart or a raw frame codec, used to play a peer that breaks the rules. Waits are driven by events, never by fixed delays,
/// except one negative check that is documented on its test.</para>
/// </remarks>
public sealed class RequestCorrelationTests
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
        // Connecting a client to a listener on a port chosen by the system
        await using TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, 0));
        Task<ITransportConnection> accepting = listener.AcceptAsync(cancellationToken);
        ITransportConnection client = await new TcpTransportConnector().ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, cancellationToken);
        ITransportConnection server = await accepting;

        // Function's return
        return (client, server);
    }

    /// <summary>
    /// Method used to answer each request with its own content, as a server handler
    /// </summary>
    /// <param name="request">Content of the request</param>
    /// <param name="cancellationToken">Token of the service</param>
    /// <returns>
    /// The content of the request
    /// </returns>
    private static Task<ReadOnlyMemory<byte>> EchoAsync(ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        // Function's return
        return Task.FromResult(request);
    }

    /// <summary>
    /// Method used to raise a shared maximum to a value, safely from several threads
    /// </summary>
    /// <param name="maximum">Shared maximum</param>
    /// <param name="value">Value to compare with</param>
    private static void InterlockedMax(ref int maximum, int value)
    {
        // Retrying until the maximum is at least the value
        int current = Volatile.Read(ref maximum);
        while (value > current)
        {
            int previous = Interlocked.CompareExchange(ref maximum, value, current);
            if (previous == current)
                return;
            current = previous;
        }
    }

    #endregion

    #region Publics

    #region Pairing

    /// <summary>
    /// A request receives the response of the server
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestReceivesItsResponseAsync()
    {
        // Serving a connection with an echo handler
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), EchoAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Sending a request
        ReadOnlyMemory<byte> response = await client.SendAsync(new byte[] { 1, 2, 3 }, token);

        // Checks (closing the client ends the service cleanly)
        response.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        await client.DisposeAsync();
        await serving;
    }

    /// <summary>
    /// When the server answers in another order, each request still receives its own response
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ResponsesInAnotherOrderReachTheirRequestsAsync()
    {
        // Serving a connection with a handler that answers each request when the test releases it (first byte = gate number)
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource[] gates = new TaskCompletionSource[] { new TaskCompletionSource(), new TaskCompletionSource() };
        TaskCompletionSource bothStarted = new TaskCompletionSource();
        int started = 0;
        async Task<ReadOnlyMemory<byte>> GatedAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            if (Interlocked.Increment(ref started) == 2)
                bothStarted.TrySetResult();
            await gates[request.Span[0]].Task.WaitAsync(serviceToken);
            return new byte[] { (byte)(request.Span[0] + 10) };
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), GatedAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Sending two requests, then answering the second one first
        Task<ReadOnlyMemory<byte>> first = client.SendAsync(new byte[] { 0 }, token);
        Task<ReadOnlyMemory<byte>> second = client.SendAsync(new byte[] { 1 }, token);
        await bothStarted.Task.WaitAsync(token);
        gates[1].SetResult();
        ReadOnlyMemory<byte> secondResponse = await second;
        first.IsCompleted.ShouldBeFalse();
        gates[0].SetResult();
        ReadOnlyMemory<byte> firstResponse = await first;

        // Checks (closing the client ends the service cleanly)
        firstResponse.ToArray().ShouldBe(new byte[] { 10 });
        secondResponse.ToArray().ShouldBe(new byte[] { 11 });
        await client.DisposeAsync();
        await serving;
    }

    /// <summary>
    /// The correlation identifiers of the requests start at 1 and increase; the reserved identifier is never used
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task CorrelationIdentifiersStartAtOneAndIncreaseAsync()
    {
        // Playing the server with a raw codec that records the identifiers and echoes the requests
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        List<ulong> identifiers = new List<ulong>();
        Task server = Task.Run(async () =>
        {
            for (int count = 0; count < 3; count++)
            {
                Frame request = (await codec.ReadAsync(raw.Stream, token))!;
                identifiers.Add(request.CorrelationId);
                await codec.WriteAsync(raw.Stream, request, token);
            }
        }, token);
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Sending three requests, one after the other
        for (byte value = 0; value < 3; value++)
            await client.SendAsync(new byte[] { value }, token);
        await server;

        // Check
        identifiers.ShouldBe(new ulong[] { 1, 2, 3 });
    }

    #endregion

    #region Client failures

    /// <summary>
    /// When the peer closes the connection, a pending request fails instead of waiting forever, and the client is closed
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task APeerClosingFailsThePendingRequestAsync()
    {
        // Playing a server that reads the request, then closes the connection without answering
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        Task server = Task.Run(async () =>
        {
            await codec.ReadAsync(serverEnd.Stream, token);
            await serverEnd.DisposeAsync();
        }, token);
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Checks
        await Should.ThrowAsync<IOException>(() => client.SendAsync(new byte[] { 1 }, token));
        await client.Completion.WaitAsync(token);
        await Should.ThrowAsync<IOException>(() => client.SendAsync(new byte[] { 2 }, token));
        await server;
    }

    /// <summary>
    /// A canceled request stops waiting; its late response is ignored and the next request still works
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ACanceledRequestIgnoresItsLateResponseAsync()
    {
        // Playing a server that answers the first request only after the test lets it, then echoes the second
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        TaskCompletionSource firstRead = new TaskCompletionSource();
        TaskCompletionSource answerLate = new TaskCompletionSource();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        Task server = Task.Run(async () =>
        {
            Frame first = (await codec.ReadAsync(raw.Stream, token))!;
            firstRead.SetResult();
            await answerLate.Task.WaitAsync(token);
            await codec.WriteAsync(raw.Stream, new Frame(first.CorrelationId, new byte[] { 99 }), token);
            Frame second = (await codec.ReadAsync(raw.Stream, token))!;
            await codec.WriteAsync(raw.Stream, second, token);
        }, token);
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Canceling the first request once the server has it, then letting its response arrive late
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<ReadOnlyMemory<byte>> first = client.SendAsync(new byte[] { 1 }, cancellation.Token);
        await firstRead.Task.WaitAsync(token);
        await cancellation.CancelAsync();
        Exception canceled = await Should.ThrowAsync<Exception>(() => first);
        answerLate.SetResult();
        ReadOnlyMemory<byte> secondResponse = await client.SendAsync(new byte[] { 2 }, token);
        await server;

        // Checks
        canceled.ShouldBeAssignableTo<OperationCanceledException>();
        secondResponse.ToArray().ShouldBe(new byte[] { 2 });
        client.Completion.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// A response to an identifier the client never sent is a protocol violation: the client closes and the request fails
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AResponseToAnUnknownIdentifierClosesTheClientAsync()
    {
        // Playing a server that answers with an identifier that was never sent
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        Task server = Task.Run(async () =>
        {
            await codec.ReadAsync(raw.Stream, token);
            await codec.WriteAsync(raw.Stream, new Frame(99, new byte[] { 1 }), token);
        }, token);
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Checks
        IOException exception = await Should.ThrowAsync<IOException>(() => client.SendAsync(new byte[] { 1 }, token));
        exception.InnerException.ShouldBeOfType<InvalidDataException>();
        await client.Completion.WaitAsync(token);
        await server;
    }

    /// <summary>
    /// A frame with the reserved identifier, sent as a response, is a protocol violation: the client closes and the request fails
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AResponseWithTheReservedIdentifierClosesTheClientAsync()
    {
        // Playing a server that answers with the reserved identifier
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        Task server = Task.Run(async () =>
        {
            await codec.ReadAsync(raw.Stream, token);
            await codec.WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, new byte[] { 1 }), token);
        }, token);
        await using RequestClient client = new RequestClient(clientEnd, codec);

        // Checks
        IOException exception = await Should.ThrowAsync<IOException>(() => client.SendAsync(new byte[] { 1 }, token));
        exception.InnerException.ShouldBeOfType<InvalidDataException>();
        await server;
    }

    /// <summary>
    /// Disposing the client fails the requests still waiting with an object-disposed exception
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingTheClientFailsThePendingRequestsAsync()
    {
        // Playing a server that reads the request and never answers
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        TaskCompletionSource requestRead = new TaskCompletionSource();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        Task server = Task.Run(async () =>
        {
            await codec.ReadAsync(raw.Stream, token);
            requestRead.SetResult();
        }, token);
        RequestClient client = new RequestClient(clientEnd, codec);

        // Disposing the client while the request waits
        Task<ReadOnlyMemory<byte>> request = client.SendAsync(new byte[] { 1 }, token);
        await requestRead.Task.WaitAsync(token);
        await client.DisposeAsync();

        // Checks
        await Should.ThrowAsync<ObjectDisposedException>(() => request);
        client.Completion.IsCompleted.ShouldBeTrue();
        await server;
    }

    /// <summary>
    /// A payload larger than the maximum is refused before anything is sent, and the client stays usable
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ATooLargeRequestIsRefusedAndTheClientStaysUsableAsync()
    {
        // Serving a connection with an echo handler and a small maximum length
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(16), EchoAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec(16));

        // Checks
        await Should.ThrowAsync<ArgumentException>(() => client.SendAsync(new byte[17], token));
        ReadOnlyMemory<byte> response = await client.SendAsync(new byte[] { 5 }, token);
        response.ToArray().ShouldBe(new byte[] { 5 });
    }

    /// <summary>
    /// Many requests sent at once, answered after random delays, each receive their own response
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> stresses the pairing: a response given to the wrong request would show as a payload that does not match.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task ManyConcurrentRequestsReceiveTheirOwnResponsesAsync()
    {
        // Serving a connection with a handler that echoes after a random delay
        CancellationToken token = TestContext.Current.CancellationToken;
        static async Task<ReadOnlyMemory<byte>> DelayedEchoAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            await Task.Delay(Random.Shared.Next(0, 20), serviceToken);
            return request;
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), DelayedEchoAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Sending 200 requests at once, each with its own number
        Task<ReadOnlyMemory<byte>>[] requests = Enumerable.Range(0, 200)
            .Select(value => client.SendAsync(BitConverter.GetBytes(value), token))
            .ToArray();
        ReadOnlyMemory<byte>[] responses = await Task.WhenAll(requests);

        // Check
        responses.Select(response => BitConverter.ToInt32(response.Span)).ShouldBe(Enumerable.Range(0, 200));
    }

    /// <summary>
    /// A disposed client refuses new requests
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ADisposedClientRefusesRequestsAsync()
    {
        // Disposing the client twice
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        RequestClient client = new RequestClient(clientEnd, new FrameCodec());
        await client.DisposeAsync();
        await client.DisposeAsync();

        // Check
        await Should.ThrowAsync<ObjectDisposedException>(() => client.SendAsync(new byte[] { 1 }, token));
    }

    #endregion

    #region Server behavior

    /// <summary>
    /// The service of a connection ends without error when the client closes it between two requests
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServiceEndsCleanlyWhenTheClientLeavesAsync()
    {
        // Serving a connection, sending one request, then closing the client
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), EchoAsync).ServeAsync(served, token);
        RequestClient client = new RequestClient(clientEnd, new FrameCodec());
        await client.SendAsync(new byte[] { 1 }, token);
        await client.DisposeAsync();

        // Check (no exception)
        await serving;
    }

    /// <summary>
    /// The server refuses a request that carries the reserved correlation identifier
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServerRefusesTheReservedIdentifierAsync()
    {
        // Playing a client that sends the reserved identifier
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(codec, EchoAsync).ServeAsync(served, token);
        await codec.WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, new byte[] { 1 }), token);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => serving);
    }

    /// <summary>
    /// A handler that throws ends the service with its exception, and the request fails on the client instead of waiting forever
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingHandlerEndsTheServiceAsync()
    {
        // Serving a connection with a handler that always fails
        CancellationToken token = TestContext.Current.CancellationToken;
        static Task<ReadOnlyMemory<byte>> FailingAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            throw new InvalidOperationException("Handler failure");
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), FailingAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());
        Task<ReadOnlyMemory<byte>> request = client.SendAsync(new byte[] { 1 }, token);

        // Checks: the service throws the failure of the handler; the owner of the connection then closes it, which fails the request
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => serving);
        failure.Message.ShouldBe("Handler failure");
        await served.DisposeAsync();
        await Should.ThrowAsync<IOException>(() => request);
    }

    /// <summary>
    /// A handler that fails after its first await also ends the service with its exception
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> regression test: the service used to wait forever when the read loop was stopped while it held a slot.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task AnAsynchronouslyFailingHandlerEndsTheServiceAsync()
    {
        // Serving a connection with a handler that fails after an await
        CancellationToken token = TestContext.Current.CancellationToken;
        static async Task<ReadOnlyMemory<byte>> LateFailingAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            await Task.Yield();
            throw new InvalidOperationException("Late handler failure");
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), LateFailingAsync).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());
        Task<ReadOnlyMemory<byte>> request = client.SendAsync(new byte[] { 1 }, token);

        // Checks
        InvalidOperationException failure = await Should.ThrowAsync<InvalidOperationException>(() => serving);
        failure.Message.ShouldBe("Late handler failure");
        await served.DisposeAsync();
        await Should.ThrowAsync<IOException>(() => request);
    }

    /// <summary>
    /// The service of an idle connection stops when its token is canceled
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServiceStopsWhenItsTokenIsCanceledAsync()
    {
        // Serving an idle connection, then canceling the service
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task serving = new RequestServer(new FrameCodec(), EchoAsync).ServeAsync(served, cancellation.Token);
        await cancellation.CancelAsync();

        // Check (a TaskCanceledException is an OperationCanceledException too)
        Exception exception = await Should.ThrowAsync<Exception>(() => serving);
        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    /// <summary>
    /// A request cut off in the middle ends the service with a protocol error
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ATruncatedRequestEndsTheServiceAsync()
    {
        // Playing a client that sends a header announcing 10 bytes, then 2 bytes, then closes
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), EchoAsync).ServeAsync(served, token);
        byte[] truncated = new byte[] { 0, 0, 0, 10, 0, 0, 0, 0, 0, 0, 0, 1, 7, 7 };
        await clientEnd.Stream.WriteAsync(truncated, token);
        await clientEnd.DisposeAsync();

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => serving);
    }

    /// <summary>
    /// When the client stops sending while a request is in progress, its response is still written before the service ends
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestInProgressIsAnsweredBeforeACleanEndAsync()
    {
        // Serving a connection with a handler that waits for the test
        CancellationToken token = TestContext.Current.CancellationToken;
        FrameCodec codec = new FrameCodec();
        TaskCompletionSource handlerStarted = new TaskCompletionSource();
        TaskCompletionSource release = new TaskCompletionSource();
        async Task<ReadOnlyMemory<byte>> WaitingAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            handlerStarted.TrySetResult();
            await release.Task.WaitAsync(serviceToken);
            return request;
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(codec, WaitingAsync).ServeAsync(served, token);

        // Sending a request, then closing the sending side only (the client still reads)
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 42 }), token);
        await handlerStarted.Task.WaitAsync(token);
        ((System.Net.Sockets.NetworkStream)raw.Stream).Socket.Shutdown(System.Net.Sockets.SocketShutdown.Send);
        release.SetResult();
        Frame? response = await codec.ReadAsync(raw.Stream, token);

        // Checks (the service ends without error)
        response.ShouldNotBeNull();
        response.CorrelationId.ShouldBe(1UL);
        response.Payload.ToArray().ShouldBe(new byte[] { 42 });
        await serving;
    }

    /// <summary>
    /// The server never handles more requests of one connection at the same time than its maximum
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> with a maximum of 2 and 4 requests sent at once, the test waits until 2 handlers have started, then checks that
    /// no third one starts within 300 milliseconds: proving that something does not happen needs a bounded wait, the only one of this class.
    /// Releasing the handlers then lets the 4 requests complete, never more than 2 at a time.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServerHandlesAtMostItsMaximumOfRequestsAsync()
    {
        // Serving a connection with a handler that waits for the test and counts the requests in progress
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource twoStarted = new TaskCompletionSource();
        TaskCompletionSource thirdStarted = new TaskCompletionSource();
        int started = 0;
        int inProgress = 0;
        int mostInProgress = 0;
        async Task<ReadOnlyMemory<byte>> CountingAsync(ReadOnlyMemory<byte> request, CancellationToken serviceToken)
        {
            int count = Interlocked.Increment(ref started);
            if (count == 2)
                twoStarted.TrySetResult();
            if (count == 3)
                thirdStarted.TrySetResult();
            int now = Interlocked.Increment(ref inProgress);
            InterlockedMax(ref mostInProgress, now);
            await release.Task.WaitAsync(serviceToken);
            Interlocked.Decrement(ref inProgress);
            return request;
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), CountingAsync, maxConcurrentRequests: 2).ServeAsync(served, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Sending 4 requests at once, then checking that a third handler does not start while two are busy
        Task<ReadOnlyMemory<byte>>[] requests = Enumerable.Range(0, 4).Select(value => client.SendAsync(new byte[] { (byte)value }, token)).ToArray();
        await twoStarted.Task.WaitAsync(token);
        Task first = await Task.WhenAny(thirdStarted.Task, Task.Delay(300, token));
        bool thirdStartedTooEarly = first == thirdStarted.Task;
        release.SetResult();
        ReadOnlyMemory<byte>[] responses = await Task.WhenAll(requests);

        // Checks
        thirdStartedTooEarly.ShouldBeFalse();
        mostInProgress.ShouldBe(2);
        responses.Select(response => response.Span[0]).ShouldBe(new byte[] { 0, 1, 2, 3 });
    }

    /// <summary>
    /// The server refuses a maximum of zero or less requests at the same time, and null arguments
    /// </summary>
    [Fact]
    public void InvalidServerArgumentsAreRefused()
    {
        // Checks
        Should.Throw<ArgumentOutOfRangeException>(() => new RequestServer(new FrameCodec(), EchoAsync, 0));
        Should.Throw<ArgumentNullException>(() => new RequestServer(null!, EchoAsync));
        Should.Throw<ArgumentNullException>(() => new RequestServer(new FrameCodec(), null!));
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
