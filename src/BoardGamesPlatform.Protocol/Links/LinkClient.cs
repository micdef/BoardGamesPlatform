/*
 *	@file	src/BoardGamesPlatform.Protocol/Links/LinkClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link: opens one shared connection on first use, and a new one on the next call once it is lost
 */

// Using
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Links;

/// <summary>
/// Client side of a link (the Gateway to the Back, the Back to the Very Back)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one connection management for every tier that calls another, instead of a copy per tier that could drift; the typed
/// clients of the tiers only name their operations.</para>
/// <para><b>Behavior:</b> the connection opens on the first call, through the connector, within the connect timeout. All the concurrent calls
/// share that one connection; a lock guards only its opening. When the connection has closed (the peer, a failure), the next call opens a new
/// one.</para>
/// <list type="bullet">
/// <item><description>A request that fails is never retried here: the caller sees the error. Retries, back-off, heartbeat and request
/// timeouts come with 0.4.0.</description></item>
/// <item><description>A connection may close between the moment a call finds it open and the moment its request is sent: that call fails
/// with an <see cref="IOException"/>, like any request on a lost connection, and the next call opens a new connection.</description></item>
/// <item><description>A call that waits for a response is stopped only by the token of its caller.</description></item>
/// <item><description>After <see cref="DisposeAsync"/>, every call throws an <see cref="ObjectDisposedException"/>.</description></item>
/// <item><description>The logs tell when a connection opens, is lost or is closed by the disposal, with the address of the peer
/// only.</description></item>
/// </list>
/// </remarks>
public sealed partial class LinkClient : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Longest connect timeout accepted: waiting longer for a tier means it is down, and a far longer value could not be set as a cancellation
    /// deadline (about 49.7 days at most)
    /// </summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Host of the peer
    /// </summary>
    private readonly string _host;

    /// <summary>
    /// Port of the peer
    /// </summary>
    private readonly int _port;

    /// <summary>
    /// Longest time allowed to open a connection
    /// </summary>
    private readonly TimeSpan _connectTimeout;

    /// <summary>
    /// Name of the peer in the logs and the messages, for example "the Very Back"
    /// </summary>
    private readonly string _peerName;

    /// <summary>
    /// Connector used to open the connection (plain TCP in production)
    /// </summary>
    private readonly ITransportConnector _connector;

    /// <summary>
    /// Logger of the tier that calls
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Codec of the link; the peer uses the default maximum length too
    /// </summary>
    private readonly FrameCodec _codec;

    /// <summary>
    /// Lock that lets one call at a time open the connection; it is never disposed, since a call may still wait for it while the client is
    /// disposed, and a semaphore that nobody asked a wait handle from holds no system resource
    /// </summary>
    private readonly SemaphoreSlim _openingLock;

    /// <summary>
    /// Request client of the current connection; null before the first call. It is replaced by the call that finds it closed
    /// </summary>
    private volatile RequestClient? _current;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> has been called (0: no, 1: yes)
    /// </summary>
    private int _isDisposed;

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

    /// <summary>
    /// Constructor; opens nothing, the connection opens on the first call
    /// </summary>
    /// <param name="host">[Cannot be null or blank] Host name or address of the peer</param>
    /// <param name="port">Port of the peer, from 1 to 65535</param>
    /// <param name="connectTimeout">Longest time allowed to open a connection; greater than zero, and at most <see cref="MaxConnectTimeout"/></param>
    /// <param name="peerName">[Cannot be null or blank] Name of the peer in the logs and the messages, for example "the Very Back"</param>
    /// <param name="connector">[Cannot be null] Connector used to open the connection</param>
    /// <param name="logger">[Cannot be null] Logger of the tier that calls</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the host or the name is blank</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the port or the timeout is out of its range</exception>
    public LinkClient(string host, int port, TimeSpan connectTimeout, string peerName, ITransportConnector connector, ILogger logger)
    {
        // Checks
        if (host is null)
            throw new ArgumentNullException(nameof(host), $"The value of the parameter: {nameof(host)} cannot be null");
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException($"The value of the parameter: {nameof(host)} cannot be blank", nameof(host));
        if (port < 1 || port > System.Net.IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port), $"The value of the parameter: {nameof(port)} must be between 1 and {System.Net.IPEndPoint.MaxPort}");
        if (connectTimeout <= TimeSpan.Zero || connectTimeout > MaxConnectTimeout)
            throw new ArgumentOutOfRangeException(nameof(connectTimeout), $"The value of the parameter: {nameof(connectTimeout)} must be greater than zero and at most {MaxConnectTimeout}");
        if (peerName is null)
            throw new ArgumentNullException(nameof(peerName), $"The value of the parameter: {nameof(peerName)} cannot be null");
        if (string.IsNullOrWhiteSpace(peerName))
            throw new ArgumentException($"The value of the parameter: {nameof(peerName)} cannot be blank", nameof(peerName));
        if (connector is null)
            throw new ArgumentNullException(nameof(connector), $"The value of the parameter: {nameof(connector)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting
        _host = host;
        _port = port;
        _connectTimeout = connectTimeout;
        _peerName = peerName;
        _connector = connector;
        _logger = logger;
        _codec = new FrameCodec();
        _openingLock = new SemaphoreSlim(1, 1);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log that a connection to the peer is open
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connection to {Peer} opened ({Host}:{Port})")]
    private partial void LogConnectionOpened(string peer, string host, int port);

    /// <summary>
    /// Method used to log that the connection to the peer is lost
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Connection to {Peer} lost ({Host}:{Port}); the next call opens a new one")]
    private partial void LogConnectionLost(string peer, string host, int port);

    /// <summary>
    /// Method used to log that the peer cannot be reached
    /// </summary>
    /// <param name="exception">Why the connection failed</param>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Connection to {Peer} failed ({Host}:{Port})")]
    private partial void LogConnectionFailed(Exception exception, string peer, string host, int port);

    /// <summary>
    /// Method used to log that closing a lost connection failed
    /// </summary>
    /// <param name="exception">Why the closing failed</param>
    /// <param name="peer">Name of the peer</param>
    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Closing the lost connection to {Peer} failed")]
    private partial void LogCloseFailed(Exception exception, string peer);

    /// <summary>
    /// Method used to log that the connection to the peer is closed because the client is disposed
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "Connection to {Peer} closed by the disposal of the client ({Host}:{Port})")]
    private partial void LogConnectionClosed(string peer, string host, int port);

    /// <summary>
    /// Method used to tell when a connection ends
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> waits for the end of the request client, then always logs it: as a loss, or as a close when the client itself was
    /// disposed (the disposal is marked before the connection is closed).</para>
    /// </remarks>
    /// <param name="client">[Cannot be null] Request client to watch</param>
    /// <returns>
    /// A task that completes when the request client has closed
    /// </returns>
    private async Task WatchConnectionAsync(RequestClient client)
    {
        // Waiting for the end of the connection (the completion never fails)
        await client.Completion.ConfigureAwait(false);

        // Telling the end: a loss, or the disposal of the client
        if (Volatile.Read(ref _isDisposed) == 0)
            LogConnectionLost(_peerName, _host, _port);
        else
            LogConnectionClosed(_peerName, _host, _port);
    }

    /// <summary>
    /// Method used to open a connection within the connect timeout
    /// </summary>
    /// <param name="cancellationToken">Token of the caller</param>
    /// <returns>
    /// The open connection
    /// </returns>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the connection cannot be opened</exception>
    private async Task<ITransportConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        // Combining the token of the caller with the deadline
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_connectTimeout);

        // Connecting; a cancellation that the caller did not ask for is the deadline
        try
        {
            // Function's return
            return await _connector.ConnectAsync(_host, _port, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The connection to {_peerName} was not open within {_connectTimeout}", exception);
        }
    }

    /// <summary>
    /// Method used to get the request client of the current connection, opening a connection when there is none or when it has closed
    /// </summary>
    /// <param name="cancellationToken">Token of the caller</param>
    /// <returns>
    /// A request client that was open when it was returned
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the connection cannot be opened</exception>
    private async Task<RequestClient> GetRequestClientAsync(CancellationToken cancellationToken)
    {
        // Using the open connection, without any lock
        RequestClient? client = _current;
        if (client is not null && !client.Completion.IsCompleted)
            return client;

        // Letting one call at a time open the connection
        await _openingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Checking again: the client may have been disposed, or another call may have opened the connection while this one waited
            ThrowIfDisposed();
            client = _current;
            if (client is not null && !client.Completion.IsCompleted)
                return client;

            // Releasing the lost connection
            if (client is not null)
            {
                try
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogCloseFailed(exception, _peerName);
                }
            }

            // Opening the new connection
            ITransportConnection connection;
            try
            {
                connection = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogConnectionFailed(exception, _peerName, _host, _port);
                throw;
            }
            client = new RequestClient(connection, _codec);
            _current = client;
            LogConnectionOpened(_peerName, _host, _port);
            _ = WatchConnectionAsync(client);

            // Function's return
            return client;
        }
        finally
        {
            _openingLock.Release();
        }
    }

    /// <summary>
    /// Method used to refuse a call once the client is disposed
    /// </summary>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    private void ThrowIfDisposed()
    {
        // Check
        if (Volatile.Read(ref _isDisposed) == 1)
            throw new ObjectDisposedException(nameof(LinkClient), $"The client of {_peerName} has been disposed");
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to send a request of an operation and read its response, on the shared connection (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> opens the connection first when needed; the request is not retried when it fails.</para>
    /// </remarks>
    /// <typeparam name="TRequest">Type of the body of the request</typeparam>
    /// <typeparam name="TResponse">Type of the body of the response</typeparam>
    /// <param name="operation">[Cannot be null] Name of the operation</param>
    /// <param name="request">[Cannot be null] Body of the request</param>
    /// <param name="cancellationToken">Token used to stop waiting, for the connection as for the response</param>
    /// <returns>
    /// The body of the response
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the peer cannot be reached</exception>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="IOException">If the connection closes before the response arrives</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    public async Task<TResponse> SendAsync<TRequest, TResponse>(string operation, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        // Check
        ThrowIfDisposed();

        // Getting the connection, then sending the request
        RequestClient client = await GetRequestClientAsync(cancellationToken).ConfigureAwait(false);

        // Function's return
        return await client.SendAsync<TRequest, TResponse>(operation, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to close the connection; the requests in progress fail with an <see cref="ObjectDisposedException"/>
    /// </summary>
    /// <returns>
    /// A task that completes when the connection is closed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            return;

        // Waiting for a connection being opened, then closing the current one; a call that waited for the lock finds the client disposed
        await _openingLock.WaitAsync().ConfigureAwait(false);
        try
        {
            RequestClient? client = _current;
            _current = null;
            if (client is not null)
                await client.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _openingLock.Release();
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
