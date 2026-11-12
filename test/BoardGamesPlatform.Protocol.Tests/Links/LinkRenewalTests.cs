/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkRenewalTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the renewal of a connection by the link client: a new connection, the requests in flight answered, the old one kept on a failure
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
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check how <see cref="LinkClient"/> replaces a connection that asks for a renewal
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a connection of the secure channel asks for a renewal before the limit of its keys; the link client must replace it
/// without failing any request. The records of the channel are checked elsewhere: here, a connection asks for it when the test says so.</para>
/// </remarks>
public sealed class LinkRenewalTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time a test may run
    /// </summary>
    private const int TestTimeout = 15000;

    #endregion

    #region Variables

    /// <summary>
    /// Logger of the link client
    /// </summary>
    private readonly CapturingLogger<LinkRenewalTests> _logger = new CapturingLogger<LinkRenewalTests>();

    /// <summary>
    /// Released by the test to let the gated pings answer
    /// </summary>
    private readonly TaskCompletionSource _gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Whether the next ping waits for the gate
    /// </summary>
    private volatile bool _isGated;

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
    /// Method used to start a peer that answers the Ping operation, a gated ping waiting for the gate
    /// </summary>
    /// <param name="stopping">[Cannot be null] Token that stops the peer</param>
    /// <param name="running">The task of the peer</param>
    /// <returns>
    /// The port of the peer
    /// </returns>
    private int StartPeer(CancellationToken stopping, out Task running)
    {
        // Listening over TCP, then running the link server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register<PingRequest, PingResponse>(BackOperations.Ping, async (_, _) =>
        {
            if (_isGated)
            {
                _isGated = false;
                await _gate.Task;
            }

            // Function's return
            return new PingResponse(DateTimeOffset.UnixEpoch);
        });
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), "the peer", NullLogger.Instance, TimeSpan.Zero,
            TimeSpan.FromSeconds(5), LinkServer.DefaultMaxConnections);
        running = server.RunAsync(stopping);

        // Function's return
        return listener.LocalEndPoint.Port;
    }

    /// <summary>
    /// Method used to wait until a condition holds, the renewal running in the background
    /// </summary>
    /// <param name="condition">[Cannot be null] Condition</param>
    /// <returns>
    /// A task that completes once the condition holds
    /// </returns>
    /// <exception cref="TimeoutException">If it does not hold within 5 seconds</exception>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        // Checking every few milliseconds
        DateTime end = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > end)
                throw new TimeoutException("The condition did not hold within 5 seconds");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// Method used to send one ping
    /// </summary>
    /// <param name="client">[Cannot be null] Link client</param>
    /// <returns>
    /// The response
    /// </returns>
    private static Task<PingResponse> PingAsync(LinkClient client)
    {
        // Function's return
        return client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), TestContext.Current.CancellationToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A connection that asks for a renewal is replaced: the next call opens a new one, and the old one is closed
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AConnectionAskingForARenewalIsReplacedAsync()
    {
        // Arranging
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int port = StartPeer(stopping.Token, out Task running);
        RenewableConnector connector = new RenewableConnector();
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the peer", connector, _logger))
        {
            // Acting: a first call, then the connection asks for a renewal: the next call starts it in the background, without waiting for it
            await PingAsync(client);
            connector.Opened[0].IsRenewalDue = true;
            await PingAsync(client);
            await WaitUntilAsync(() => connector.Opened.Count == 2);
            await connector.Opened[0].Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            // Checks; the renewal is logged once the old connection ended, by a task of its own: waited for before the client is disposed,
            // since a client disposed first logs the old connection as closed by the disposal
            connector.Opened.Count.ShouldBe(2);
            await PingAsync(client);
            connector.Opened.Count.ShouldBe(2);
            await WaitUntilAsync(() => _logger.Entries.Any(entry => entry.Message.Contains("renewed", StringComparison.Ordinal)));
        }

        // Stopping the peer
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A request in flight on the replaced connection is still answered, and the old connection closes only once it is
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestInFlightOnTheOldConnectionIsAnsweredAsync()
    {
        // Arranging: a ping waiting on the first connection
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int port = StartPeer(stopping.Token, out Task running);
        RenewableConnector connector = new RenewableConnector();
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the peer", connector, _logger))
        {
            await PingAsync(client);
            _isGated = true;
            Task<PingResponse> inFlight = PingAsync(client);

            // Acting: a renewal while the ping waits, then a second ping, the renewal done in the background
            connector.Opened[0].IsRenewalDue = true;
            await PingAsync(client);
            await WaitUntilAsync(() => connector.Opened.Count == 2);
            await PingAsync(client);
            bool closedEarly = connector.Opened[0].Disposed.Task.IsCompleted;
            _gate.SetResult();
            PingResponse answered = await inFlight;

            // Checks
            closedEarly.ShouldBeFalse();
            answered.ServerTime.ShouldBe(DateTimeOffset.UnixEpoch);
            await connector.Opened[0].Disposed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        }

        // Stopping the peer
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// When the new connection cannot be opened, the old one stays in use, and the renewal waits before it is tried again
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailedRenewalKeepsTheOldConnectionAsync()
    {
        // Arranging: every connection after the first one fails
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int port = StartPeer(stopping.Token, out Task running);
        RenewableConnector connector = new RenewableConnector { MaxConnections = 1 };
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the peer", connector, _logger))
        {
            await PingAsync(client);
            connector.Opened[0].IsRenewalDue = true;

            // Acting: the renewal tried in the background fails, the calls going on on the old connection
            await PingAsync(client);
            await WaitUntilAsync(() => connector.Attempts == 2);
            await WaitUntilAsync(() => _logger.Entries.Any(entry => entry.Message.Contains("renewal of the connection", StringComparison.Ordinal)));
            await PingAsync(client);

            // Checks: the renewal was tried once (the next try waits a minute), the old connection still serves
            connector.Attempts.ShouldBe(2);
            connector.Opened[0].Disposed.Task.IsCompleted.ShouldBeFalse();
        }
        _logger.Entries.ShouldContain(entry => entry.Message.Contains("renewal", StringComparison.Ordinal) && entry.Message.Contains("failed", StringComparison.Ordinal));

        // Stopping the peer
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// Disposing the client closes at once a replaced connection still waiting for a request, without waiting for the request timeout
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ADisposedClientClosesItsRetiredConnectionsAtOnceAsync()
    {
        // Arranging: a ping waiting on the first connection, replaced in the background
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int port = StartPeer(stopping.Token, out Task running);
        RenewableConnector connector = new RenewableConnector();
        LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the peer", connector, _logger);
        await PingAsync(client);
        _isGated = true;
        Task<PingResponse> inFlight = PingAsync(client);
        connector.Opened[0].IsRenewalDue = true;
        await PingAsync(client);
        await WaitUntilAsync(() => connector.Opened.Count == 2);
        bool closedBefore = connector.Opened[0].Disposed.Task.IsCompleted;

        // Acting: disposing while the old connection waits for its request
        await client.DisposeAsync();
        bool closedAfter = connector.Opened[0].Disposed.Task.IsCompleted;
        _gate.SetResult();

        // Checks
        closedBefore.ShouldBeFalse();
        closedAfter.ShouldBeTrue();
        connector.Opened[1].Disposed.Task.IsCompleted.ShouldBeTrue();
        await Should.ThrowAsync<Exception>(inFlight);

        // Stopping the peer
        await stopping.CancelAsync();
        await running;
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
    /// Connector over TCP whose connections ask for a renewal when the test says so
    /// </summary>
    private sealed class RenewableConnector : ITransportConnector
    {
        /// <summary>
        /// Connections opened, in their order
        /// </summary>
        public List<RenewableConnection> Opened { get; } = new List<RenewableConnection>();

        /// <summary>
        /// Most connections opened; the next ones fail
        /// </summary>
        public int MaxConnections { get; init; } = int.MaxValue;

        /// <summary>
        /// Number of connections asked
        /// </summary>
        public int Attempts { get; private set; }

        /// <inheritdoc/>
        public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
        {
            // Refusing beyond the maximum, else opening over TCP
            Attempts++;
            if (Opened.Count >= MaxConnections)
                throw new IOException("No more connections in this test");
            RenewableConnection connection = new RenewableConnection(await new TcpTransportConnector().ConnectAsync(host, port, cancellationToken));
            Opened.Add(connection);

            // Function's return
            return connection;
        }
    }

    /// <summary>
    /// Connection over TCP that asks for a renewal when the test says so
    /// </summary>
    /// <param name="inner">TCP connection</param>
    private sealed class RenewableConnection(ITransportConnection inner) : ITransportConnection
    {
        /// <summary>
        /// Completed once the connection is disposed
        /// </summary>
        public TaskCompletionSource Disposed { get; } = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <inheritdoc/>
        public Stream Stream => inner.Stream;

        /// <inheritdoc/>
        public EndPoint RemoteEndPoint => inner.RemoteEndPoint;

        /// <inheritdoc/>
        public string? PeerIdentity => inner.PeerIdentity;

        /// <inheritdoc/>
        public bool IsRenewalDue { get; set; }

        /// <inheritdoc/>
        public Task CloseSendAsync(CancellationToken cancellationToken = default)
        {
            // Function's return
            return inner.CloseSendAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            // Closing, then telling the test
            await inner.DisposeAsync();
            Disposed.TrySetResult();
        }
    }
}
