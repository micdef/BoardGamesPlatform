/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkHardeningTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the behaviors hardened after the review of 0.4.0: timeouts, back-off ceiling, heartbeat on any frame, bounded drain
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check the behaviors of the links hardened after the review of 0.4.0
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves what the review found unproven: the request timeout covers the wait for the connection, the back-off never
/// waits longer than its ceiling, any frame keeps a connection alive (not only the pings and pongs), and a server never stays held by a
/// client that closed its sending side.</para>
/// <para><b>Behavior:</b> the clocks are fake clocks moved by the tests; the sockets are loopback sockets on test ports of the block of this
/// project.</para>
/// </remarks>
public sealed class LinkHardeningTests
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
    /// Time the Ping of the peer answers
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 11, 3, 2, 0, 0, TimeSpan.FromHours(1));

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

    #endregion

    #region Publics

    /// <summary>
    /// The request timeout covers the wait for the connection: a peer that never completes the connection gives "link.timeout" within the
    /// request timeout, even when the connect timeout is longer
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheRequestTimeoutCoversTheWaitForTheConnectionAsync()
    {
        // Building a client whose connector never connects, with a connect timeout longer than its request timeout
        CancellationToken token = TestContext.Current.CancellationToken;
        await using LinkClient client = new LinkClient("127.0.0.1", 7822, TimeSpan.FromMinutes(1), TimeSpan.FromMilliseconds(300), "the Very Back",
            new NeverConnectingConnector(), NullLogger.Instance);

        // Check
        LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(
            () => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));
        failure.Code.ShouldBe(ErrorCodes.LinkTimeout);
    }

    /// <summary>
    /// However many failures in a row, the back-off never waits longer than its ceiling of 30 seconds, the random spread included
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheBackOffNeverWaitsLongerThanItsCeilingAsync()
    {
        // Starting a peer that is down, and a client on a fake clock
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        await using KillablePeer peer = KillablePeer.Start(_pingTime);
        await peer.GoDownAsync();
        await using LinkClient client = new LinkClient("127.0.0.1", peer.Port, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), "the Very Back",
            new TcpTransportConnector(), NullLogger.Instance, clock);

        // Failing 10 times in a row; after each failure, the ceiling is always enough for the next call to reach the peer again
        for (int failure = 1; failure <= 10; failure++)
        {
            await Should.ThrowAsync<LinkFailureException>(() => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));
            peer.Accepted.ShouldBe(failure, $"failure {failure}");
            clock.Advance(LinkClient.MaxBackOff + TimeSpan.FromMilliseconds(1));
        }
    }

    /// <summary>
    /// Any frame received keeps the client open: responses alone, without any pong, are enough
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ResponsesAloneKeepTheClientOpenAsync()
    {
        // Opening a client on a fake clock, in front of a raw server that answers the requests and never the pings
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = serverEnd;
        await using RequestClient client = new RequestClient(clientEnd, codec, heartbeat: HeartbeatSettings.Default, timeProvider: clock);

        // Living 6 intervals (twice the client timeout) on responses only
        for (int interval = 1; interval <= 6; interval++)
        {
            clock.Advance(HeartbeatSettings.Default.PingInterval);
            Task<ReadOnlyMemory<byte>> echo = client.SendAsync(new byte[] { (byte)interval }, token);
            Frame request;
            do
                request = (await codec.ReadAsync(raw.Stream, token)).ShouldNotBeNull();
            while (request.CorrelationId == Frame.NoCorrelation);
            await codec.WriteAsync(raw.Stream, request, token);
            (await echo).ToArray().ShouldBe(new byte[] { (byte)interval });
        }

        // Check
        client.IsOpen.ShouldBeTrue();
    }

    /// <summary>
    /// Any frame received keeps the server serving: requests alone, without any ping, are enough
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task RequestsAloneKeepTheServerServingAsync()
    {
        // Serving a connection on a fake clock, played by a raw client that sends requests and never pings
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        FrameCodec codec = new FrameCodec();
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request), heartbeat: HeartbeatSettings.Default,
            timeProvider: clock).ServeAsync(served, token);

        // Living 10 intervals (twice the idle timeout) on requests only; each response proves the server read the request in time
        for (ulong interval = 1; interval <= 10; interval++)
        {
            await codec.WriteAsync(raw.Stream, new Frame(interval, new byte[] { 7 }), token);
            (await codec.ReadAsync(raw.Stream, token)).ShouldNotBeNull().CorrelationId.ShouldBe(interval);
            clock.Advance(HeartbeatSettings.Default.PingInterval);
        }

        // Checks: still served, and closing ends the service cleanly
        serving.IsCompleted.ShouldBeFalse();
        raw.Stream.Close();
        await serving;
    }

    /// <summary>
    /// A client that closes its sending side while a request runs does not hold the server: the request is canceled once the idle timeout
    /// has passed, and the service reports it
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AClosedClientDoesNotHoldTheServerAsync()
    {
        // Serving a connection on a fake clock with a handler that waits for its token, then closing the sending side of the client
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        TaskCompletionSource started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<ReadOnlyMemory<byte>> WaitingAsync(ReadOnlyMemory<byte> request, CancellationToken requestToken)
        {
            started.TrySetResult();
            try
            {
                await Task.Delay(Timeout.Infinite, requestToken);
            }
            catch (OperationCanceledException)
            {
                canceled.TrySetResult();
                throw;
            }
            return request;
        }
        (ITransportConnection clientEnd, ITransportConnection serverEnd) = await ConnectAsync(token);
        await using ITransportConnection raw = clientEnd;
        await using ITransportConnection served = serverEnd;
        Task serving = new RequestServer(new FrameCodec(), WaitingAsync, heartbeat: HeartbeatSettings.Default, timeProvider: clock).ServeAsync(served, token);
        await new FrameCodec().WriteAsync(raw.Stream, new Frame(1, new byte[] { 1 }), token);
        await started.Task.WaitAsync(token);
        await raw.CloseSendAsync(token);

        // Letting the server read the end of the stream, then moving the clock past the idle timeout. The only fixed wait of this class: no
        // signal tells that the server has read the end (it takes milliseconds); moved too early, the clock would end the service through the
        // idle watch instead, with a TimeoutException, and the last check would fail
        await Task.Delay(500, token);
        clock.Advance(HeartbeatSettings.Default.ServerIdleTimeout + TimeSpan.FromSeconds(1));

        // Checks: the handler is canceled, and the service reports the requests it had to cancel, so that the link server logs them
        await canceled.Task.WaitAsync(token);
        await Should.ThrowAsync<TimeoutException>(() => serving);
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

    /// <summary>
    /// Connector that never completes a connection, as a peer that drops the packets of the TCP handshake
    /// </summary>
    private sealed class NeverConnectingConnector : ITransportConnector
    {
        #region Members

        #region Constants

        // #N/A

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
        /// Method used to wait until the token is canceled, without ever connecting
        /// </summary>
        /// <param name="host">Host of the peer (unused)</param>
        /// <param name="port">Port of the peer (unused)</param>
        /// <param name="cancellationToken">Token that ends the wait</param>
        /// <returns>
        /// A task that never completes with a connection
        /// </returns>
        /// <exception cref="OperationCanceledException">When the token is canceled</exception>
        public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken)
        {
            // Waiting for the cancellation
            await Task.Delay(Timeout.Infinite, cancellationToken);

            // Function's return (never reached)
            throw new InvalidOperationException("The connector never connects");
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
}
