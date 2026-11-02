/*
 *	@file	src/BoardGamesPlatform.Protocol/Links/LinkClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link: opens one shared connection on first use, and a new one on the next call once it is lost
 */

// Using
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
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
/// <para><b>Behavior:</b> the connection opens on the first call, through the connector, and its handshake runs
/// (<see cref="ProtocolHandshake"/>), both within the connect timeout. All the concurrent calls
/// share that one connection; a lock guards only its opening. When the connection has closed (the peer, a failure), the next call opens a new
/// one.</para>
/// <list type="bullet">
/// <item><description>A request that fails is never retried here: the caller sees the error. A peer that cannot be reached, or a connection
/// lost before the response, is a <see cref="LinkFailureException"/> with the code <see cref="ErrorCodes.LinkUnavailable"/>, which a tier
/// answers in turn to its own caller; an error code answered by the peer is a <see cref="RemoteErrorException"/>, the connection staying
/// open.</description></item>
/// <item><description>A connection may close between the moment a call finds it open and the moment its request is sent: that call fails
/// like any request on a lost connection, and the next call opens a new connection.</description></item>
/// <item><description>Reconnection with back-off: a lost connection is opened again by the next call, at once. When opening fails (the peer
/// is down, refuses, or breaks the handshake), the next attempt waits a back-off delay: <see cref="FirstBackOff"/>, doubled after each failure
/// up to <see cref="MaxBackOff"/>, with a random spread of 20 % either way so that several clients do not retry in step. A call made during
/// the delay fails at once with <see cref="ErrorCodes.LinkUnavailable"/>, without touching the network; the first connection that opens
/// resets the delay. Nothing waits in a queue: a request is either sent or failed, and never retried automatically, since the peer may already
/// have handled it.</description></item>
/// <item><description>A call is stopped by the token of its caller, or by the request timeout, which covers the whole call: the wait for
/// the connection as well as the wait for the response.</description></item>
/// <item><description>After <see cref="DisposeAsync"/>, every call throws an <see cref="ObjectDisposedException"/>.</description></item>
/// <item><description>The logs tell when a connection opens, is lost or is closed by the disposal, with the address of the peer only; they
/// go through the logger of the tier, with the event ids 9001 to 9006, reserved for this class (see the coding conventions).</description></item>
/// </list>
/// </remarks>
public sealed partial class LinkClient : IAsyncDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest connect timeout accepted: waiting longer for a tier means it is down, and a far longer value could not be set as a cancellation
    /// deadline (about 49.7 days at most)
    /// </summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Longest request timeout accepted, for the same reasons as <see cref="MaxConnectTimeout"/>
    /// </summary>
    public static readonly TimeSpan MaxRequestTimeout = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Request timeout used when none is given
    /// </summary>
    public static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Back-off delay after a first failure to open a connection
    /// </summary>
    public static readonly TimeSpan FirstBackOff = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Longest back-off delay between two attempts to open a connection
    /// </summary>
    public static readonly TimeSpan MaxBackOff = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Most requests in progress this client ever keeps at the same time, whatever the server announces in the handshake
    /// </summary>
    public const int MaxConcurrentRequests = 64;

    #endregion

    #region Variables

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
    /// Longest time a request waits for its response once sent
    /// </summary>
    private readonly TimeSpan _requestTimeout;

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

    /// <summary>
    /// Clock of the back-off (the system clock outside the tests)
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Number of failures in a row to open a connection; read and written under the opening lock only
    /// </summary>
    private int _openingFailures;

    /// <summary>
    /// Time stamp of the clock of the failure that started the back-off; read and written under the opening lock only
    /// </summary>
    private long _backOffStart;

    /// <summary>
    /// Delay before a new attempt to open a connection, counted from <see cref="_backOffStart"/>; zero when no back-off runs; read and written
    /// under the opening lock only
    /// </summary>
    private TimeSpan _backOffDelay;

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
        : this(host, port, connectTimeout, DefaultRequestTimeout, peerName, connector, logger)
    {
    }

    /// <summary>
    /// Constructor with the request timeout; opens nothing, the connection opens on the first call
    /// </summary>
    /// <param name="host">[Cannot be null or blank] Host name or address of the peer</param>
    /// <param name="port">Port of the peer, from 1 to 65535</param>
    /// <param name="connectTimeout">Longest time allowed to open a connection; greater than zero, and at most <see cref="MaxConnectTimeout"/></param>
    /// <param name="requestTimeout">Longest time a request waits for its response; greater than zero, and at most <see cref="MaxRequestTimeout"/></param>
    /// <param name="peerName">[Cannot be null or blank] Name of the peer in the logs and the messages, for example "the Very Back"</param>
    /// <param name="connector">[Cannot be null] Connector used to open the connection</param>
    /// <param name="logger">[Cannot be null] Logger of the tier that calls</param>
    /// <param name="timeProvider">[Can be null] Clock of the back-off; null for the system clock</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the host or the name is blank</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the port or a timeout is out of its range</exception>
    public LinkClient(string host, int port, TimeSpan connectTimeout, TimeSpan requestTimeout, string peerName, ITransportConnector connector, ILogger logger,
        TimeProvider? timeProvider = null)
    {
        // Checks
        if (host is null)
            throw new ArgumentNullException(nameof(host), $"The value of the parameter: {nameof(host)} cannot be null");
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException($"The value of the parameter: {nameof(host)} cannot be blank", nameof(host));
        if (port < 1 || port > System.Net.IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port), $"The value of the parameter: {nameof(port)} must be between 1 and {System.Net.IPEndPoint.MaxPort}");
        if (requestTimeout <= TimeSpan.Zero || requestTimeout > MaxRequestTimeout)
            throw new ArgumentOutOfRangeException(nameof(requestTimeout), $"The value of the parameter: {nameof(requestTimeout)} must be greater than zero and at most {MaxRequestTimeout}");
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
        _requestTimeout = requestTimeout;
        _peerName = peerName;
        _connector = connector;
        _logger = logger;
        _codec = new FrameCodec();
        _openingLock = new SemaphoreSlim(1, 1);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _backOffDelay = TimeSpan.Zero;
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
    [LoggerMessage(EventId = 9001, Level = LogLevel.Information, Message = "Connection to {Peer} opened ({Host}:{Port})")]
    private partial void LogConnectionOpened(string peer, string host, int port);

    /// <summary>
    /// Method used to log that the connection to the peer is lost
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    /// <param name="reason">[Can be null] Why the connection closed</param>
    [LoggerMessage(EventId = 9002, Level = LogLevel.Warning, Message = "Connection to {Peer} lost ({Host}:{Port}); the next call opens a new one")]
    private partial void LogConnectionLost(Exception? reason, string peer, string host, int port);

    /// <summary>
    /// Method used to log that the peer cannot be reached
    /// </summary>
    /// <param name="exception">Why the connection failed</param>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 9003, Level = LogLevel.Warning, Message = "Connection to {Peer} failed ({Host}:{Port})")]
    private partial void LogConnectionFailed(Exception exception, string peer, string host, int port);

    /// <summary>
    /// Method used to log that closing a lost connection failed
    /// </summary>
    /// <param name="exception">Why the closing failed</param>
    /// <param name="peer">Name of the peer</param>
    [LoggerMessage(EventId = 9004, Level = LogLevel.Warning, Message = "Closing the lost connection to {Peer} failed")]
    private partial void LogCloseFailed(Exception exception, string peer);

    /// <summary>
    /// Method used to log that the connection to the peer is closed because the client is disposed
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="host">Host of the peer</param>
    /// <param name="port">Port of the peer</param>
    [LoggerMessage(EventId = 9005, Level = LogLevel.Information, Message = "Connection to {Peer} closed by the disposal of the client ({Host}:{Port})")]
    private partial void LogConnectionClosed(string peer, string host, int port);

    /// <summary>
    /// Method used to log the back-off after a failure to open a connection
    /// </summary>
    /// <param name="peer">Name of the peer</param>
    /// <param name="failures">Number of failures in a row</param>
    /// <param name="delay">Time before the next attempt</param>
    [LoggerMessage(EventId = 9006, Level = LogLevel.Information, Message = "Next connection attempt to {Peer} in {Delay} ({Failures} failures in a row); calls fail at once until then")]
    private partial void LogBackOff(string peer, int failures, TimeSpan delay);

    /// <summary>
    /// Method used to compute the back-off delay after a number of failures in a row
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> <see cref="FirstBackOff"/> doubled for each failure after the first one, spread at random by 20 % either way,
    /// and never more than <see cref="MaxBackOff"/>.</para>
    /// </remarks>
    /// <param name="failures">Number of failures in a row; 1 or more</param>
    /// <returns>
    /// The delay before the next attempt
    /// </returns>
    private static TimeSpan ComputeBackOff(int failures)
    {
        // Doubling the first delay for each failure, without overflowing, then spreading it from 80 % to 120 %
        double doublings = Math.Min(failures - 1, 16);
        double delay = FirstBackOff.TotalMilliseconds * Math.Pow(2, doublings) * (0.8 + (0.4 * Random.Shared.NextDouble()));

        // Function's return: never more than the maximum
        return TimeSpan.FromMilliseconds(Math.Min(delay, MaxBackOff.TotalMilliseconds));
    }

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

        // Telling the end: a loss with its cause, or the disposal of the client
        if (Volatile.Read(ref _isDisposed) == 0)
            LogConnectionLost(client.CloseReason, _peerName, _host, _port);
        else
            LogConnectionClosed(_peerName, _host, _port);
    }

    /// <summary>
    /// Method used to open a connection and run its handshake, both within the connect timeout
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> a connection whose handshake fails is closed before the exception is thrown.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token of the caller</param>
    /// <returns>
    /// The open connection, and the agreement reached by its handshake
    /// </returns>
    /// <exception cref="TimeoutException">If the connection is not open, or its handshake not ended, within the connect timeout</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the connection cannot be opened</exception>
    /// <exception cref="RemoteErrorException">If the peer refuses the handshake</exception>
    /// <exception cref="InvalidDataException">If the peer breaks the handshake</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    private async Task<(ITransportConnection Connection, HandshakeResult Handshake)> ConnectAsync(CancellationToken cancellationToken)
    {
        // Combining the token of the caller with the deadline
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_connectTimeout);

        // Connecting, then agreeing on the protocol; a cancellation that the caller did not ask for is the deadline
        try
        {
            ITransportConnection connection = await _connector.ConnectAsync(_host, _port, deadline.Token).ConfigureAwait(false);
            try
            {
                HandshakeResult handshake = await ProtocolHandshake.RunClientAsync(connection, _codec, deadline.Token).ConfigureAwait(false);

                // Function's return
                return (connection, handshake);
            }
            catch
            {
                // The connection rule of the codec: a connection whose handshake failed is closed, never used; a failure of that close is
                // logged, so that it never hides the failure of the handshake
                try
                {
                    await connection.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception closeFailure)
                {
                    LogCloseFailed(closeFailure, _peerName);
                }
                throw;
            }
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
    /// <exception cref="RemoteErrorException">If the peer refuses the handshake</exception>
    /// <exception cref="InvalidDataException">If the peer breaks the handshake</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    private async Task<RequestClient> GetRequestClientAsync(CancellationToken cancellationToken)
    {
        // Using the open connection, without any lock
        RequestClient? client = _current;
        if (client is not null && client.IsOpen)
            return client;

        // Letting one call at a time open the connection
        await _openingLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Checking again: the client may have been disposed, or another call may have opened the connection while this one waited
            ThrowIfDisposed();
            client = _current;
            if (client is not null && client.IsOpen)
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

            // Failing at once during the back-off, without touching the network
            if (_backOffDelay > TimeSpan.Zero && _timeProvider.GetElapsedTime(_backOffStart) < _backOffDelay)
                throw new LinkFailureException(ErrorCodes.LinkUnavailable, $"{_peerName} cannot be reached; the next attempt is not due yet", null);

            // Opening the new connection; a failure delays the next attempt (a cancellation of the caller is not a failure of the peer)
            ITransportConnection connection;
            HandshakeResult handshake;
            try
            {
                (connection, handshake) = await ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogConnectionFailed(exception, _peerName, _host, _port);
                _openingFailures++;
                _backOffDelay = ComputeBackOff(_openingFailures);
                _backOffStart = _timeProvider.GetTimestamp();
                LogBackOff(_peerName, _openingFailures, _backOffDelay);
                throw;
            }
            _openingFailures = 0;
            _backOffDelay = TimeSpan.Zero;
            client = new RequestClient(connection, _codec, Math.Min(handshake.MaxConcurrentRequests, MaxConcurrentRequests));
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
    /// <para><b>Behavior:</b> opens the connection first when needed; the request is not retried when it fails. A request that gets no
    /// response within the request timeout fails with a <see cref="LinkFailureException"/> whose code is <see cref="ErrorCodes.LinkTimeout"/>;
    /// it is canceled on the peer like any canceled request.</para>
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
    /// <exception cref="LinkFailureException">If the peer cannot be reached (the connection is not open within the connect timeout, or is
    /// refused), or if the connection closes before the response arrives; its code is <see cref="ErrorCodes.LinkUnavailable"/></exception>
    /// <exception cref="RemoteErrorException">If the peer answers an error code</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    public async Task<TResponse> SendAsync<TRequest, TResponse>(string operation, TRequest request, CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        // Check
        ThrowIfDisposed();

        // Bounding the whole call with the request timeout, the wait for the connection included, so that the timeouts of the tiers add up
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_requestTimeout);

        // Getting the connection; a peer that cannot be reached, or whose handshake fails, is a failure of the link (a refusal keeps its code)
        RequestClient client;
        try
        {
            client = await GetRequestClientAsync(deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new LinkFailureException(ErrorCodes.LinkTimeout, $"{_peerName} could not be reached within {_requestTimeout}", exception);
        }
        catch (RemoteErrorException exception)
        {
            throw new LinkFailureException(exception.Code, $"{_peerName} refused the handshake", exception);
        }
        catch (Exception exception) when (exception is (System.Net.Sockets.SocketException or TimeoutException or IOException or InvalidDataException)
            and not LinkFailureException)
        {
            throw new LinkFailureException(ErrorCodes.LinkUnavailable, $"{_peerName} cannot be reached", exception);
        }

        // Sending the request within what is left of the request timeout: a deadline that expires cancels the request, so that the peer stops
        // its work too; a connection lost before the response is a failure of the link (the disposal of this client is not)
        try
        {
            // Function's return
            return await client.SendAsync<TRequest, TResponse>(operation, request, deadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
        {
            throw new LinkFailureException(ErrorCodes.LinkTimeout, $"{_peerName} did not answer within {_requestTimeout}", exception);
        }
        catch (IOException exception) when (Volatile.Read(ref _isDisposed) == 0)
        {
            throw new LinkFailureException(ErrorCodes.LinkUnavailable, $"The connection to {_peerName} was lost before the response", exception);
        }
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
