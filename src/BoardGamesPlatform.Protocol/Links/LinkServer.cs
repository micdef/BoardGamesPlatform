/*
 *	@file	src/BoardGamesPlatform.Protocol/Links/LinkServer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link: accepts the connections of a listener and serves each one, never ending silently
 */

// Using
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Links;

/// <summary>
/// Server side of a link (the Back for the Gateway, the Very Back for the Back)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one hardened accept loop for every tier that listens, instead of a copy per tier that could drift.</para>
/// <para><b>Behavior:</b> accepts the connections of a started listener until the stop is requested, and serves each one in its own task: the
/// handshake first (<see cref="ProtocolHandshake"/>, within its timeout), then the request server. The loop follows the rule of <see cref="ITransportListener"/>, and never ends silently:</para>
/// <list type="bullet">
/// <item><description>A disposed listener or a cancellation ends it when the stop is requested.</description></item>
/// <item><description>The same exceptions without a stop request mean the listener is lost: <see cref="RunAsync"/> fails, so that the tier
/// stops instead of running without listening.</description></item>
/// <item><description>Any other exception concerns one incoming connection: it is logged and the loop keeps accepting after a short pause;
/// after <see cref="MaxConsecutiveAcceptFailures"/> failures in a row, <see cref="RunAsync"/> fails the same way.</description></item>
/// </list>
/// <para>A connection is disposed when its service ends, whatever the reason; a failure of one connection never reaches the others. Any end
/// once the stop is requested is a close, not a failure. When the loop ends, the listener is disposed and the service of every connection is
/// awaited: the stopping token ends them. Beyond <see cref="DefaultMaxConnections"/> connections served at the same time (or the maximum
/// given), a new connection is closed at once and logged. The logs hold the remote end point and the exception; they go through the logger of the tier, with
/// the event ids 9011 to 9019, reserved for this class (see the coding conventions).</para>
/// </remarks>
public sealed partial class LinkServer
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of accept failures in a row after which the listener is considered broken
    /// </summary>
    public const int MaxConsecutiveAcceptFailures = 50;

    /// <summary>
    /// Number of connections served at the same time when none is given: a link has one client tier, which shares one connection, so a few
    /// places cover a reconnection that overlaps the end of the lost connection
    /// </summary>
    public const int DefaultMaxConnections = 8;

    /// <summary>
    /// Pause after an accept failure used by the tiers, so that a failure that repeats cannot make the loop spin and flood the logs
    /// </summary>
    public static readonly TimeSpan DefaultAcceptFailurePause = TimeSpan.FromMilliseconds(100);

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the link, already started; disposed when the loop ends
    /// </summary>
    private readonly ITransportListener _listener;

    /// <summary>
    /// Server that answers the requests of every connection
    /// </summary>
    private readonly RequestServer _server;

    /// <summary>
    /// Name of the tier in the logs, for example "the Very Back"
    /// </summary>
    private readonly string _name;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Pause after an accept failure
    /// </summary>
    private readonly TimeSpan _acceptFailurePause;

    /// <summary>
    /// Time allowed to a client for the handshake that opens its connection
    /// </summary>
    private readonly TimeSpan _handshakeTimeout;

    /// <summary>
    /// Number of connections served at the same time, at most
    /// </summary>
    private readonly int _maxConnections;

    /// <summary>
    /// Services of the connections accepted and not yet swept; only the accept loop reads and writes it
    /// </summary>
    private readonly List<Task> _connectionTasks = new List<Task>();

    /// <summary>
    /// Whether <see cref="RunAsync"/> has been called (0: no, 1: yes)
    /// </summary>
    private int _isRunning;

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
    /// Constructor
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener of the link, already started; the server disposes it when the loop ends</param>
    /// <param name="server">[Cannot be null] Server that answers the requests</param>
    /// <param name="name">[Cannot be null or blank] Name of the tier in the logs, for example "the Very Back"</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="acceptFailurePause">Pause after an accept failure; zero or more (<see cref="DefaultAcceptFailurePause"/> outside the
    /// tests)</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the name is blank</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the pause is negative</exception>
    public LinkServer(ITransportListener listener, RequestServer server, string name, ILogger logger, TimeSpan acceptFailurePause)
        : this(listener, server, name, logger, acceptFailurePause, ProtocolHandshake.DefaultServerTimeout, DefaultMaxConnections)
    {
    }

    /// <summary>
    /// Constructor with the time allowed for the handshake (the tests shorten it)
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener of the link, already started; the server disposes it when the loop ends</param>
    /// <param name="server">[Cannot be null] Server that answers the requests</param>
    /// <param name="name">[Cannot be null or blank] Name of the tier in the logs, for example "the Very Back"</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="acceptFailurePause">Pause after an accept failure; zero or more</param>
    /// <param name="handshakeTimeout">Time allowed to a client for the handshake of its connection; greater than zero
    /// (<see cref="ProtocolHandshake.DefaultServerTimeout"/> outside the tests)</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the name is blank</exception>
    /// <param name="maxConnections">Number of connections served at the same time, at most; 1 or more (<see cref="DefaultMaxConnections"/>
    /// outside the tests)</param>
    /// <exception cref="ArgumentOutOfRangeException">If the pause is negative, the handshake timeout not greater than zero, or the maximum of
    /// connections below one</exception>
    public LinkServer(ITransportListener listener, RequestServer server, string name, ILogger logger, TimeSpan acceptFailurePause, TimeSpan handshakeTimeout,
        int maxConnections)
    {
        // Checks
        if (listener is null)
            throw new ArgumentNullException(nameof(listener), $"The value of the parameter: {nameof(listener)} cannot be null");
        if (server is null)
            throw new ArgumentNullException(nameof(server), $"The value of the parameter: {nameof(server)} cannot be null");
        if (name is null)
            throw new ArgumentNullException(nameof(name), $"The value of the parameter: {nameof(name)} cannot be null");
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException($"The value of the parameter: {nameof(name)} cannot be blank", nameof(name));
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (acceptFailurePause < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(acceptFailurePause), $"The value of the parameter: {nameof(acceptFailurePause)} cannot be negative");
        if (maxConnections < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConnections), $"The value of the parameter: {nameof(maxConnections)} must be greater than zero");
        if (handshakeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(handshakeTimeout), $"The value of the parameter: {nameof(handshakeTimeout)} must be greater than zero");

        // Setting
        _listener = listener;
        _server = server;
        _name = name;
        _logger = logger;
        _acceptFailurePause = acceptFailurePause;
        _handshakeTimeout = handshakeTimeout;
        _maxConnections = maxConnections;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log an accepted connection
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9011, Level = LogLevel.Information, Message = "Connection accepted from {RemoteEndPoint}")]
    private partial void LogAccepted(string remoteEndPoint);

    /// <summary>
    /// Method used to log the normal end of the service of a connection
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9012, Level = LogLevel.Information, Message = "Connection from {RemoteEndPoint} closed")]
    private partial void LogClosed(string remoteEndPoint);

    /// <summary>
    /// Method used to log the failure of the service of a connection
    /// </summary>
    /// <param name="exception">Why the service ended</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9013, Level = LogLevel.Warning, Message = "The service of the connection from {RemoteEndPoint} failed and the connection is closed")]
    private partial void LogConnectionFailed(Exception exception, string remoteEndPoint);

    /// <summary>
    /// Method used to log the failure to accept one incoming connection
    /// </summary>
    /// <param name="exception">Why the connection was not accepted</param>
    [LoggerMessage(EventId = 9014, Level = LogLevel.Warning, Message = "An incoming connection could not be accepted; the listener keeps accepting")]
    private partial void LogAcceptFailed(Exception exception);

    /// <summary>
    /// Method used to log the stop of the listener
    /// </summary>
    /// <param name="name">Name of the tier</param>
    [LoggerMessage(EventId = 9015, Level = LogLevel.Information, Message = "Listener of {Name} stopped")]
    private partial void LogStoppedListening(string name);

    /// <summary>
    /// Method used to log that closing a connection failed
    /// </summary>
    /// <param name="exception">Why the closing failed</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9016, Level = LogLevel.Warning, Message = "Closing the connection from {RemoteEndPoint} failed")]
    private partial void LogCloseFailed(Exception exception, string remoteEndPoint);

    /// <summary>
    /// Method used to close a connection, logging a failure instead of throwing it
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to close</param>
    /// <param name="remoteEndPoint">[Cannot be null] Address and port of the peer, for the log</param>
    /// <returns>
    /// A task that completes once the connection is closed, or its failure logged
    /// </returns>
    private async Task CloseAsync(ITransportConnection connection, string remoteEndPoint)
    {
        // Closing; a failure concerns this connection only, and must neither stop the tier nor be lost
        try
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            LogCloseFailed(exception, remoteEndPoint);
        }
    }

    /// <summary>
    /// Method used to log that the listener was lost without a stop request
    /// </summary>
    /// <param name="exception">How the loss showed</param>
    /// <param name="name">Name of the tier</param>
    [LoggerMessage(EventId = 9017, Level = LogLevel.Critical, Message = "The listener of {Name} was closed without a stop request; {Name} stops")]
    private partial void LogListenerLost(Exception exception, string name);

    /// <summary>
    /// Method used to log that the accept failures repeat
    /// </summary>
    /// <param name="exception">Last failure</param>
    /// <param name="failures">Number of failures in a row</param>
    /// <param name="name">Name of the tier</param>
    [LoggerMessage(EventId = 9018, Level = LogLevel.Critical, Message = "{Failures} accept failures in a row: the listener of {Name} is broken; {Name} stops")]
    private partial void LogAcceptFailuresRepeated(Exception exception, int failures, string name);

    /// <summary>
    /// Method used to log a connection refused because the maximum of connections is reached
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="maxConnections">Maximum of connections served at the same time</param>
    [LoggerMessage(EventId = 9019, Level = LogLevel.Warning, Message = "Connection from {RemoteEndPoint} refused: {MaxConnections} connections are already served")]
    private partial void LogConnectionRefused(string remoteEndPoint, int maxConnections);

    /// <summary>
    /// Method used to serve one accepted connection until its service ends, then dispose it
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: the cause of an end is logged. Runs in its own task, so that a slow or failing connection never
    /// delays the others. Any end once the stop is requested is a close, not a failure, whatever the exception (a client library may report a
    /// canceled call as an error of its own).</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Accepted connection; disposed by this method</param>
    /// <param name="cancellationToken">Token canceled when the stop is requested</param>
    /// <returns>
    /// A task that completes once the connection is disposed
    /// </returns>
    private async Task ServeConnectionAsync(ITransportConnection connection, CancellationToken cancellationToken)
    {
        // Reading the remote end point first (read once when the connection was accepted)
        string remoteEndPoint = connection.RemoteEndPoint.ToString() ?? "unknown";
        LogAccepted(remoteEndPoint);
        try
        {
            // Agreeing on the protocol with the client, then serving the connection to its end
            await ProtocolHandshake.RunServerAsync(connection, _server.Codec, _server.MaxConcurrentRequests, _handshakeTimeout, cancellationToken).ConfigureAwait(false);
            await _server.ServeAsync(connection, cancellationToken).ConfigureAwait(false);
            LogClosed(remoteEndPoint);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The stop is requested: not a failure
            LogClosed(remoteEndPoint);
        }
        catch (Exception exception)
        {
            // The connection rule of the codec: after any failure, the connection is closed, never reused
            LogConnectionFailed(exception, remoteEndPoint);
        }
        finally
        {
            await CloseAsync(connection, remoteEndPoint).ConfigureAwait(false);
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to accept and serve the connections until the stop is requested (Asynchronous)
    /// </summary>
    /// <param name="stoppingToken">Token canceled when the tier stops</param>
    /// <returns>
    /// A task that completes once the listener is disposed and every connection served to its end
    /// </returns>
    /// <exception cref="InvalidOperationException">If the server already ran, if the listener is lost without a stop request, or if the
    /// accept failures repeat</exception>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        // Check
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            throw new InvalidOperationException("The link server has already run");

        int consecutiveFailures = 0;
        try
        {
            // Accepting connections until the stop is requested
            while (true)
            {
                ITransportConnection connection;
                try
                {
                    connection = await _listener.AcceptAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
                {
                    // The accept loop rule: the tier stops; without a stop request, nothing would listen any more while the tier runs
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    LogListenerLost(exception, _name);
                    throw new InvalidOperationException($"The listener of {_name} was closed without a stop request", exception);
                }
                catch (Exception exception)
                {
                    // The accept loop rule: this exception concerns one incoming connection, unless it repeats
                    consecutiveFailures++;
                    if (consecutiveFailures >= MaxConsecutiveAcceptFailures)
                    {
                        LogAcceptFailuresRepeated(exception, consecutiveFailures, _name);
                        throw new InvalidOperationException($"{consecutiveFailures} accept failures in a row: the listener of {_name} is broken", exception);
                    }

                    // Pausing before the next accept; the pause is cut short when the stop is requested
                    LogAcceptFailed(exception);
                    await Task.Delay(_acceptFailurePause, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                // Refusing the connection when the maximum is reached: closed at once, so that a peer cannot make the tier hold an unbounded
                // number of connections (each one can hold its requests in progress)
                consecutiveFailures = 0;
                _connectionTasks.RemoveAll(task => task.IsCompleted);
                if (_connectionTasks.Count >= _maxConnections)
                {
                    string refused = connection.RemoteEndPoint.ToString() ?? "unknown";
                    LogConnectionRefused(refused, _maxConnections);
                    await CloseAsync(connection, refused).ConfigureAwait(false);
                    continue;
                }

                // Serving the connection in its own task, the services that ended being forgotten
                _connectionTasks.Add(Task.Run(() => ServeConnectionAsync(connection, stoppingToken), CancellationToken.None));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The stop was requested during the pause after an accept failure
        }
        finally
        {
            // Closing the listener, then waiting for the service of every connection: the stopping token ends them
            await _listener.DisposeAsync().ConfigureAwait(false);
            LogStoppedListening(_name);
            await Task.WhenAll(_connectionTasks).ConfigureAwait(false);
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
