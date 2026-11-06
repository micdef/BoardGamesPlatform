/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Correlation/HeartbeatTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the heartbeat: the client pings, the server answers, and each end gives up a peer that sends nothing
 */

// Using
using System.Net;
using System.Text;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Correlation;

/// <summary>
/// Tests used to check the heartbeat of the request client and of the request server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a dead peer or a broken network path must be found within a bounded time, even when no request flows; and a peer that
/// answers must never be taken for dead.</para>
/// <para><b>Behavior:</b> the time of the heartbeat is a fake clock moved by the test, so that no test waits for real seconds. Each test opens
/// a loopback connection on a test port of the block of this project; the other end is a raw frame codec that plays the peer.</para>
/// </remarks>
public sealed class HeartbeatTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    #endregion

    #region Variables

    /// <summary>
    /// Timings of the tests, the defaults of the tiers: a ping every 10 seconds, 30 seconds for the client, 45 for the server
    /// </summary>
    private static readonly HeartbeatSettings _timings = HeartbeatSettings.Default;

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
    /// Method used to read the next frame as a raw peer and check that it is the given heartbeat message
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to read from</param>
    /// <param name="kind">[Cannot be null] Expected kind, ping or pong</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the frame is read and checked
    /// </returns>
    private static async Task ReadHeartbeatAsync(ITransportConnection connection, string kind, CancellationToken cancellationToken)
    {
        // Reading, then checking
        Frame frame = (await new FrameCodec().ReadAsync(connection.Stream, cancellationToken)).ShouldNotBeNull();
        frame.CorrelationId.ShouldBe(Frame.NoCorrelation);
        MessageSerializer.ReadControl(frame.Payload.Span).ShouldBeOfType<HeartbeatMessage>().Control.ShouldBe(kind);
    }

    /// <summary>
    /// Method used to write a heartbeat message as a raw peer
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to write to</param>
    /// <param name="message">[Cannot be null] Ping or pong</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once the frame is written
    /// </returns>
    private static Task WriteHeartbeatAsync(ITransportConnection connection, HeartbeatMessage message, CancellationToken cancellationToken)
    {
        // Function's return
        return new FrameCodec().WriteAsync(connection.Stream, new Frame(Frame.NoCorrelation, MessageSerializer.WriteControl(message)), cancellationToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// The heartbeat messages follow the wire format
    /// </summary>
    [Fact]
    public void TheHeartbeatFollowsTheWireFormat()
    {
        // Checks
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(HeartbeatMessage.Ping)).ShouldBe("{\"control\":\"ping\"}");
        Encoding.UTF8.GetString(MessageSerializer.WriteControl(HeartbeatMessage.Pong)).ShouldBe("{\"control\":\"pong\"}");
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadControl(Encoding.UTF8.GetBytes("{\"control\":\"ping\",\"at\":1}")));
    }

    /// <summary>
    /// The timings refuse an interval that is not positive and timeouts that are not longer than the interval
    /// </summary>
    [Fact]
    public void TheTimingsAreChecked()
    {
        // Checks
        Should.Throw<ArgumentOutOfRangeException>(() => new HeartbeatSettings(TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45)));
        Should.Throw<ArgumentOutOfRangeException>(() => new HeartbeatSettings(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(45)));
        Should.Throw<ArgumentOutOfRangeException>(() => new HeartbeatSettings(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10)));
    }

    /// <summary>
    /// The client pings the server at each interval when nothing else flows
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheClientPingsAtEachIntervalAsync()
    {
        // Opening a client on a fake clock, in front of a raw server
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec(), heartbeat: _timings, timeProvider: clock);

        // Checks: one ping per interval
        clock.Advance(_timings.PingInterval);
        await ReadHeartbeatAsync(raw, ControlKinds.Ping, token);
        clock.Advance(_timings.PingInterval);
        await ReadHeartbeatAsync(raw, ControlKinds.Ping, token);
    }

    /// <summary>
    /// A server that sends nothing back for longer than the client timeout is taken as dead: the client closes and its pending requests fail
    /// with the timeout as the cause
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheClientGivesUpAServerThatSendsNothingAsync()
    {
        // Opening a client on a fake clock, in front of a raw server that never answers, with a request waiting
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec(), heartbeat: _timings, timeProvider: clock);
        Task<ReadOnlyMemory<byte>> pending = client.SendAsync(new byte[] { 1 }, token);
        (await new FrameCodec().ReadAsync(raw.Stream, token)).ShouldNotBeNull();

        // Moving the clock past the timeout
        clock.Advance(_timings.ClientTimeout + TimeSpan.FromSeconds(1));

        // Checks
        await client.Completion.WaitAsync(token);
        IOException failure = await Should.ThrowAsync<IOException>(() => pending);
        failure.InnerException.ShouldBeOfType<TimeoutException>();
    }

    /// <summary>
    /// A server that answers the pings keeps the client open, however long nothing else flows
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task APongKeepsTheClientOpenAsync()
    {
        // Opening a client on a fake clock, in front of a raw server that answers the pings and echoes the requests
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec, heartbeat: _timings, timeProvider: clock);

        // Living 6 intervals (twice the client timeout) on pongs only; after each pong, a request proves the client has read it
        for (int interval = 1; interval <= 6; interval++)
        {
            clock.Advance(_timings.PingInterval);
            await ReadHeartbeatAsync(raw, ControlKinds.Ping, token);
            await WriteHeartbeatAsync(raw, HeartbeatMessage.Pong, token);
            Task<ReadOnlyMemory<byte>> echo = client.SendAsync(new byte[] { (byte)interval }, token);
            Frame request = (await codec.ReadAsync(raw.Stream, token)).ShouldNotBeNull();
            await codec.WriteAsync(raw.Stream, request, token);
            (await echo).ToArray().ShouldBe(new byte[] { (byte)interval });
        }

        // Check
        client.Completion.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// A large request arriving slowly keeps the server serving: any byte received counts, not only a whole frame
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALargeFrameArrivingSlowlyKeepsTheServerServingAsync()
    {
        // Serving a connection on a fake clock with an echo handler, played by a raw client
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ObservedConnection served = new ObservedConnection(serverEnd);
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request), heartbeat: _timings, timeProvider: clock)
            .ServeAsync(served, token);

        // Sending a request in 10 pieces, one per ping interval, each one read by the server before the clock moves: the whole frame takes
        // twice the idle timeout to arrive
        byte[] payload = Enumerable.Range(0, 10000).Select(value => (byte)value).ToArray();
        using MemoryStream framed = new MemoryStream();
        await codec.WriteAsync(framed, new Frame(1, payload), token);
        byte[] bytes = framed.ToArray();
        int pieceLength = (bytes.Length / 10) + 1;
        for (int offset = 0; offset < bytes.Length; offset += pieceLength)
        {
            int length = Math.Min(pieceLength, bytes.Length - offset);
            await raw.Stream.WriteAsync(bytes.AsMemory(offset, length), token);
            await raw.Stream.FlushAsync(token);
            await served.WaitForReadAsync(offset + length, token);
            clock.Advance(_timings.PingInterval);
        }

        // Checks: the request is answered, the service goes on, and closing ends it cleanly
        Frame answered = (await codec.ReadAsync(raw.Stream, token)).ShouldNotBeNull();
        answered.Payload.ToArray().ShouldBe(payload);
        serving.IsCompleted.ShouldBeFalse();
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A large response arriving slowly keeps the client open: any byte received counts, not only a whole frame
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALargeFrameArrivingSlowlyKeepsTheClientOpenAsync()
    {
        // Opening a client on a fake clock, in front of a raw server
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        ObservedConnection observed = new ObservedConnection(clientEnd);
        await using RequestClient client = new RequestClient(observed, codec, heartbeat: _timings, timeProvider: clock);
        Task<ReadOnlyMemory<byte>> pending = client.SendAsync(new byte[] { 1 }, token);
        Frame request = (await codec.ReadAsync(raw.Stream, token)).ShouldNotBeNull();

        // Answering in 10 pieces, one per ping interval, each one read by the client before the clock moves: the whole frame takes more than
        // three times the client timeout to arrive
        byte[] payload = Enumerable.Range(0, 10000).Select(value => (byte)value).ToArray();
        using MemoryStream framed = new MemoryStream();
        await codec.WriteAsync(framed, new Frame(request.CorrelationId, payload), token);
        byte[] bytes = framed.ToArray();
        int pieceLength = (bytes.Length / 10) + 1;
        for (int offset = 0; offset < bytes.Length; offset += pieceLength)
        {
            int length = Math.Min(pieceLength, bytes.Length - offset);
            await raw.Stream.WriteAsync(bytes.AsMemory(offset, length), token);
            await raw.Stream.FlushAsync(token);
            await observed.WaitForReadAsync(offset + length, token);
            clock.Advance(_timings.PingInterval);
        }

        // Checks: the response arrives whole and the client is still open
        (await pending).ToArray().ShouldBe(payload);
        client.Completion.IsCompleted.ShouldBeFalse();
    }

    /// <summary>
    /// A large request written slowly keeps the client open: each piece that leaves counts, and the ping waits for its turn instead of failing
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALargeFrameWrittenSlowlyKeepsTheClientOpenAsync()
    {
        // Opening a client on a fake clock, in front of a raw server that does not read yet; the request (16 MiB) fills the buffers of the
        // system, so that its write stalls until the server reads
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec(FrameCodec.HighestMaxPayloadLength);
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec, heartbeat: _timings, timeProvider: clock);
        byte[] payload = new byte[FrameCodec.HighestMaxPayloadLength];
        Task<ReadOnlyMemory<byte>> pending = client.SendAsync(payload, token);

        // Reading the request 1 MiB per ping interval: the whole write takes more than five times the client timeout. The clock moves only
        // once a whole MiB is read, so that the client has written pieces since the last move (a read of a few bytes must not move it)
        byte[] piece = new byte[1024 * 1024];
        int total = FrameCodec.HeaderLength + payload.Length;
        int received = 0;
        while (received < total)
        {
            received += await raw.Stream.ReadAtLeastAsync(piece, Math.Min(piece.Length, total - received), throwOnEndOfStream: true, token);
            clock.Advance(_timings.PingInterval);
        }

        // Checks: the client is still open, and the answer reaches the request
        client.Completion.IsCompleted.ShouldBeFalse();
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 7 }), token);
        (await pending).ToArray().ShouldBe(new byte[] { 7 });
    }

    /// <summary>
    /// A ping sent by the server is a protocol violation: only the client pings
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task APingFromTheServerIsAViolationAsync()
    {
        // Opening a client in front of a raw server that pings
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());
        Task<ReadOnlyMemory<byte>> pending = client.SendAsync(new byte[] { 1 }, token);
        (await new FrameCodec().ReadAsync(raw.Stream, token)).ShouldNotBeNull();
        await WriteHeartbeatAsync(raw, HeartbeatMessage.Ping, token);

        // Checks
        await client.Completion.WaitAsync(token);
        (await Should.ThrowAsync<IOException>(() => pending)).InnerException.ShouldBeOfType<InvalidDataException>();
    }

    /// <summary>
    /// The server answers each ping with a pong
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServerAnswersAPingWithAPongAsync()
    {
        // Serving a connection, played by a raw client
        CancellationToken token = TestContext.Current.CancellationToken;
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request)).ServeAsync(served, token);

        // Checks: a pong for each ping, and closing ends the service cleanly
        await WriteHeartbeatAsync(raw, HeartbeatMessage.Ping, token);
        await ReadHeartbeatAsync(raw, ControlKinds.Pong, token);
        await WriteHeartbeatAsync(raw, HeartbeatMessage.Ping, token);
        await ReadHeartbeatAsync(raw, ControlKinds.Pong, token);
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A client that sends nothing for longer than the idle timeout is given up by the server, which then frees its slots
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServerGivesUpAnIdleClientAsync()
    {
        // Serving a connection on a fake clock, played by a raw client that stays silent
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request), heartbeat: _timings, timeProvider: clock)
            .ServeAsync(served, token);

        // Moving the clock past the idle timeout
        clock.Advance(_timings.ServerIdleTimeout + TimeSpan.FromSeconds(1));

        // Check
        await Should.ThrowAsync<TimeoutException>(() => serving);
    }

    /// <summary>
    /// A client that pings in time is never given up by the server, however long no request flows
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task APingInTimeKeepsTheServerServingAsync()
    {
        // Serving a connection on a fake clock, played by a raw client that pings at each interval
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request), heartbeat: _timings, timeProvider: clock)
            .ServeAsync(served, token);

        // Living 10 intervals (twice the idle timeout); each pong proves the server has read the ping before the clock moves on
        for (int interval = 1; interval <= 10; interval++)
        {
            await WriteHeartbeatAsync(raw, HeartbeatMessage.Ping, token);
            await ReadHeartbeatAsync(raw, ControlKinds.Pong, token);
            clock.Advance(_timings.PingInterval);
        }

        // Checks: still served, and closing ends the service cleanly
        serving.IsCompleted.ShouldBeFalse();
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A response the client never reads ends the service at the write deadline (L1 of the security review of 0.4.0), even while the client
    /// keeps the connection alive with frames that need no answer
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AWriteWithoutProgressEndsTheServiceAsync()
    {
        // Serving a connection on a fake clock whose writes never go through, as towards a client that stops reading
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        ObservedConnection observed = new ObservedConnection(serverEnd);
        await using GatedWriteConnection served = new GatedWriteConnection(observed);
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request), heartbeat: _timings, timeProvider: clock)
            .ServeAsync(served, token);

        // A request whose response gets stuck, then a cancellation (read by the server) that keeps the connection alive
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 7 }), token);
        await served.WaitForWriteAsync(token);
        byte[] cancel = MessageSerializer.WriteControl(new CancelMessage(99));
        await codec.WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, cancel), token);
        await observed.WaitForReadAsync((FrameCodec.HeaderLength * 2) + 1 + cancel.Length, token);

        // Moving the clock to the deadline of the write (the idle timeout is not passed: something was received at this time)
        clock.Advance(_timings.ServerIdleTimeout);

        // Check
        TimeoutException failure = await Should.ThrowAsync<TimeoutException>(() => serving);
        failure.Message.ShouldContain("read nothing of a frame");
    }

    /// <summary>
    /// A large response read slowly keeps the service: the write deadline moves with each piece written, so the whole frame may take longer
    /// than the deadline
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AWriteThatProgressesSlowlyKeepsTheServiceAsync()
    {
        // Serving a connection on a fake clock whose writes go through one at a time, answering a large response
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        ObservedConnection observed = new ObservedConnection(serverEnd);
        await using GatedWriteConnection served = new GatedWriteConnection(observed);
        byte[] response = Enumerable.Range(0, (3 * FrameCodec.WriteChunkLength) + 100).Select(value => (byte)value).ToArray();
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult<ReadOnlyMemory<byte>>(response), heartbeat: _timings,
            timeProvider: clock).ServeAsync(served, token);

        // Sending the request, and reading the response while it is written
        await codec.WriteAsync(raw.Stream, new Frame(1, new byte[] { 7 }), token);
        long received = FrameCodec.HeaderLength + 1;
        Task<Frame?> reading = codec.ReadAsync(raw.Stream, token);

        // Letting each of the 4 pieces through after almost the deadline, a cancellation keeping the connection alive in between: the whole
        // response takes more than 3 times the deadline
        byte[] cancel = MessageSerializer.WriteControl(new CancelMessage(99));
        for (int piece = 0; piece < 4; piece++)
        {
            await served.WaitForWriteAsync(token);
            await codec.WriteAsync(raw.Stream, new Frame(Frame.NoCorrelation, cancel), token);
            received += FrameCodec.HeaderLength + cancel.Length;
            await observed.WaitForReadAsync(received, token);
            clock.Advance(_timings.ServerIdleTimeout - TimeSpan.FromSeconds(1));
            served.AllowWrite();
        }

        // Checks: the response is whole, the service goes on, and closing ends it cleanly
        Frame answered = (await reading).ShouldNotBeNull();
        answered.Payload.ToArray().ShouldBe(response);
        serving.IsCompleted.ShouldBeFalse();
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A server that reads everything but never answers is given up by the client, even while short requests leave at every interval: a
    /// write that completes is not a sign of life
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ShortRequestsNeverKeepASilentServerAliveAsync()
    {
        // Opening a client on a fake clock, in front of a raw server that reads every frame and answers none
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec, heartbeat: _timings, timeProvider: clock);
        using SemaphoreSlim requestRead = new SemaphoreSlim(0);
        Task draining = Task.Run(async () =>
        {
            try
            {
                while (await codec.ReadAsync(raw.Stream, token) is Frame frame)
                {
                    if (frame.CorrelationId != Frame.NoCorrelation)
                        requestRead.Release();
                }
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or OperationCanceledException)
            {
                // The client closed
            }
        }, token);

        // Sending a short request at every interval, each one read by the server before the clock moves, then given up by its caller
        int intervals = (int)(_timings.ClientTimeout / _timings.PingInterval) + 3;
        for (int interval = 0; interval < intervals && !client.Completion.IsCompleted; interval++)
        {
            using CancellationTokenSource caller = new CancellationTokenSource();
            Task<ReadOnlyMemory<byte>> pending = client.SendAsync(new byte[] { 7 }, caller.Token);
            await requestRead.WaitAsync(token);
            clock.Advance(_timings.PingInterval);
            await caller.CancelAsync();
            await Should.ThrowAsync<Exception>(() => pending);
        }

        // Checks: the client closed at its timeout, the requests notwithstanding
        await client.Completion.WaitAsync(token);
        client.CloseReason.ShouldBeOfType<TimeoutException>();
        raw.Stream.Close();
        await draining;
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
