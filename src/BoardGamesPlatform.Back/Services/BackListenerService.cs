/*
 *	@file	src/BoardGamesPlatform.Back/Services/BackListenerService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Listens for the connections of the Gateway and answers their requests
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Back.Settings;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Services;

/// <summary>
/// Service used to accept the connections of the Gateway and answer their requests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the server side of the link Gateway to Back (plain TCP in 0.3.0 and 0.4.0, development only).</para>
/// <para><b>Behavior:</b> the listener is started when the host starts, so that a port already in use or an invalid endpoint stops the host
/// at once, and <see cref="LocalEndPoint"/> is known as soon as the start has returned. A listen address other than loopback is accepted (the
/// tiers may run on separate machines) but logged as a warning, since the link is neither encrypted nor authenticated yet.</para>
/// <para>The connections are accepted and served by a <see cref="LinkServer"/>, which never ends silently: a listener lost without a stop
/// request, or <see cref="MaxConsecutiveAcceptFailures"/> accept failures in a row, fail the service, so that the host stops instead of
/// running without listening. Each connection is served on its own: a failure of one connection never reaches the others.</para>
/// <para>The Ping operation asks the Very Back for the time of the database server, through the shared client of the Very Back, and answers
/// it unchanged, with its offset. When the Very Back cannot be reached, the request fails: until the error codes of 0.4.0, the connection of
/// the Gateway is then closed, and the Gateway opens a new one on its next call.</para>
/// </remarks>
public sealed partial class BackListenerService : BackgroundService
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of accept failures in a row after which the listener is considered broken and the service fails
    /// </summary>
    public const int MaxConsecutiveAcceptFailures = LinkServer.MaxConsecutiveAcceptFailures;

    /// <summary>
    /// Name of the tier in the logs
    /// </summary>
    private const string TierName = "the Back";

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the link; null until the service is started
    /// </summary>
    private ITransportListener? _listener;

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
    private readonly BackOptions _options;

    /// <summary>
    /// Client of the Very Back, shared by every connection
    /// </summary>
    private readonly VeryBackClient _svcVeryBackClient;

    /// <summary>
    /// Logger of the service
    /// </summary>
    private readonly ILogger<BackListenerService> _logger;

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
    /// <param name="svcVeryBackClient">[Cannot be null] Client of the Very Back</param>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public BackListenerService(IOptions<BackOptions> options, VeryBackClient svcVeryBackClient, ILogger<BackListenerService> logger)
        : this(options, svcVeryBackClient, logger, endPoint => TcpTransportListener.Start(endPoint), LinkServer.DefaultAcceptFailurePause)
    {
    }

    /// <summary>
    /// Constructor with the function that starts the listener and the pause after an accept failure, so that the tests can make the accept
    /// loop fail quickly
    /// </summary>
    /// <param name="options">[Cannot be null] Settings of the listener, validated at startup</param>
    /// <param name="svcVeryBackClient">[Cannot be null] Client of the Very Back</param>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <param name="startListener">[Cannot be null] Function that starts the listener on an end point</param>
    /// <param name="acceptFailurePause">Pause after an accept failure; zero or more</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the pause is negative</exception>
    internal BackListenerService(IOptions<BackOptions> options, VeryBackClient svcVeryBackClient, ILogger<BackListenerService> logger,
        Func<IPEndPoint, ITransportListener> startListener, TimeSpan acceptFailurePause)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (svcVeryBackClient is null)
            throw new ArgumentNullException(nameof(svcVeryBackClient), $"The value of the parameter: {nameof(svcVeryBackClient)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (startListener is null)
            throw new ArgumentNullException(nameof(startListener), $"The value of the parameter: {nameof(startListener)} cannot be null");
        if (acceptFailurePause < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(acceptFailurePause), $"The value of the parameter: {nameof(acceptFailurePause)} cannot be negative");

        // Setting services
        _options = options.Value;
        _svcVeryBackClient = svcVeryBackClient;
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
    [LoggerMessage(EventId = 10, Level = LogLevel.Information, Message = "Back listening on {EndPoint}")]
    private partial void LogListening(IPEndPoint endPoint);

    /// <summary>
    /// Method used to warn that the Back listens on an address that other machines can reach
    /// </summary>
    /// <param name="endPoint">Address and port listened to</param>
    [LoggerMessage(EventId = 16, Level = LogLevel.Warning,
        Message = "Back listening on {EndPoint}, which is not a loopback address: the link is neither encrypted nor authenticated yet, restrict it with the firewall")]
    private partial void LogNotLoopback(IPEndPoint endPoint);

    /// <summary>
    /// Method used to answer a Ping request with the time of the database server, asked to the Very Back
    /// </summary>
    /// <param name="request">Body of the request, which holds nothing</param>
    /// <param name="cancellationToken">Token of the service of the connection</param>
    /// <returns>
    /// The response, holding the server time with its offset
    /// </returns>
    private async Task<PingResponse> PingAsync(PingRequest request, CancellationToken cancellationToken)
    {
        // Asking the Very Back (its response type belongs to the contracts of the Very Back, read through its client)
        DateTimeOffset serverTime = (await _svcVeryBackClient.PingAsync(cancellationToken).ConfigureAwait(false)).ServerTime;

        // Function's return
        return new PingResponse(serverTime);
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
    /// Method used to accept and serve the connections until the host stops (Asynchronous)
    /// </summary>
    /// <param name="stoppingToken">Token canceled when the host stops</param>
    /// <returns>
    /// A task that completes once the listener is disposed and every connection served to its end
    /// </returns>
    /// <exception cref="InvalidOperationException">If the listener is lost without a stop request, or the accept failures repeat; the host
    /// then stops</exception>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Routing the operations of the Back, then serving the connections
        ITransportListener listener = _listener ?? throw new InvalidOperationException("The listener is not started");
        OperationDispatcher dispatcher = new OperationDispatcher().Register<PingRequest, PingResponse>(BackOperations.Ping, PingAsync);
        RequestServer server = new RequestServer(new FrameCodec(), dispatcher.HandleAsync);
        LinkServer linkServer = new LinkServer(listener, server, TierName, _logger, _acceptFailurePause);
        await linkServer.RunAsync(stoppingToken).ConfigureAwait(false);
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
