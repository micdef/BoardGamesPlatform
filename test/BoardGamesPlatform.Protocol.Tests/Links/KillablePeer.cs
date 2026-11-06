/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/KillablePeer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Test peer of a link that can go down in the middle of a session and come back, on the same port
 */

// Using
using System.Collections.Concurrent;
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Peer of a link that the test stops and restarts
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> plays a tier that goes down and comes back. Stopping a real listener and binding the same fixed port again depends on
/// the system releasing it at once, which a test cannot prove; this peer keeps its listener and only changes what it does with the
/// connections.</para>
/// <para><b>Behavior:</b> while it is up, each accepted connection runs the handshake, then is served with a Ping handler. When it goes down,
/// every connection in progress is closed, and each new one is closed as soon as it is accepted, before any byte (as a refused tier looks to
/// its client). It counts the accepted connections, so that a test can prove a client did not touch it.</para>
/// </remarks>
internal sealed class KillablePeer : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the peer, kept for its whole life
    /// </summary>
    private readonly TcpTransportListener _listener;

    /// <summary>
    /// Time the Ping answers
    /// </summary>
    private readonly DateTimeOffset _pingTime;

    /// <summary>
    /// Connections served, guarded by <see cref="_connectionsLock"/>
    /// </summary>
    private readonly List<ITransportConnection> _connections = new List<ITransportConnection>();

    /// <summary>
    /// Errors that ended the services of the connections, kept for a test that would need them
    /// </summary>
    private readonly ConcurrentQueue<Exception> _serviceErrors = new ConcurrentQueue<Exception>();

    /// <summary>
    /// Lock of <see cref="_connections"/>
    /// </summary>
    private readonly Lock _connectionsLock = new Lock();

    /// <summary>
    /// Source canceled when the peer is disposed
    /// </summary>
    private readonly CancellationTokenSource _stopping = new CancellationTokenSource();

    /// <summary>
    /// Completed when a held Ping is received
    /// </summary>
    private readonly TaskCompletionSource _pingHeld = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Accept loop of the peer
    /// </summary>
    private Task _acceptLoop = Task.CompletedTask;

    /// <summary>
    /// Whether the peer serves its connections (1) or closes them (0)
    /// </summary>
    private int _isUp = 1;

    /// <summary>
    /// Whether the next Ping is held until its request is canceled (1) or answered (0)
    /// </summary>
    private int _holdNextPing;

    /// <summary>
    /// Number of connections accepted so far
    /// </summary>
    private int _accepted;

    /// <summary>
    /// Whether each Ping drops its connection without an answer (1), as a tier in a crash loop, or is answered (0)
    /// </summary>
    private int _crashOnPing;

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
    /// Port the peer listens on
    /// </summary>
    public int Port => _listener.LocalEndPoint.Port;

    /// <summary>
    /// Number of connections accepted so far, whatever the state of the peer
    /// </summary>
    public int Accepted => Volatile.Read(ref _accepted);

    /// <summary>
    /// Task that completes when a held Ping is received
    /// </summary>
    public Task PingHeld => _pingHeld.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="listener">[Cannot be null] Started listener</param>
    /// <param name="pingTime">Time the Ping answers</param>
    private KillablePeer(TcpTransportListener listener, DateTimeOffset pingTime)
    {
        // Setting
        _listener = listener;
        _pingTime = pingTime;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to answer a Ping, or to hold it until its request is canceled
    /// </summary>
    /// <param name="request">Ping request</param>
    /// <param name="cancellationToken">Token of the request</param>
    /// <returns>
    /// The time of the peer
    /// </returns>
    private async Task<PingResponse> PingAsync(PingRequest request, CancellationToken cancellationToken)
    {
        // Dropping the connection without an answer, as a tier that dies at its first request after an accepted handshake
        if (Volatile.Read(ref _crashOnPing) == 1)
        {
            await DropConnectionsAsync();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        // Holding the Ping when asked
        if (Interlocked.Exchange(ref _holdNextPing, 0) == 1)
        {
            _pingHeld.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        // Function's return
        return new PingResponse(_pingTime);
    }

    /// <summary>
    /// Method used to accept the connections until the peer is disposed
    /// </summary>
    /// <returns>
    /// A task that completes when the peer is disposed
    /// </returns>
    private async Task AcceptAsync()
    {
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register<PingRequest, PingResponse>(BackOperations.Ping, PingAsync);
        RequestServer server = new RequestServer(new FrameCodec(), dispatcher.HandleAsync);
        List<Task> serving = new List<Task>();
        try
        {
            while (true)
            {
                // Accepting, then serving or closing at once
                ITransportConnection connection = await _listener.AcceptAsync(_stopping.Token);
                Interlocked.Increment(ref _accepted);
                if (Volatile.Read(ref _isUp) == 0)
                {
                    await connection.DisposeAsync();
                    continue;
                }
                lock (_connectionsLock)
                    _connections.Add(connection);
                serving.Add(ServeAsync(server, connection));
            }
        }
        catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
        {
            // The peer is disposed
        }

        // Waiting for the services, which end with their connections
        await Task.WhenAll(serving);
    }

    /// <summary>
    /// Method used to serve one connection until it ends, whatever the reason, then forget and close it
    /// </summary>
    /// <param name="server">[Cannot be null] Request server of the peer</param>
    /// <param name="connection">[Cannot be null] Accepted connection</param>
    /// <returns>
    /// A task that completes when the connection is closed
    /// </returns>
    private async Task ServeAsync(RequestServer server, ITransportConnection connection)
    {
        try
        {
            await ProtocolHandshake.RunServerAsync(connection, server.Codec, server.MaxConcurrentRequests, ProtocolHandshake.DefaultServerTimeout, _stopping.Token);
            await server.ServeAsync(connection, _stopping.Token);
        }
        catch (Exception exception)
        {
            // Keeping the error: a closed connection ends its service with an error that the tests expect, and have nothing to report
            _serviceErrors.Enqueue(exception);
        }
        finally
        {
            lock (_connectionsLock)
                _connections.Remove(connection);
            await connection.DisposeAsync();
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to start a peer on a test port of the block of this project
    /// </summary>
    /// <param name="pingTime">Time the Ping answers</param>
    /// <returns>
    /// The started peer, up, owned by the caller
    /// </returns>
    public static KillablePeer Start(DateTimeOffset pingTime)
    {
        // Starting the listener, then the accept loop
        KillablePeer peer = new KillablePeer(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), pingTime);
        peer._acceptLoop = peer.AcceptAsync();

        // Function's return
        return peer;
    }

    /// <summary>
    /// Method used to hold the next Ping until its request is canceled, or its connection closed
    /// </summary>
    public void HoldNextPing()
    {
        // Setting
        Volatile.Write(ref _holdNextPing, 1);
    }

    /// <summary>
    /// Method used to make each Ping drop its connection without an answer, or answer again
    /// </summary>
    /// <param name="isCrashing">True for a tier in a crash loop (the handshake is accepted, then the connection dies at the first request)</param>
    public void CrashOnPing(bool isCrashing)
    {
        // Setting
        Volatile.Write(ref _crashOnPing, isCrashing ? 1 : 0);
    }

    /// <summary>
    /// Method used to close every connection served, the peer staying up
    /// </summary>
    /// <returns>
    /// A task that completes once the connections are closed
    /// </returns>
    public async Task DropConnectionsAsync()
    {
        // Closing a copy of the list: each service forgets its connection when it ends
        ITransportConnection[] connections;
        lock (_connectionsLock)
            connections = _connections.ToArray();
        foreach (ITransportConnection connection in connections)
            await connection.DisposeAsync();
    }

    /// <summary>
    /// Method used to take the peer down: every connection served is closed, and each new one is closed as soon as it is accepted
    /// </summary>
    /// <returns>
    /// A task that completes once the connections are closed
    /// </returns>
    public Task GoDownAsync()
    {
        // Refusing the new connections, then closing the others
        Volatile.Write(ref _isUp, 0);

        // Function's return
        return DropConnectionsAsync();
    }

    /// <summary>
    /// Method used to bring the peer back: the next connections are served again
    /// </summary>
    public void ComeBack()
    {
        // Setting
        Volatile.Write(ref _isUp, 1);
    }

    /// <summary>
    /// Method used to stop the peer, close its listener and every connection
    /// </summary>
    /// <returns>
    /// A task that completes once the peer is stopped
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Stopping the accept loop and the services, then closing the listener
        await _stopping.CancelAsync();
        await _listener.DisposeAsync();
        await DropConnectionsAsync();
        await _acceptLoop;
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
