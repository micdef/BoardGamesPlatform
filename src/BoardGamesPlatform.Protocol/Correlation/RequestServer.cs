/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/RequestServer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link: answers the requests of one connection, several at a time
 */

// Using
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Correlation;

/// <summary>
/// Server used to answer the requests received on a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Back and the Very Back answer the requests of the tier in front of them, each response carrying the
/// correlation identifier of its request, so that the client can pair them whatever the order.</para>
/// <para><b>Behavior:</b> the connection has already passed its handshake (<see cref="ProtocolHandshake"/>). The server reads the frames one
/// after the other and never waits for a slot to read, so that a control message (a cancellation, a ping) is received at once, even when every
/// slot is busy; only a client that stops reading the answers written from the read loop (a pong, a "busy") can stall it, until the idle
/// timeout ends the connection. Each request is handed to the handler without waiting for the previous one, up to a maximum number handled at
/// the same time; beyond it, the request is answered at once with the code <see cref="ErrorCodes.ServerBusy"/>, without being started, so that
/// a peer cannot make the server start an unbounded amount of work. The frames are written one at a time.</para>
/// <list type="bullet">
/// <item><description>A ping of the client is answered with a pong; a connection on which nothing at all was received for the idle timeout
/// of <see cref="HeartbeatSettings"/> is given up, so that a dead client does not hold its slots.</description></item>
/// <item><description>Each request has its own token: a <see cref="CancelMessage"/> of the client cancels it, and a handler that ends with
/// that cancellation is not answered (the client already forgot the request). A handler that finishes anyway is answered; the client
/// ignores the response.</description></item>
/// <item><description>A request whose identifier is still in progress, a control message other than a cancellation or a ping, or a malformed
/// one, is a protocol violation.</description></item>
/// <item><description>A handler that throws ends the service of the connection, since a client must not wait for a response that will never
/// come. The handler of the tiers, <see cref="OperationDispatcher"/>, turns the failures of a request into an error code instead, so that it
/// only throws for a malformed envelope.</description></item>
/// </list>
/// </remarks>
public sealed class RequestServer
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of requests of one connection handled at the same time when none is given
    /// </summary>
    public const int DefaultMaxConcurrentRequests = 64;

    #endregion

    #region Variables

    /// <summary>
    /// Response given to a request when every slot is busy, written once
    /// </summary>
    private static readonly byte[] _busyResponse = MessageSerializer.WriteError(ErrorCodes.ServerBusy);

    /// <summary>
    /// Pong written for each ping, built once
    /// </summary>
    private static readonly byte[] _pong = MessageSerializer.WriteControl(HeartbeatMessage.Pong);

    /// <summary>
    /// Codec used to read the requests and write the responses
    /// </summary>
    private readonly FrameCodec _codec;

    /// <summary>
    /// Handler that turns the content of a request into the content of its response
    /// </summary>
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, Task<ReadOnlyMemory<byte>>> _handler;

    /// <summary>
    /// Number of requests of one connection handled at the same time, at most
    /// </summary>
    private readonly int _maxConcurrentRequests;

    /// <summary>
    /// Timings of the heartbeat
    /// </summary>
    private readonly HeartbeatSettings _heartbeat;

    /// <summary>
    /// Clock of the heartbeat (the system clock outside the tests)
    /// </summary>
    private readonly TimeProvider _timeProvider;

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
    /// Codec of the link, used by the handshake that opens each connection
    /// </summary>
    public FrameCodec Codec => _codec;

    /// <summary>
    /// Number of requests of one connection handled at the same time, at most; announced to the client by the handshake
    /// </summary>
    public int MaxConcurrentRequests => _maxConcurrentRequests;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="codec">[Cannot be null] Codec of the link; both ends use the same maximum length</param>
    /// <param name="handler">[Cannot be null] Handler of the requests: receives the content of a request and the token of that request,
    /// canceled when the client cancels it or when the service of the connection ends, and returns the content of the response. It may be
    /// called for several requests at the same time. It must observe the token: the service of a connection only ends once every handler has
    /// returned. Its response must not exceed the maximum length of the codec; a larger one is a handler failure.</param>
    /// <param name="maxConcurrentRequests">Number of requests of one connection handled at the same time, at most; 1 or more</param>
    /// <param name="heartbeat">[Can be null] Timings of the heartbeat; null for <see cref="HeartbeatSettings.Default"/></param>
    /// <param name="timeProvider">[Can be null] Clock of the heartbeat; null for the system clock</param>
    /// <exception cref="ArgumentNullException">If the codec or the handler is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the maximum number of requests is zero or negative</exception>
    public RequestServer(FrameCodec codec, Func<ReadOnlyMemory<byte>, CancellationToken, Task<ReadOnlyMemory<byte>>> handler,
        int maxConcurrentRequests = DefaultMaxConcurrentRequests, HeartbeatSettings? heartbeat = null, TimeProvider? timeProvider = null)
    {
        // Checks
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");
        if (handler is null)
            throw new ArgumentNullException(nameof(handler), $"The value of the parameter: {nameof(handler)} cannot be null");
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), maxConcurrentRequests, $"The value of the parameter: {nameof(maxConcurrentRequests)} must be greater than zero");

        // Setting
        _codec = codec;
        _handler = handler;
        _maxConcurrentRequests = maxConcurrentRequests;
        _heartbeat = heartbeat ?? HeartbeatSettings.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to write one frame, when no other frame of the connection is being written
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to write on</param>
    /// <param name="frame">[Cannot be null] Frame to write</param>
    /// <param name="service">[Cannot be null] State shared by the requests of the connection</param>
    /// <returns>
    /// A task that completes once the frame is written
    /// </returns>
    private async Task WriteAsync(ITransportConnection connection, Frame frame, ServedConnectionState service)
    {
        // Writing under the write lock; the end of the service stops the wait and the write
        await service.WriteLock.WaitAsync(service.Token).ConfigureAwait(false);
        try
        {
            await _codec.WriteAsync(connection.Stream, frame, service.Token).ConfigureAwait(false);
        }
        finally
        {
            service.WriteLock.Release();
        }
    }

    /// <summary>
    /// Method used to handle one request and write its response
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: a failure of the handler or of the write is recorded in <paramref name="service"/>, which ends the
    /// service of the connection. A handler that ends with the cancellation of its request by the client is not answered. The request is always
    /// forgotten and its slot given back.</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection to answer on</param>
    /// <param name="request">[Cannot be null] Request to handle</param>
    /// <param name="requestToken">Token of the request</param>
    /// <param name="service">[Cannot be null] State shared by the requests of the connection</param>
    /// <returns>
    /// A task that completes when the response is written, or the request is canceled or has failed
    /// </returns>
    private async Task HandleRequestAsync(ITransportConnection connection, Frame request, ServedConnectionState service, CancellationToken requestToken)
    {
        bool isEnded = false;
        try
        {
            // Leaving the read loop first: the handler then never delays the reading of the next frames, even before its first await
            await Task.Yield();

            // Running the handler; a cancellation asked by the client, the service going on, is not answered
            ReadOnlyMemory<byte> response;
            try
            {
                response = await _handler(request.Payload, requestToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (requestToken.IsCancellationRequested && !service.Token.IsCancellationRequested)
            {
                // Function's return: nothing to answer
                return;
            }

            // Writing the response under the identifier of the request; the slot is given back once the write lock is held, just before the
            // write: the client may send its next request as soon as it reads this response, and must find a free slot, while at most one
            // response at a time is held without a slot
            await service.WriteLock.WaitAsync(service.Token).ConfigureAwait(false);
            try
            {
                service.EndRequest(request.CorrelationId);
                service.Slots.Release();
                isEnded = true;
                await _codec.WriteAsync(connection.Stream, new Frame(request.CorrelationId, response), service.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                // Recording the failure while the write lock is still held: the end of the service waits for this lock, so it always sees
                // the failure, and the state it shares is not yet released
                service.Fail(exception);
            }
            finally
            {
                service.WriteLock.Release();
            }
        }
        catch (Exception exception)
        {
            // Ending the service of the connection (a cancellation caused by an earlier failure keeps that failure as the reason)
            service.Fail(exception);
        }
        finally
        {
            // Forgetting the request and giving its slot back, unless it was done before the write
            if (!isEnded)
            {
                service.EndRequest(request.CorrelationId);
                service.Slots.Release();
            }
        }
    }

    /// <summary>
    /// Method used to act on a control message received after the handshake
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to answer on</param>
    /// <param name="frame">[Cannot be null] Frame with the reserved identifier</param>
    /// <param name="service">[Cannot be null] State shared by the requests of the connection</param>
    /// <returns>
    /// A task that completes once the message is handled
    /// </returns>
    /// <exception cref="InvalidDataException">If the message is malformed, or is not one the client may send after the handshake</exception>
    private async Task HandleControlAsync(ITransportConnection connection, Frame frame, ServedConnectionState service)
    {
        // Reading the message: a cancellation or a ping is expected
        ControlMessage message = MessageSerializer.ReadControl(frame.Payload.Span);
        switch (message)
        {
            // Canceling the request, if it is still in progress, outside the read loop: the callbacks of the handler may block
            case CancelMessage cancel:
                service.StartCancelRequest(cancel.CorrelationId);
                break;

            // Answering the ping, like any frame
            case HeartbeatMessage { Control: ControlKinds.Ping }:
                await WriteAsync(connection, new Frame(Frame.NoCorrelation, _pong), service).ConfigureAwait(false);
                break;

            default:
                throw new InvalidDataException("The client sent a control message that is not expected after the handshake");
        }
    }

    /// <summary>
    /// Method used to end the service of a connection on which nothing was received for too long
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> checks at each ping interval the time since the last frame received; beyond the server idle timeout, the service
    /// fails with a <see cref="TimeoutException"/>, which cancels the read and the requests in progress (the caller then closes the connection,
    /// following the connection rule of the codec). Never throws.</para>
    /// </remarks>
    /// <param name="service">[Cannot be null] State shared by the requests of the connection</param>
    /// <param name="cancellationToken">Token canceled when the read loop has ended</param>
    /// <returns>
    /// A task that completes when the watch stops
    /// </returns>
    private async Task WatchIdleAsync(ServedConnectionState service, CancellationToken cancellationToken)
    {
        try
        {
            using PeriodicTimer timer = new PeriodicTimer(_heartbeat.PingInterval, _timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_timeProvider.GetElapsedTime(service.LastReceived) > _heartbeat.ServerIdleTimeout)
                {
                    service.Fail(new TimeoutException($"The client sent no frame for more than {_heartbeat.ServerIdleTimeout}"));

                    // Function's return: the service ends
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The read loop has ended
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to answer the requests of a connection until it ends
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> returns when the peer closes the connection between two frames, once every request in progress has ended:
    /// they are still answered, for a client that closed only its sending side, but those still running after the idle timeout of the
    /// heartbeat are canceled. When anything else ends the service (a protocol violation, a failure of the stream, a failing handler, the cancellation), the requests still
    /// in progress are canceled and awaited, then the cause is thrown. In every case, the caller then disposes the connection.</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection to serve, its handshake done; it stays owned by the caller</param>
    /// <param name="cancellationToken">Token used to stop serving, for example when the tier stops</param>
    /// <returns>
    /// A task that completes when the connection is served to its end
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="InvalidDataException">If the peer breaks the protocol (a malformed frame or control message, an identifier in progress)</exception>
    /// <exception cref="IOException">If the stream fails</exception>
    /// <exception cref="TimeoutException">If the client sent no frame for the idle timeout of the heartbeat</exception>
    /// <exception cref="OperationCanceledException">If the service is canceled</exception>
    /// <exception cref="Exception">Whatever a handler throws</exception>
    public async Task ServeAsync(ITransportConnection connection, CancellationToken cancellationToken = default)
    {
        // Check
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");

        // Reading the frames until the peer closes or the service fails: never waiting for a slot, so that the control messages always arrive
        using ServedConnectionState service = new ServedConnectionState(_maxConcurrentRequests, _timeProvider.GetTimestamp(), cancellationToken);
        using CancellationTokenSource watching = CancellationTokenSource.CreateLinkedTokenSource(service.Token);
        Task watchdog = WatchIdleAsync(service, watching.Token);
        try
        {
            while (true)
            {
                Frame? frame = await _codec.ReadAsync(connection.Stream, service.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    // The client sends nothing more: the requests in progress may still be answered (a client that closed only its sending
                    // side still reads), but within the idle timeout, after which they are canceled, so that a connection never stays held
                    service.EndOnPeerClose(_heartbeat.ServerIdleTimeout, _timeProvider);
                    break;
                }
                service.MarkReceived(_timeProvider.GetTimestamp());

                // A control message is handled at once, in the read loop
                if (frame.CorrelationId == Frame.NoCorrelation)
                {
                    await HandleControlAsync(connection, frame, service).ConfigureAwait(false);
                    continue;
                }

                // An identifier still in progress is refused first: its response would reach the wrong request
                if (!service.TryStartRequest(frame.CorrelationId, out CancellationToken requestToken))
                    throw new InvalidDataException($"The peer sent the correlation identifier {frame.CorrelationId}, already in progress");

                // A request without a free slot is answered "busy" at once, without being started
                if (!service.Slots.Wait(0, CancellationToken.None))
                {
                    service.EndRequest(frame.CorrelationId);
                    await WriteAsync(connection, new Frame(frame.CorrelationId, _busyResponse), service).ConfigureAwait(false);
                    continue;
                }

                // The request runs in its own task, which forgets it and gives its slot back when it ends
                _ = HandleRequestAsync(connection, frame, service, requestToken);
            }
        }
        catch (Exception exception)
        {
            service.Fail(exception);
        }

        // Stopping the idle watch, then waiting for the cancellations asked by the client: the read loop has ended and starts none any more
        await watching.CancelAsync().ConfigureAwait(false);
        await watchdog.ConfigureAwait(false);
        await service.WhenCancellationsEndedAsync().ConfigureAwait(false);

        // Waiting for the requests in progress (each one gives its slot back), then for the last response being written, which holds the
        // write lock without a slot: once both are taken, nothing of this connection runs any more
        for (int slot = 0; slot < _maxConcurrentRequests; slot++)
            await service.Slots.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        await service.WriteLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        // Reporting why the service ended, unless the peer simply closed the connection
        service.ThrowIfFailed();
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
