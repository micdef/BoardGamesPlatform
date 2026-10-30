/*
 *	@file	test/BoardGamesPlatform.Platform.Infrastructure.Tests/VeryBack/FakeVeryBack.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Very Back listening on a loopback socket, which answers the Ping operation
 */

// Using
using System.Collections.Concurrent;
using System.Net;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.Tests.VeryBack;

/// <summary>
/// Fake Very Back used by the tests of the Very Back client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> gives the client a real peer on a real socket, whose behavior the test controls and observes.</para>
/// <para><b>Behavior:</b> listens on the loopback address, on a fixed test port of the block of this project (each fake has its own, so that tests never collide), serves
/// every accepted connection with the real <see cref="RequestServer"/> and answers Ping with <see cref="PingTime"/>. It counts the connections
/// it accepted and the requests it received, can drop every connection, and can make the handler fail.</para>
/// </remarks>
internal sealed class FakeVeryBack : IAsyncDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Time answered to every Ping
    /// </summary>
    public static readonly DateTimeOffset PingTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the fake Very Back
    /// </summary>
    private readonly TcpTransportListener _listener;

    /// <summary>
    /// Source canceled when the fake Very Back stops
    /// </summary>
    private readonly CancellationTokenSource _stopping;

    /// <summary>
    /// Dispatcher that routes the requests to their handler
    /// </summary>
    private readonly OperationDispatcher _dispatcher;

    /// <summary>
    /// Connections accepted and not yet dropped, on the server side
    /// </summary>
    private readonly ConcurrentDictionary<ITransportConnection, byte> _connections;

    /// <summary>
    /// Tasks that serve the accepted connections
    /// </summary>
    private readonly ConcurrentBag<Task> _serving;

    /// <summary>
    /// Errors that ended the service of a connection or the accepting, kept for a debugger; the tests expect them (dropped connections)
    /// </summary>
    private readonly ConcurrentQueue<Exception> _serviceErrors;

    /// <summary>
    /// Task that accepts the connections
    /// </summary>
    private Task _accepting;

    /// <summary>
    /// Number of connections accepted
    /// </summary>
    private int _acceptedConnections;

    /// <summary>
    /// Number of requests received
    /// </summary>
    private int _receivedRequests;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> has been called (0: no, 1: yes)
    /// </summary>
    private int _isDisposed;

    /// <summary>
    /// Whether the handler fails (0: no, 1: yes)
    /// </summary>
    private int _failRequests;

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

    /// <summary>
    /// Port the fake Very Back listens on
    /// </summary>
    public int Port => _listener.LocalEndPoint.Port;

    /// <summary>
    /// Number of connections accepted so far
    /// </summary>
    public int AcceptedConnections => Volatile.Read(ref _acceptedConnections);

    /// <summary>
    /// Number of requests received so far
    /// </summary>
    public int ReceivedRequests => Volatile.Read(ref _receivedRequests);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; use <see cref="Start"/>
    /// </summary>
    private FakeVeryBack()
    {
        // Setting
        _listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        _stopping = new CancellationTokenSource();
        _dispatcher = new OperationDispatcher()
            .Register<PingRequest, PingResponse>(VeryBackOperations.Ping, (request, cancellationToken) => Task.FromResult(new PingResponse(PingTime)));
        _connections = new ConcurrentDictionary<ITransportConnection, byte>();
        _serving = new ConcurrentBag<Task>();
        _serviceErrors = new ConcurrentQueue<Exception>();
        _accepting = Task.CompletedTask;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to answer a request, or to fail when the test asked for it
    /// </summary>
    /// <param name="request">Content of the request</param>
    /// <param name="cancellationToken">Token canceled when the service of the connection ends</param>
    /// <returns>
    /// The content of the response
    /// </returns>
    /// <exception cref="InvalidOperationException">If the test made the handler fail</exception>
    private async Task<ReadOnlyMemory<byte>> HandleAsync(ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        // Counting the request
        Interlocked.Increment(ref _receivedRequests);

        // Failing on demand
        if (Volatile.Read(ref _failRequests) == 1)
            throw new InvalidOperationException("The fake Very Back was asked to fail");

        // Function's return
        return await _dispatcher.HandleAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to serve one accepted connection until it ends, however it ends
    /// </summary>
    /// <param name="connection">[Cannot be null] Accepted connection</param>
    /// <returns>
    /// A task that completes when the connection is served and disposed
    /// </returns>
    private async Task ServeAsync(ITransportConnection connection)
    {
        // Serving; a connection dropped by the test, or a failing handler, ends the service with an error that the test expects
        try
        {
            RequestServer server = new RequestServer(new FrameCodec(), HandleAsync);
            await server.ServeAsync(connection, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Keeping the error: the client observes the closed connection, the test has nothing to report
            _serviceErrors.Enqueue(exception);
        }
        finally
        {
            _connections.TryRemove(connection, out _);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Method used to accept the connections until the fake Very Back stops
    /// </summary>
    /// <returns>
    /// A task that completes when the fake Very Back stops
    /// </returns>
    private async Task AcceptAsync()
    {
        // Accepting until the listener is disposed or the token canceled
        try
        {
            while (true)
            {
                ITransportConnection connection = await _listener.AcceptAsync(_stopping.Token).ConfigureAwait(false);
                Interlocked.Increment(ref _acceptedConnections);
                _connections[connection] = 0;
                _serving.Add(ServeAsync(connection));
            }
        }
        catch (Exception exception)
        {
            // The fake Very Back stops (listener disposed or token canceled)
            _serviceErrors.Enqueue(exception);
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to start a fake Very Back on a free loopback port
    /// </summary>
    /// <returns>
    /// The fake Very Back, already listening
    /// </returns>
    public static FakeVeryBack Start()
    {
        // Starting to accept
        FakeVeryBack fake = new FakeVeryBack();
        fake._accepting = fake.AcceptAsync();

        // Function's return
        return fake;
    }

    /// <summary>
    /// Method used to make every later request fail in the handler, which ends the service of its connection
    /// </summary>
    public void FailRequests()
    {
        // Setting
        Volatile.Write(ref _failRequests, 1);
    }

    /// <summary>
    /// Method used to close every connection on the server side, as a crash of the Very Back would
    /// </summary>
    /// <returns>
    /// A task that completes when the connections are disposed
    /// </returns>
    public async Task DropConnectionsAsync()
    {
        // Disposing the connections; their service ends and removes them
        foreach (ITransportConnection connection in _connections.Keys)
            await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to stop the fake Very Back and wait for everything it started
    /// </summary>
    /// <returns>
    /// A task that completes when the listener is stopped and every connection served
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once (a test may stop the fake itself, then leave its "await using")
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            return;

        // Stopping the listener and the services
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _listener.DisposeAsync().ConfigureAwait(false);
        await DropConnectionsAsync().ConfigureAwait(false);

        // Waiting for the tasks started
        await _accepting.ConfigureAwait(false);
        await Task.WhenAll(_serving).ConfigureAwait(false);
        _stopping.Dispose();
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
