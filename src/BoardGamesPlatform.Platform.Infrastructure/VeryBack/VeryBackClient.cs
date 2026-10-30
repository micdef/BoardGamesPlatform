/*
 *	@file	src/BoardGamesPlatform.Platform.Infrastructure/VeryBack/VeryBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client of the Very Back used by the Back: opens the link on first use and calls its operations
 */

// Using
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.VeryBack;

/// <summary>
/// Client used by the Back to call the operations of the Very Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> gives the Back one place to reach the Very Back, hiding the connection, the framing and the messages.</para>
/// <para><b>Behavior:</b> the connection opens on the first call, through the connector, within the connect timeout of the options. All the
/// concurrent calls share that one connection; a lock guards only its opening. When the connection has closed (the peer, a failure), the next
/// call opens a new one.</para>
/// <list type="bullet">
/// <item><description>A request that fails is never retried here: the caller sees the error. Retries, back-off, heartbeat and request
/// timeouts come with 0.4.0.</description></item>
/// <item><description>A call that waits for a response is stopped only by the token of its caller.</description></item>
/// <item><description>After <see cref="DisposeAsync"/>, every call throws an <see cref="ObjectDisposedException"/>.</description></item>
/// <item><description>The logs tell when a connection opens or is lost, with the address of the Very Back only.</description></item>
/// </list>
/// </remarks>
public sealed partial class VeryBackClient : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Settings of the link, validated at construction
    /// </summary>
    private readonly VeryBackClientOptions _options;

    /// <summary>
    /// Connector used to open the connection (plain TCP in production)
    /// </summary>
    private readonly ITransportConnector _connector;

    /// <summary>
    /// Logger of the client
    /// </summary>
    private readonly ILogger<VeryBackClient> _logger;

    /// <summary>
    /// Codec of the link; the Very Back uses the default maximum length too
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
    /// <param name="options">[Cannot be null] Settings of the link; checked here, so that a wrong setting stops the Back at startup</param>
    /// <param name="connector">[Cannot be null] Connector used to open the connection</param>
    /// <param name="logger">[Cannot be null] Logger of the client</param>
    /// <exception cref="ArgumentNullException">If the options, the connector or the logger is null</exception>
    /// <exception cref="InvalidOperationException">If a setting is invalid</exception>
    public VeryBackClient(VeryBackClientOptions options, ITransportConnector connector, ILogger<VeryBackClient> logger)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (connector is null)
            throw new ArgumentNullException(nameof(connector), $"The value of the parameter: {nameof(connector)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        options.Validate();

        // Setting
        _options = options;
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
    /// Method used to log that a connection to the Very Back is open
    /// </summary>
    /// <param name="host">Host of the Very Back</param>
    /// <param name="port">Port of the Very Back</param>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Connection to the Very Back opened ({Host}:{Port})")]
    private partial void LogConnectionOpened(string host, int port);

    /// <summary>
    /// Method used to log that the connection to the Very Back is lost
    /// </summary>
    /// <param name="host">Host of the Very Back</param>
    /// <param name="port">Port of the Very Back</param>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "Connection to the Very Back lost ({Host}:{Port}); the next call opens a new one")]
    private partial void LogConnectionLost(string host, int port);

    /// <summary>
    /// Method used to log that the Very Back cannot be reached
    /// </summary>
    /// <param name="exception">Why the connection failed</param>
    /// <param name="host">Host of the Very Back</param>
    /// <param name="port">Port of the Very Back</param>
    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "Connection to the Very Back failed ({Host}:{Port})")]
    private partial void LogConnectionFailed(Exception exception, string host, int port);

    /// <summary>
    /// Method used to log that closing a lost connection failed
    /// </summary>
    /// <param name="exception">Why the closing failed</param>
    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Closing the lost connection to the Very Back failed")]
    private partial void LogCloseFailed(Exception exception);

    /// <summary>
    /// Method used to tell when a connection is lost
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> waits for the end of the request client, then logs the loss, unless the client itself was disposed.</para>
    /// </remarks>
    /// <param name="client">[Cannot be null] Request client to watch</param>
    /// <returns>
    /// A task that completes when the request client has closed
    /// </returns>
    private async Task WatchConnectionAsync(RequestClient client)
    {
        // Waiting for the end of the connection (the completion never fails)
        await client.Completion.ConfigureAwait(false);

        // Telling the loss, unless it is the disposal of the client
        if (Volatile.Read(ref _isDisposed) == 0)
            LogConnectionLost(_options.Host, _options.Port);
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
        deadline.CancelAfter(_options.ConnectTimeout);

        // Connecting; a cancellation that the caller did not ask for is the deadline
        try
        {
            // Function's return
            return await _connector.ConnectAsync(_options.Host, _options.Port, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The connection to the Very Back was not open within {_options.ConnectTimeout}", exception);
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
                    LogCloseFailed(exception);
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
                LogConnectionFailed(exception, _options.Host, _options.Port);
                throw;
            }
            client = new RequestClient(connection, _codec);
            _current = client;
            LogConnectionOpened(_options.Host, _options.Port);
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
            throw new ObjectDisposedException(nameof(VeryBackClient), "The Very Back client has been disposed");
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to ask the Very Back whether the chain of tiers answers (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves that the Back reaches the Very Back, and the Very Back its database.</para>
    /// <para><b>Behavior:</b> sends the Ping operation on the shared connection, opening it first when needed. The request is not retried
    /// when it fails.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to stop waiting, for the connection as for the response</param>
    /// <returns>
    /// The time of the SQL Server, as answered by the Very Back
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the Very Back cannot be reached</exception>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="IOException">If the connection closes before the response arrives</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken = default)
    {
        // Check
        ThrowIfDisposed();

        // Getting the connection, then sending the request
        RequestClient client = await GetRequestClientAsync(cancellationToken).ConfigureAwait(false);

        // Function's return
        return await client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken).ConfigureAwait(false);
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
