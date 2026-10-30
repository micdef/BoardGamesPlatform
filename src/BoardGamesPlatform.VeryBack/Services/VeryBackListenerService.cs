/*
 *	@file	src/BoardGamesPlatform.VeryBack/Services/VeryBackListenerService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Listens for the connections of the Back and answers their requests
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Services;

/// <summary>
/// Service used to accept the connections of the Back and answer their requests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the server side of the link Back to Very Back (plain TCP in 0.3.0 and 0.4.0, development only).</para>
/// <para><b>Behavior:</b> the listener is started when the host starts, so that a port already in use or an invalid endpoint stops the host
/// at once, and <see cref="LocalEndPoint"/> is known as soon as the start has returned. A listen address other than loopback is accepted (the
/// tiers may run on separate machines) but logged as a warning, since the link is neither encrypted nor authenticated yet.</para>
/// <para>The accept loop follows the rule of <see cref="ITransportListener"/>, with one addition: it never ends silently.</para>
/// <list type="bullet">
/// <item><description>A disposed listener or a cancellation ends it when the host stops.</description></item>
/// <item><description>The same exceptions without a stop request mean the listener is lost: the service fails, so that the host stops instead
/// of running without listening.</description></item>
/// <item><description>Any other exception concerns one incoming connection: it is logged and the loop keeps accepting after a short pause;
/// after <see cref="MaxConsecutiveAcceptFailures"/> failures in a row, the service fails the same way.</description></item>
/// </list>
/// <para>Each accepted connection is served in its own task by a <see cref="RequestServer"/>, and disposed when its service ends, whatever the
/// reason (the peer closes, a protocol violation, a failing procedure). A failure of one connection never reaches the others. When the
/// service ends, the listener is disposed and the service of every connection is awaited: the stopping token ends them.</para>
/// <para>The logs hold the remote end point and the exception, never the connection string. A SQL error may still name the server, the
/// database or the login, as SQL Server writes them in its messages.</para>
/// </remarks>
public sealed partial class VeryBackListenerService : BackgroundService
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of accept failures in a row after which the listener is considered broken and the service fails
    /// </summary>
    public const int MaxConsecutiveAcceptFailures = 50;

    /// <summary>
    /// Pause after an accept failure, so that a failure that repeats cannot make the loop spin and flood the logs
    /// </summary>
    private static readonly TimeSpan _defaultAcceptFailurePause = TimeSpan.FromMilliseconds(100);

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the link; null until the service is started
    /// </summary>
    private ITransportListener? _listener;

    /// <summary>
    /// Services of the connections accepted and not yet swept; only the accept loop reads and writes it
    /// </summary>
    private readonly List<Task> _connectionTasks = new List<Task>();

    /// <summary>
    /// Pause after an accept failure (shorter in the tests)
    /// </summary>
    private readonly TimeSpan _acceptFailurePause;

    #endregion

    #region Delegates

    /// <summary>
    /// Function that starts the listener on an end point (plain TCP outside the tests)
    /// </summary>
    private readonly Func<IPEndPoint, ITransportListener> _startListener;

    #endregion

    #endregion

    #region Enumerations

    // #N/A

    #endregion

    #region Services

    /// <summary>
    /// Settings of the listener
    /// </summary>
    private readonly VeryBackOptions _options;

    /// <summary>
    /// Call of the Ping procedure
    /// </summary>
    private readonly IPingProcedure _svcPingProcedure;

    /// <summary>
    /// Logger of the service
    /// </summary>
    private readonly ILogger<VeryBackListenerService> _logger;

    #endregion

    #region Accessors/Properties

    #region Members & Properties

    /// <summary>
    /// Address and port the service listens on; when port 0 is configured, the port given by the system (used by the tests)
    /// </summary>
    /// <exception cref="InvalidOperationException">If the service is not started</exception>
    public IPEndPoint LocalEndPoint => _listener?.LocalEndPoint ?? throw new InvalidOperationException("The listener is not started");

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, listening on plain TCP
    /// </summary>
    /// <param name="options">[Cannot be null] Settings of the listener, validated at startup</param>
    /// <param name="svcPingProcedure">[Cannot be null] Call of the Ping procedure</param>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public VeryBackListenerService(IOptions<VeryBackOptions> options, IPingProcedure svcPingProcedure, ILogger<VeryBackListenerService> logger)
        : this(options, svcPingProcedure, logger, endPoint => TcpTransportListener.Start(endPoint), _defaultAcceptFailurePause)
    {
    }

    /// <summary>
    /// Constructor with the function that starts the listener and the pause after an accept failure, so that the tests can make the accept
    /// loop fail quickly
    /// </summary>
    /// <param name="options">[Cannot be null] Settings of the listener, validated at startup</param>
    /// <param name="svcPingProcedure">[Cannot be null] Call of the Ping procedure</param>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <param name="startListener">[Cannot be null] Function that starts the listener on an end point</param>
    /// <param name="acceptFailurePause">Pause after an accept failure; zero or more</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the pause is negative</exception>
    internal VeryBackListenerService(IOptions<VeryBackOptions> options, IPingProcedure svcPingProcedure, ILogger<VeryBackListenerService> logger,
        Func<IPEndPoint, ITransportListener> startListener, TimeSpan acceptFailurePause)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (svcPingProcedure is null)
            throw new ArgumentNullException(nameof(svcPingProcedure), $"The value of the parameter: {nameof(svcPingProcedure)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (startListener is null)
            throw new ArgumentNullException(nameof(startListener), $"The value of the parameter: {nameof(startListener)} cannot be null");
        if (acceptFailurePause < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(acceptFailurePause), $"The value of the parameter: {nameof(acceptFailurePause)} cannot be negative");

        // Setting services
        _options = options.Value;
        _svcPingProcedure = svcPingProcedure;
        _logger = logger;
        _startListener = startListener;
        _acceptFailurePause = acceptFailurePause;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log the start of the listener
    /// </summary>
    /// <param name="endPoint">Address and port listened to</param>
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Very Back listening on {EndPoint}")]
    private partial void LogListening(IPEndPoint endPoint);

    /// <summary>
    /// Method used to log an accepted connection
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the Back</param>
    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Connection accepted from {RemoteEndPoint}")]
    private partial void LogAccepted(string remoteEndPoint);

    /// <summary>
    /// Method used to log the normal end of the service of a connection
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the Back</param>
    [LoggerMessage(EventId = 12, Level = LogLevel.Information, Message = "Connection from {RemoteEndPoint} closed")]
    private partial void LogClosed(string remoteEndPoint);

    /// <summary>
    /// Method used to log the failure of the service of a connection
    /// </summary>
    /// <param name="exception">Why the service ended</param>
    /// <param name="remoteEndPoint">Address and port of the Back</param>
    [LoggerMessage(EventId = 13, Level = LogLevel.Warning, Message = "The service of the connection from {RemoteEndPoint} failed and the connection is closed")]
    private partial void LogConnectionFailed(Exception exception, string remoteEndPoint);

    /// <summary>
    /// Method used to log the failure to accept one incoming connection
    /// </summary>
    /// <param name="exception">Why the connection was not accepted</param>
    [LoggerMessage(EventId = 14, Level = LogLevel.Warning, Message = "An incoming connection could not be accepted; the listener keeps accepting")]
    private partial void LogAcceptFailed(Exception exception);

    /// <summary>
    /// Method used to log the stop of the listener
    /// </summary>
    [LoggerMessage(EventId = 15, Level = LogLevel.Information, Message = "Very Back stopped listening")]
    private partial void LogStoppedListening();

    /// <summary>
    /// Method used to warn that the Very Back listens on an address that other machines can reach
    /// </summary>
    /// <param name="endPoint">Address and port listened to</param>
    [LoggerMessage(EventId = 16, Level = LogLevel.Warning,
        Message = "Very Back listening on {EndPoint}, which is not a loopback address: the link is neither encrypted nor authenticated yet, restrict it with the firewall")]
    private partial void LogNotLoopback(IPEndPoint endPoint);

    /// <summary>
    /// Method used to log that the listener was lost without a stop request
    /// </summary>
    /// <param name="exception">How the loss showed</param>
    [LoggerMessage(EventId = 17, Level = LogLevel.Critical, Message = "The listener of the Very Back was closed without a stop request; the Very Back stops")]
    private partial void LogListenerLost(Exception exception);

    /// <summary>
    /// Method used to log that the accept failures repeat
    /// </summary>
    /// <param name="exception">Last failure</param>
    /// <param name="failures">Number of failures in a row</param>
    [LoggerMessage(EventId = 18, Level = LogLevel.Critical, Message = "{Failures} accept failures in a row: the listener of the Very Back is broken; the Very Back stops")]
    private partial void LogAcceptFailuresRepeated(Exception exception, int failures);

    /// <summary>
    /// Method used to answer a Ping request with the time of the database server
    /// </summary>
    /// <param name="request">Body of the request, which holds nothing</param>
    /// <param name="cancellationToken">Token of the service of the connection</param>
    /// <returns>
    /// The response, holding the server time
    /// </returns>
    private async Task<PingResponse> PingAsync(PingRequest request, CancellationToken cancellationToken)
    {
        // Asking the database
        DateTimeOffset serverTime = await _svcPingProcedure.GetServerTimeAsync(cancellationToken).ConfigureAwait(false);

        // Function's return
        return new PingResponse(serverTime);
    }

    /// <summary>
    /// Method used to serve one accepted connection until its service ends, then dispose it
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: the cause of an end is logged. Runs in its own task, so that a slow or failing connection never
    /// delays the others. Any end once the host stops is a close, not a failure, whatever the exception (the SQL client may report a
    /// canceled call as a SQL error).</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Accepted connection; disposed by this method</param>
    /// <param name="server">[Cannot be null] Server that answers the requests</param>
    /// <param name="cancellationToken">Token canceled when the host stops</param>
    /// <returns>
    /// A task that completes once the connection is disposed
    /// </returns>
    private async Task ServeConnectionAsync(ITransportConnection connection, RequestServer server, CancellationToken cancellationToken)
    {
        // Reading the remote end point first (read once when the connection was accepted)
        string remoteEndPoint = connection.RemoteEndPoint.ToString() ?? "unknown";
        LogAccepted(remoteEndPoint);
        try
        {
            // Serving the connection to its end
            await server.ServeAsync(connection, cancellationToken).ConfigureAwait(false);
            LogClosed(remoteEndPoint);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // The host stops: not a failure
            LogClosed(remoteEndPoint);
        }
        catch (Exception exception)
        {
            // The connection rule of the codec: after any failure, the connection is closed, never reused
            LogConnectionFailed(exception, remoteEndPoint);
        }
        finally
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion

    #region Publics

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <summary>
    /// Method used to start listening, then let the host start its other services (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token signaled when the start must be aborted</param>
    /// <returns>
    /// A task that completes once the listener is started
    /// </returns>
    /// <exception cref="System.Net.Sockets.SocketException">If the address and port cannot be bound; the host then does not start</exception>
    /// <exception cref="InvalidOperationException">If the service is already started</exception>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Check
        if (_listener is not null)
            throw new InvalidOperationException("The listener is already started");

        // Starting the listener on the configured end point
        _listener = _startListener(new IPEndPoint(IPAddress.Parse(_options.ListenAddress), _options.Port));
        IPEndPoint localEndPoint = _listener.LocalEndPoint;
        if (IPAddress.IsLoopback(localEndPoint.Address))
            LogListening(localEndPoint);
        else
            LogNotLoopback(localEndPoint);

        // Function's return
        return base.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to accept the connections until the host stops (Asynchronous)
    /// </summary>
    /// <param name="stoppingToken">Token canceled when the host stops</param>
    /// <returns>
    /// A task that completes once the listener is disposed and every connection served to its end
    /// </returns>
    /// <exception cref="InvalidOperationException">If the listener is lost without a stop request, or the accept failures repeat; the host
    /// then stops</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Preparing the codec and the routing of the operations, shared by every connection
        ITransportListener listener = _listener ?? throw new InvalidOperationException("The listener is not started");
        FrameCodec codec = new FrameCodec();
        OperationDispatcher dispatcher = new OperationDispatcher().Register<PingRequest, PingResponse>(VeryBackOperations.Ping, PingAsync);
        RequestServer server = new RequestServer(codec, dispatcher.HandleAsync);
        int consecutiveFailures = 0;

        try
        {
            // Accepting connections until the host stops
            while (true)
            {
                ITransportConnection connection;
                try
                {
                    connection = await listener.AcceptAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is OperationCanceledException or ObjectDisposedException)
                {
                    // The accept loop rule: the tier stops; without a stop request, nothing would listen any more while the host runs
                    if (stoppingToken.IsCancellationRequested)
                        break;
                    LogListenerLost(exception);
                    throw new InvalidOperationException("The listener of the Very Back was closed without a stop request", exception);
                }
                catch (Exception exception)
                {
                    // The accept loop rule: this exception concerns one incoming connection, unless it repeats
                    consecutiveFailures++;
                    if (consecutiveFailures >= MaxConsecutiveAcceptFailures)
                    {
                        LogAcceptFailuresRepeated(exception, consecutiveFailures);
                        throw new InvalidOperationException($"{consecutiveFailures} accept failures in a row: the listener of the Very Back is broken", exception);
                    }

                    // Pausing before the next accept; the pause is cut short when the host stops
                    LogAcceptFailed(exception);
                    await Task.Delay(_acceptFailurePause, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                // Serving the connection in its own task, after forgetting the services that ended
                consecutiveFailures = 0;
                _connectionTasks.RemoveAll(task => task.IsCompleted);
                _connectionTasks.Add(Task.Run(() => ServeConnectionAsync(connection, server, stoppingToken), CancellationToken.None));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host stopped during the pause after an accept failure
        }
        finally
        {
            // Closing the listener, then waiting for the service of every connection: the stopping token ends them
            await listener.DisposeAsync().ConfigureAwait(false);
            LogStoppedListening();
            await Task.WhenAll(_connectionTasks).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Method used to dispose the listener when the service was started but never ran to its end (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token signaled when the stop must no longer be graceful</param>
    /// <returns>
    /// A task that completes once the service is stopped
    /// </returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await base.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Idempotent: the listener is already disposed when the accept loop ended
            if (_listener is not null)
                await _listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
