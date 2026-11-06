/*
 *	@file	src/BoardGamesPlatform.Protocol/Links/LinkServer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link: accepts the connections of a listener and serves each one, never ending silently
 */

// Using
using System.Diagnostics;
using System.Net;
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
/// connection is first established by the listener (the TLS handshake from 0.5.0), then the protocol is agreed (<see cref="ProtocolHandshake"/>),
/// both within the one handshake timeout, then the request server answers. A slow or hostile peer therefore never holds the accept loop. The
/// loop follows the rule of <see cref="ITransportListener"/>, and never ends silently:</para>
/// <list type="bullet">
/// <item><description>A disposed listener or a cancellation ends it when the stop is requested.</description></item>
/// <item><description>The same exceptions without a stop request mean the listener is lost: <see cref="RunAsync"/> fails, so that the tier
/// stops instead of running without listening.</description></item>
/// <item><description>Any other exception concerns one incoming connection: it is logged and the loop keeps accepting after a short pause;
/// after <see cref="MaxConsecutiveAcceptFailures"/> failures in a row, <see cref="RunAsync"/> fails the same way.</description></item>
/// </list>
/// <para>A connection is disposed when its service ends, whatever the reason; a failure of one connection never reaches the others. Any end
/// once the stop is requested is a close, not a failure. When the loop ends, for whatever reason, the listener is disposed, the service of
/// every connection is canceled and awaited: a lost listener never leaves connections served by a tier that stops.</para>
/// <para>The places follow <see cref="LinkServerLimits"/>: a connection in its handshake (not yet authenticated) takes a place of the
/// handshakes, never one of the served connections, and one source can only take a part of the places of the handshakes; the connection
/// takes a served place once its handshake is done. A new connection is closed at once, before any byte, when no served place is free, when
/// every place of the handshakes is taken, or when its source already holds its part; a connection whose handshake ends when the served
/// places are all taken is closed too. A silent or unknown peer can therefore delay the next connections of the client tier for the
/// handshake timeout at most, never take the place of the connections it serves. A refusal is logged at most once per
/// <see cref="LinkServerLimits.RefusalLogInterval"/>, the refusals in between being counted in the next log, so that a flood of connections
/// does not flood the logs.</para>
/// <para>The logs hold the remote end point and the exception; they go through the logger of the tier, with the event ids 9011 to 9019 and
/// 9040 to 9043, reserved for this class (see the coding conventions). A connection is logged as accepted once it is established
/// (authenticated from 0.5.0); a failure of the establishment is logged by the listener, at its pace, and only traced here.</para>
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
    /// Number of connections served at the same time when none is given (see <see cref="LinkServerLimits.DefaultMaxConnections"/>)
    /// </summary>
    public const int DefaultMaxConnections = LinkServerLimits.DefaultMaxConnections;

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
    /// Limits of the connections
    /// </summary>
    private readonly LinkServerLimits _limits;

    /// <summary>
    /// Clock of the pace of the refusal logs
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Services of the connections accepted and not yet swept; only the accept loop reads and writes it
    /// </summary>
    private readonly List<Task> _connectionTasks = new List<Task>();

    /// <summary>
    /// Lock of the places and of the pace of the refusal logs, taken by the accept loop and by the services of the connections
    /// </summary>
    private readonly object _slotsLock = new object();

    /// <summary>
    /// Number of connections in their handshake, by source (a source without any is removed)
    /// </summary>
    private readonly Dictionary<string, int> _pendingBySource = new Dictionary<string, int>(StringComparer.Ordinal);

    /// <summary>
    /// Number of connections in their handshake
    /// </summary>
    private int _pending;

    /// <summary>
    /// Number of connections served
    /// </summary>
    private int _served;

    /// <summary>
    /// Whether a refusal has already been logged
    /// </summary>
    private bool _hasLoggedRefusal;

    /// <summary>
    /// Time stamp (of <see cref="_timeProvider"/>) of the last refusal logged
    /// </summary>
    private long _lastRefusalLog;

    /// <summary>
    /// Number of refusals not logged since the last refusal logged
    /// </summary>
    private int _unloggedRefusals;

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

    /// <summary>
    /// Why a connection is refused
    /// </summary>
    private enum Refusal
    {
        /// <summary>
        /// Every served place is taken
        /// </summary>
        ServedFull,

        /// <summary>
        /// Every place of the handshakes is taken
        /// </summary>
        HandshakesFull,

        /// <summary>
        /// The source already holds its part of the places of the handshakes
        /// </summary>
        SourceFull
    }

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
        : this(listener, server, name, logger, acceptFailurePause, ProtocolHandshake.DefaultServerTimeout, LinkServerLimits.Default, TimeProvider.System)
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
        : this(listener, server, name, logger, acceptFailurePause, handshakeTimeout, new LinkServerLimits(maxConnections, LinkServerLimits.DefaultMaxPendingHandshakes,
            LinkServerLimits.DefaultMaxHandshakesPerSource, LinkServerLimits.DefaultRefusalLogInterval), TimeProvider.System)
    {
    }

    /// <summary>
    /// Constructor with every limit and the clock of the refusal logs (the tests shorten or fake them)
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener of the link, already started; the server disposes it when the loop ends</param>
    /// <param name="server">[Cannot be null] Server that answers the requests</param>
    /// <param name="name">[Cannot be null or blank] Name of the tier in the logs, for example "the Very Back"</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="acceptFailurePause">Pause after an accept failure; zero or more</param>
    /// <param name="handshakeTimeout">Time allowed to a client for the handshake of its connection; greater than zero</param>
    /// <param name="limits">[Cannot be null] Limits of the connections (<see cref="LinkServerLimits.Default"/> outside the tests)</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the pace of the refusal logs</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If the name is blank</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the pause is negative, or the handshake timeout not greater than zero</exception>
    public LinkServer(ITransportListener listener, RequestServer server, string name, ILogger logger, TimeSpan acceptFailurePause, TimeSpan handshakeTimeout,
        LinkServerLimits limits, TimeProvider timeProvider)
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
        if (handshakeTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(handshakeTimeout), $"The value of the parameter: {nameof(handshakeTimeout)} must be greater than zero");
        if (limits is null)
            throw new ArgumentNullException(nameof(limits), $"The value of the parameter: {nameof(limits)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");

        // Setting
        _listener = listener;
        _server = server;
        _name = name;
        _logger = logger;
        _acceptFailurePause = acceptFailurePause;
        _handshakeTimeout = handshakeTimeout;
        _limits = limits;
        _timeProvider = timeProvider;
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
    /// Method used to trace a connection that failed its establishment (TLS), which the listener already logged at its pace
    /// </summary>
    /// <param name="exception">Why the establishment failed</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9040, Level = LogLevel.Debug, Message = "Connection from {RemoteEndPoint} closed: its establishment failed (logged by the listener)")]
    private partial void LogEstablishmentFailed(Exception exception, string remoteEndPoint);

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
    /// Method used to log a connection refused because every place of the handshakes is taken
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="maxPendingHandshakes">Maximum of connections in their handshake at the same time</param>
    [LoggerMessage(EventId = 9041, Level = LogLevel.Warning, Message = "Connection from {RemoteEndPoint} refused: {MaxPendingHandshakes} connections are already in their handshake")]
    private partial void LogHandshakesFull(string remoteEndPoint, int maxPendingHandshakes);

    /// <summary>
    /// Method used to log a connection refused because its source already holds its part of the places of the handshakes
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="maxHandshakesPerSource">Maximum of connections in their handshake at the same time from one source</param>
    [LoggerMessage(EventId = 9042, Level = LogLevel.Warning, Message = "Connection from {RemoteEndPoint} refused: its source already has {MaxHandshakesPerSource} connections in their handshake")]
    private partial void LogSourceFull(string remoteEndPoint, int maxHandshakesPerSource);

    /// <summary>
    /// Method used to log the number of refusals that were not logged one by one
    /// </summary>
    /// <param name="count">Number of refusals not logged</param>
    /// <param name="interval">Shortest time between two logs of a refusal</param>
    [LoggerMessage(EventId = 9043, Level = LogLevel.Warning, Message = "{Count} other connections were refused without a log of their own (at most one log every {Interval})")]
    private partial void LogRefusalsNotLogged(int count, TimeSpan interval);

    /// <summary>
    /// Method used to get the source of a peer: its IP address, without the port it chose
    /// </summary>
    /// <param name="remoteEndPoint">[Cannot be null] End point of the peer</param>
    /// <returns>
    /// The source
    /// </returns>
    private static string GetSource(EndPoint remoteEndPoint)
    {
        // An IPv4 address seen through IPv6 is the same source as the IPv4 address
        if (remoteEndPoint is IPEndPoint ip)
            return (ip.Address.IsIPv4MappedToIPv6 ? ip.Address.MapToIPv4() : ip.Address).ToString();

        // Function's return
        return remoteEndPoint.ToString() ?? "unknown";
    }

    /// <summary>
    /// Method used to take a place of the handshakes for a new connection
    /// </summary>
    /// <param name="source">[Cannot be null] Source of the connection</param>
    /// <param name="refusal">Why the connection is refused, when no place is taken</param>
    /// <returns>
    /// True when a place is taken, false when the connection is refused
    /// </returns>
    private bool TryTakeHandshakeSlot(string source, out Refusal refusal)
    {
        lock (_slotsLock)
        {
            // Refusing when nothing could serve the connection, when every place of the handshakes is taken, or when the source holds its part
            _pendingBySource.TryGetValue(source, out int ofSource);
            refusal = Refusal.ServedFull;
            if (_served >= _limits.MaxConnections)
                return false;
            refusal = Refusal.HandshakesFull;
            if (_pending >= _limits.MaxPendingHandshakes)
                return false;
            refusal = Refusal.SourceFull;
            if (ofSource >= _limits.MaxHandshakesPerSource)
                return false;

            // Taking the place
            _pending++;
            _pendingBySource[source] = ofSource + 1;
        }

        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to give back the place of the handshakes of a connection
    /// </summary>
    /// <param name="source">[Cannot be null] Source of the connection</param>
    private void ReleaseHandshakeSlot(string source)
    {
        lock (_slotsLock)
        {
            // Giving back the place, a source without any being forgotten
            _pending--;
            int ofSource = _pendingBySource[source] - 1;
            if (ofSource == 0)
                _pendingBySource.Remove(source);
            else
                _pendingBySource[source] = ofSource;
        }
    }

    /// <summary>
    /// Method used to take a served place for a connection whose handshake is done
    /// </summary>
    /// <returns>
    /// True when a place is taken, false when every served place is taken
    /// </returns>
    private bool TryTakeServedSlot()
    {
        lock (_slotsLock)
        {
            // Refusing when every place is taken
            if (_served >= _limits.MaxConnections)
                return false;

            // Taking the place
            _served++;
        }

        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to give back the served place of a connection
    /// </summary>
    private void ReleaseServedSlot()
    {
        lock (_slotsLock)
        {
            _served--;
        }
    }

    /// <summary>
    /// Method used to log a refused connection, at most once per interval of the limits
    /// </summary>
    /// <param name="refusal">Why the connection is refused</param>
    /// <param name="remoteEndPoint">[Cannot be null] Address and port of the peer</param>
    private void LogRefusal(Refusal refusal, string remoteEndPoint)
    {
        // Counting the refusal instead of logging it when the last log is too recent
        int notLogged;
        lock (_slotsLock)
        {
            long now = _timeProvider.GetTimestamp();
            if (_hasLoggedRefusal && _timeProvider.GetElapsedTime(_lastRefusalLog, now) < _limits.RefusalLogInterval)
            {
                _unloggedRefusals++;
                return;
            }
            notLogged = _unloggedRefusals;
            _unloggedRefusals = 0;
            _lastRefusalLog = now;
            _hasLoggedRefusal = true;
        }

        // Logging the refusals counted since the last log, then this one
        if (notLogged > 0)
            LogRefusalsNotLogged(notLogged, _limits.RefusalLogInterval);
        switch (refusal)
        {
            case Refusal.HandshakesFull:
                LogHandshakesFull(remoteEndPoint, _limits.MaxPendingHandshakes);
                break;
            case Refusal.SourceFull:
                LogSourceFull(remoteEndPoint, _limits.MaxHandshakesPerSource);
                break;
            default:
                LogConnectionRefused(remoteEndPoint, _limits.MaxConnections);
                break;
        }
    }

    /// <summary>
    /// Method used to serve one accepted connection until its service ends, then dispose it
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: the cause of an end is logged. Runs in its own task, so that a slow or failing connection never
    /// delays the others. Any end once the stop is requested is a close, not a failure, whatever the exception (a client library may report a
    /// canceled call as an error of its own).</para>
    /// </remarks>
    /// <param name="accepted">[Cannot be null] Accepted connection; disposed by this method, with the connection established on it</param>
    /// <param name="source">[Cannot be null] Source of the connection, whose place of the handshakes is taken; this method gives it back</param>
    /// <param name="cancellationToken">Token canceled when the stop is requested</param>
    /// <returns>
    /// A task that completes once the connection is disposed and its places given back
    /// </returns>
    private async Task ServeConnectionAsync(ITransportConnection accepted, string source, CancellationToken cancellationToken)
    {
        // Reading the remote end point first (read once when the connection was accepted)
        string remoteEndPoint = "unknown";
        ITransportConnection connection = accepted;
        bool isPending = true;
        bool isServed = false;
        try
        {
            remoteEndPoint = accepted.RemoteEndPoint.ToString() ?? remoteEndPoint;

            // Establishing the connection (TLS), then agreeing on the protocol with the client, both within the handshake timeout; a deadline
            // that expires is a timeout, not a stop of the tier. A failure of the establishment is logged by the listener, at its pace (a peer
            // can fail handshakes in a loop): it is only traced here
            long start = Stopwatch.GetTimestamp();
            using (CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(_handshakeTimeout);
                try
                {
                    connection = await _listener.EstablishAsync(accepted, deadline.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException($"The client did not establish its connection within {_handshakeTimeout}", exception);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    LogEstablishmentFailed(exception, remoteEndPoint);
                    return;
                }
            }
            LogAccepted(remoteEndPoint);
            TimeSpan left = _handshakeTimeout - Stopwatch.GetElapsedTime(start);
            if (left <= TimeSpan.Zero)
                throw new TimeoutException($"The client did not establish its connection within {_handshakeTimeout}");
            await ProtocolHandshake.RunServerAsync(connection, _server.Codec, _server.MaxConcurrentRequests, left, cancellationToken).ConfigureAwait(false);

            // Moving from a place of the handshakes to a served place; the connection is closed when none is free any more
            ReleaseHandshakeSlot(source);
            isPending = false;
            if (!TryTakeServedSlot())
            {
                LogRefusal(Refusal.ServedFull, remoteEndPoint);
                return;
            }
            isServed = true;

            // Serving the connection to its end
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
            // Closing the established connection, which owns the accepted one, or the accepted one when nothing was established, then giving
            // back its place (after the close, so that the tier never holds more connections than its places)
            await CloseAsync(connection, remoteEndPoint).ConfigureAwait(false);
            if (isPending)
                ReleaseHandshakeSlot(source);
            if (isServed)
                ReleaseServedSlot();
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to accept and serve the connections until the stop is requested (Asynchronous)
    /// </summary>
    /// <param name="stoppingToken">Token canceled when the tier stops</param>
    /// <returns>
    /// A task that completes once the listener is disposed and the service of every connection ended
    /// </returns>
    /// <exception cref="InvalidOperationException">If the server already ran, if the listener is lost without a stop request, or if the
    /// accept failures repeat</exception>
    public async Task RunAsync(CancellationToken stoppingToken)
    {
        // Check
        if (Interlocked.Exchange(ref _isRunning, 1) == 1)
            throw new InvalidOperationException("The link server has already run");

        // Services of the connections, canceled when the tier stops and when the loop ends for any other reason
        using CancellationTokenSource services = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
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

                // Refusing the connection when it gets no place of the handshakes: closed at once, so that a peer cannot make the tier hold
                // an unbounded number of connections (each one can hold its requests in progress), nor take the places of the served ones
                consecutiveFailures = 0;
                _connectionTasks.RemoveAll(task => task.IsCompleted);
                string source = GetSource(connection.RemoteEndPoint);
                if (!TryTakeHandshakeSlot(source, out Refusal refusal))
                {
                    string refused = connection.RemoteEndPoint.ToString() ?? "unknown";
                    LogRefusal(refusal, refused);
                    await CloseAsync(connection, refused).ConfigureAwait(false);
                    continue;
                }

                // Serving the connection in its own task, which gives its places back; the services that ended are forgotten
                _connectionTasks.Add(Task.Run(() => ServeConnectionAsync(connection, source, services.Token), CancellationToken.None));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The stop was requested during the pause after an accept failure
        }
        finally
        {
            // Closing the listener, then ending the service of every connection and waiting for it: without the cancellation, a listener lost
            // without a stop request would wait for ever for connections that nothing ends any more. The services are ended even when closing
            // the listener fails; that failure then ends the run
            try
            {
                await _listener.DisposeAsync().ConfigureAwait(false);
                LogStoppedListening(_name);
            }
            finally
            {
                await services.CancelAsync().ConfigureAwait(false);
                await Task.WhenAll(_connectionTasks).ConfigureAwait(false);
            }
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
