/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/RequestClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link: sends requests on one connection and pairs each response with its request
 */

// Using
using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using BoardGamesPlatform.Protocol.Control;
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
/// Client used to send requests on one connection, several at a time, and to receive their responses
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Gateway and the Back send many requests on a single connection, each caller waiting only for its own response,
/// whatever the order in which the peer answers.</para>
/// <para><b>Behavior:</b> each request gets a new correlation identifier (1, 2, 3…; never <see cref="Frame.NoCorrelation"/>). One read loop
/// receives the responses and completes the matching request. The writes are serialized, since the frame codec allows one writer at a time.</para>
/// <list type="bullet">
/// <item><description>Canceling a request stops the wait at once, and, once its frame is written, sends the server a
/// <see cref="CancelMessage"/>, so that the work of the request stops in the next tiers too. A frame being written is always finished, so
/// that the stream stays intact; a response that arrives after the cancellation is ignored.</description></item>
/// <item><description>A response to an identifier never sent, or a frame with the reserved identifier, is a protocol violation: the connection
/// is closed. The check compares with the last identifier given to a request, not with the last one written: a peer that guesses the
/// identifier of a request still waiting for the write lock could answer it early. The links join tiers of the platform only, and mutual
/// TLS authenticates the peer from 0.5.0.</description></item>
/// <item><description>The client pings the server at a fixed interval (<see cref="HeartbeatSettings"/>) and closes when no frame at all
/// came back within its timeout: a dead server or a broken path is found even when no request flows.</description></item>
/// <item><description>When the connection closes (peer, failure, violation, heartbeat or disposal), the client closes for good: every pending and later
/// request fails. A new connection needs a new client: the link client opens one on the next call.</description></item>
/// </list>
/// </remarks>
public sealed class RequestClient : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Ping written at each tick of the heartbeat, built once
    /// </summary>
    private static readonly byte[] _ping = MessageSerializer.WriteControl(HeartbeatMessage.Ping);

    /// <summary>
    /// Connection the requests travel on, owned by the client
    /// </summary>
    private readonly ITransportConnection _connection;

    /// <summary>
    /// Codec used to write the requests and read the responses
    /// </summary>
    private readonly FrameCodec _codec;

    /// <summary>
    /// Requests waiting for their response, by correlation identifier
    /// </summary>
    private readonly ConcurrentDictionary<ulong, TaskCompletionSource<ReadOnlyMemory<byte>>> _pendingRequests;

    /// <summary>
    /// Lock that makes "register a request" and "close the client" exclusive, so that no request is registered after the pending ones failed
    /// </summary>
    private readonly Lock _stateLock;

    /// <summary>
    /// Lock that lets one request at a time write its frame
    /// </summary>
    private readonly SemaphoreSlim _writeLock;

    /// <summary>
    /// Places of the requests in progress, as many as the server handles at the same time; it is never disposed, for the same reason as the
    /// write lock (a caller may still wait for it while the client closes)
    /// </summary>
    private readonly SemaphoreSlim _requestSlots;

    /// <summary>
    /// Source canceled when the client closes, which stops the read loop and any write in progress; it is never disposed, since a request may
    /// still read its token while the client closes, and a source without timer nor link holds no system resource
    /// </summary>
    private readonly CancellationTokenSource _lifetime;

    /// <summary>
    /// Read loop, started by the constructor
    /// </summary>
    private readonly Task _readLoop;

    /// <summary>
    /// Timings of the heartbeat
    /// </summary>
    private readonly HeartbeatSettings _heartbeat;

    /// <summary>
    /// Clock of the heartbeat (the system clock outside the tests)
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Heartbeat loop, started by the constructor: pings the server and closes the client when nothing comes back
    /// </summary>
    private readonly Task _heartbeatLoop;

    /// <summary>
    /// Time stamp of the clock when the last frame was received (the opening of the client before the first one)
    /// </summary>
    private long _lastReceived;

    /// <summary>
    /// Completed once the client is closed for good: connection disposed and every pending request failed
    /// </summary>
    private readonly TaskCompletionSource _closed;

    /// <summary>
    /// Last correlation identifier given to a request; 0 before the first one (it would take 2^63 requests on one connection to wrap)
    /// </summary>
    private long _lastCorrelationId;

    /// <summary>
    /// Failure to close the connection met by a cancellation sent apart from any caller; thrown by <see cref="DisposeAsync"/> when it was
    /// recorded before the disposal ends (a close still running at that moment can record it too late); null while none happened
    /// </summary>
    private Exception? _detachedCloseFailure;

    /// <summary>
    /// Why the client closed; null while it is open
    /// </summary>
    private Exception? _closeReason;

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

    /// <summary>
    /// Task that completes when the client is closed for good (connection disposed, every pending request failed), whatever the reason;
    /// it never fails, so that it can be awaited safely
    /// </summary>
    public Task Completion => _closed.Task;

    /// <summary>
    /// Whether the client is still open: false from the very start of its close, before its pending requests fail and before
    /// <see cref="Completion"/> completes, so that a caller never picks a client that is closing
    /// </summary>
    public bool IsOpen => Volatile.Read(ref _closeReason) is null;

    /// <summary>
    /// Why the client closed (the peer, a failure, a violation, the heartbeat, the disposal), for the logs of the caller; null while it is open
    /// </summary>
    public Exception? CloseReason => Volatile.Read(ref _closeReason);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; starts reading the responses at once
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection; the client takes ownership of it and disposes it when it closes</param>
    /// <param name="codec">[Cannot be null] Codec of the link; both ends use the same maximum length</param>
    /// <param name="maxConcurrentRequests">Number of requests in progress at the same time, at most: the limit the server announced in the
    /// handshake (beyond it, the server would answer "busy"); 1 or more</param>
    /// <param name="heartbeat">[Can be null] Timings of the heartbeat; null for <see cref="HeartbeatSettings.Default"/></param>
    /// <param name="timeProvider">[Can be null] Clock of the heartbeat; null for the system clock</param>
    /// <exception cref="ArgumentNullException">If the connection or the codec is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the maximum number of requests is zero or negative</exception>
    public RequestClient(ITransportConnection connection, FrameCodec codec, int maxConcurrentRequests = RequestServer.DefaultMaxConcurrentRequests,
        HeartbeatSettings? heartbeat = null, TimeProvider? timeProvider = null)
    {
        // Checks
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), maxConcurrentRequests, $"The value of the parameter: {nameof(maxConcurrentRequests)} must be greater than zero");

        // Setting
        _connection = connection;
        _codec = codec;
        _pendingRequests = new ConcurrentDictionary<ulong, TaskCompletionSource<ReadOnlyMemory<byte>>>();
        _stateLock = new Lock();
        _writeLock = new SemaphoreSlim(1, 1);
        _requestSlots = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        _lifetime = new CancellationTokenSource();
        _closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _heartbeat = heartbeat ?? HeartbeatSettings.Default;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _lastReceived = _timeProvider.GetTimestamp();

        // Starting the read loop and the heartbeat
        _readLoop = ReadResponsesAsync();
        _heartbeatLoop = HeartbeatAsync();
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to receive the responses and complete the matching requests, until the connection closes
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> whatever ends the loop becomes the close reason of the client. The task only fails when disposing the connection
    /// throws; that error is then thrown by <see cref="DisposeAsync"/>, which awaits this task.</para>
    /// </remarks>
    /// <returns>
    /// A task that completes when the client is closed
    /// </returns>
    private async Task ReadResponsesAsync()
    {
        // Letting the constructor return before the first read
        await Task.Yield();

        // Reading until the connection ends
        Exception reason;
        try
        {
            while (true)
            {
                Frame? frame = await _codec.ReadAsync(_connection.Stream, _lifetime.Token).ConfigureAwait(false);
                if (frame is null)
                {
                    reason = new IOException("The peer closed the connection");
                    break;
                }

                // Any frame proves that the server is there
                Interlocked.Exchange(ref _lastReceived, _timeProvider.GetTimestamp());

                // A control message from the server can only be a pong, which needs nothing more
                if (frame.CorrelationId == Frame.NoCorrelation)
                {
                    if (MessageSerializer.ReadControl(frame.Payload.Span) is not HeartbeatMessage { Control: ControlKinds.Pong })
                        throw new InvalidDataException("The peer sent a control message that is not expected after the handshake");
                    continue;
                }

                // Refusing the identifiers never sent
                if (frame.CorrelationId > (ulong)Interlocked.Read(ref _lastCorrelationId))
                    throw new InvalidDataException($"The peer answered the correlation identifier {frame.CorrelationId}, which was never sent");

                // Completing the request; a response to a canceled request finds nobody and is ignored
                if (_pendingRequests.TryRemove(frame.CorrelationId, out TaskCompletionSource<ReadOnlyMemory<byte>>? request))
                    request.TrySetResult(frame.Payload);
            }
        }
        catch (Exception exception)
        {
            reason = exception;
        }

        // Closing the client for good
        await CloseAsync(reason).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to ping the server at each interval, and to close the client when no frame came back in time
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> at each tick, the client first checks the time since the last frame received: beyond the client timeout, the
    /// server is taken as dead and the client closes with a <see cref="TimeoutException"/>. Otherwise it writes a ping, like any frame, under
    /// the write lock; that write is bounded by the client timeout too, so that a peer that no longer reads cannot block the heartbeat. A read
    /// is never canceled here: closing the client ends the read loop, following the connection rule of the codec. The task only fails when
    /// disposing the connection throws; that error is then thrown by <see cref="DisposeAsync"/>.</para>
    /// </remarks>
    /// <returns>
    /// A task that completes when the client is closed
    /// </returns>
    private async Task HeartbeatAsync()
    {
        Exception? reason = null;
        try
        {
            using PeriodicTimer timer = new PeriodicTimer(_heartbeat.PingInterval, _timeProvider);
            while (await timer.WaitForNextTickAsync(_lifetime.Token).ConfigureAwait(false))
            {
                // Giving up on a server that sent nothing for too long
                if (_timeProvider.GetElapsedTime(Interlocked.Read(ref _lastReceived)) > _heartbeat.ClientTimeout)
                {
                    reason = new TimeoutException($"The server sent no frame for more than {_heartbeat.ClientTimeout}");
                    break;
                }

                // Pinging, within the client timeout
                using CancellationTokenSource deadline = new CancellationTokenSource(_heartbeat.ClientTimeout, _timeProvider);
                using CancellationTokenSource writing = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token, deadline.Token);
                try
                {
                    await _writeLock.WaitAsync(writing.Token).ConfigureAwait(false);
                    try
                    {
                        await _codec.WriteAsync(_connection.Stream, new Frame(Frame.NoCorrelation, _ping), writing.Token)
                            .ConfigureAwait(false);
                    }
                    finally
                    {
                        _writeLock.Release();
                    }
                }
                catch (OperationCanceledException exception) when (deadline.IsCancellationRequested && !_lifetime.IsCancellationRequested)
                {
                    reason = new TimeoutException($"The ping could not be written within {_heartbeat.ClientTimeout}", exception);
                    break;
                }
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The client closes for another reason
        }
        catch (Exception exception)
        {
            // A failed write leaves the stream in an unknown state
            reason = exception;
        }

        // Closing the client when the heartbeat found it broken
        if (reason is not null)
            await CloseAsync(reason).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to close the client once, and to fail every pending request with the reason
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the first call closes; any later call waits until that first one has finished, so that no caller goes on while
    /// the connection is still being closed. The pending requests are failed and <see cref="Completion"/> completes in every case; an error
    /// while disposing the connection is then thrown to the caller that closed (it is never hidden).</para>
    /// </remarks>
    /// <param name="reason">[Cannot be null] Why the client closes</param>
    /// <returns>
    /// A task that completes when the client is closed for good
    /// </returns>
    private async Task CloseAsync(Exception reason)
    {
        // Keeping the first reason only, and taking the pending requests: none can be registered after this point
        List<TaskCompletionSource<ReadOnlyMemory<byte>>> pendingRequests = new List<TaskCompletionSource<ReadOnlyMemory<byte>>>();
        bool isFirstClose;
        lock (_stateLock)
        {
            isFirstClose = _closeReason is null;
            if (isFirstClose)
            {
                _closeReason = reason;
                foreach (ulong correlationId in _pendingRequests.Keys)
                {
                    if (_pendingRequests.TryRemove(correlationId, out TaskCompletionSource<ReadOnlyMemory<byte>>? request))
                        pendingRequests.Add(request);
                }
            }
        }

        // Waiting for the first close when another caller started it
        if (!isFirstClose)
        {
            await _closed.Task.ConfigureAwait(false);
            return;
        }

        // Stopping the read loop and any write, then closing the connection (connection rule of the codec)
        try
        {
            await _lifetime.CancelAsync().ConfigureAwait(false);
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            // Failing the pending requests, then telling that the client is closed
            foreach (TaskCompletionSource<ReadOnlyMemory<byte>> request in pendingRequests)
                request.TrySetException(CreateClosedException());
            _closed.TrySetResult();
        }
    }

    /// <summary>
    /// Method used to tell the server that a request was abandoned, so that it stops its work
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> best effort, run apart from the caller who canceled: the message waits for the write lock like any frame. When
    /// the client closes meanwhile, nothing is sent (the server then cancels the requests still running after its idle timeout). A failed write
    /// closes the client, following the connection rule of the codec; a failure of that close is thrown by <see cref="DisposeAsync"/>. The task
    /// itself never fails.</para>
    /// </remarks>
    /// <param name="correlationId">Correlation identifier of the abandoned request</param>
    /// <returns>
    /// A task that completes once the message is written, or the client closed
    /// </returns>
    private async Task SendCancelAsync(ulong correlationId)
    {
        try
        {
            // Writing the cancellation under the write lock, as long as the client is open
            await _writeLock.WaitAsync(_lifetime.Token).ConfigureAwait(false);
            try
            {
                byte[] cancel = MessageSerializer.WriteControl(new CancelMessage(correlationId));
                await _codec.WriteAsync(_connection.Stream, new Frame(Frame.NoCorrelation, cancel), _lifetime.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
            // The client closes: the server sees the connection end, and cancels the requests still running after its idle timeout
        }
        catch (Exception exception)
        {
            // A failed write leaves the stream in an unknown state: the client closes (it completes in every case); a failure of that close
            // has no caller here, so it is kept for DisposeAsync
            try
            {
                await CloseAsync(exception).ConfigureAwait(false);
            }
            catch (Exception closeFailure)
            {
                Interlocked.CompareExchange(ref _detachedCloseFailure, closeFailure, null);
            }
        }
    }

    /// <summary>
    /// Method used to build the exception given to a request when the client is closed
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the type follows the close reason, not the moment of the call: a request failed by a real error keeps an
    /// <see cref="IOException"/> even when the client is disposed right after.</para>
    /// </remarks>
    /// <returns>
    /// An <see cref="ObjectDisposedException"/> when the client was closed by its disposal, else an <see cref="IOException"/> holding the close reason
    /// </returns>
    private Exception CreateClosedException()
    {
        // Function's return
        return _closeReason is ObjectDisposedException
            ? new ObjectDisposedException(nameof(RequestClient), "The request client has been disposed")
            : new IOException("The connection of the request client is closed", _closeReason);
    }

    /// <summary>
    /// Method used to send a request that holds a place, and wait for its response
    /// </summary>
    /// <param name="payload">Content of the request</param>
    /// <param name="cancellationToken">Token used to stop waiting</param>
    /// <returns>
    /// The content of the response
    /// </returns>
    private async Task<ReadOnlyMemory<byte>> SendInPlaceAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
    {
        // Registering the request under a new identifier, unless the client is closed
        TaskCompletionSource<ReadOnlyMemory<byte>> request = new TaskCompletionSource<ReadOnlyMemory<byte>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ulong correlationId;
        lock (_stateLock)
        {
            if (_closeReason is not null)
                throw CreateClosedException();
            correlationId = (ulong)Interlocked.Increment(ref _lastCorrelationId);
            _pendingRequests[correlationId] = request;
        }

        // Stopping the wait on cancellation; the request is forgotten, so that a late response is ignored, and the server is told to stop
        // once the request is written (0: not written, 1: written, 2: cancellation sent; the cancellation is sent once, by whoever comes last)
        int cancelState = 0;
        using CancellationTokenRegistration cancellation = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(correlationId, out TaskCompletionSource<ReadOnlyMemory<byte>>? canceled))
                canceled.TrySetCanceled(cancellationToken);
            if (Interlocked.CompareExchange(ref cancelState, 2, 1) == 1)
                _ = SendCancelAsync(correlationId);
        });

        // Waiting for the write lock; this wait can be canceled, since nothing is written yet
        try
        {
            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ending the request as canceled; when a close already failed it, its failure is marked as observed, since this caller
            // leaves with the cancellation and nobody else will await it
            _pendingRequests.TryRemove(correlationId, out _);
            if (!request.TrySetCanceled(cancellationToken))
                _ = request.Task.Exception;
            throw;
        }

        // Writing the request, unless it already ended (canceled, or failed by a close) while waiting for the lock;
        // the write itself only stops when the client closes
        bool isWritten = false;
        try
        {
            if (!request.Task.IsCompleted)
            {
                await _codec.WriteAsync(_connection.Stream, new Frame(correlationId, payload), _lifetime.Token).ConfigureAwait(false);
                isWritten = true;
            }
        }
        catch (Exception exception)
        {
            // A failed write leaves the stream in an unknown state: the client closes, which fails this request too
            await CloseAsync(exception).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }

        // Telling the server to stop when the caller canceled while the request was being written
        if (isWritten && Interlocked.CompareExchange(ref cancelState, 1, 0) == 0 && request.Task.IsCanceled
            && Interlocked.CompareExchange(ref cancelState, 2, 1) == 1)
        {
            _ = SendCancelAsync(correlationId);
        }

        // Function's return
        return await request.Task.ConfigureAwait(false);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to send a request and wait for its response
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> waits for a place among the requests in progress (the limit of the server), gives the request a new correlation
    /// identifier, writes it when no other frame is being written, then waits for the response. Several calls may run at the same time. A
    /// canceled request gives its place back at once, while the server may still take a moment to see its cancellation: a request sent just
    /// then can be answered "busy".</para>
    /// </remarks>
    /// <param name="payload">Content of the request; it must not be modified until the method returns</param>
    /// <param name="cancellationToken">Token used to stop waiting, for a place as for the response; it never interrupts a frame being written</param>
    /// <returns>
    /// The content of the response
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="IOException">If the connection is closed, or closes before the response arrives</exception>
    /// <exception cref="ArgumentException">If the payload is larger than the maximum length of the codec</exception>
    /// <exception cref="OperationCanceledException">If the wait is canceled</exception>
    public async Task<ReadOnlyMemory<byte>> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        // Check
        if (payload.Length > _codec.MaxPayloadLength)
            throw new ArgumentException($"The payload of the request ({payload.Length} bytes) is larger than the maximum length ({_codec.MaxPayloadLength} bytes)", nameof(payload));

        // Holding a place for the whole request
        await _requestSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Function's return
            return await SendInPlaceAsync(payload, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _requestSlots.Release();
        }
    }


    /// <summary>
    /// Method used to close the client and its connection; the pending requests fail with an <see cref="ObjectDisposedException"/>
    /// </summary>
    /// <returns>
    /// A task that completes when the client is closed for good and its read loop has ended
    /// </returns>
    /// <exception cref="Exception">Whatever closing the connection threw, when no caller could see it (a close started by a cancellation
    /// sent apart from any caller)</exception>
    public async ValueTask DisposeAsync()
    {
        // Disposing once; a second call waits for the first one
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            await _readLoop.ConfigureAwait(false);
            await _heartbeatLoop.ConfigureAwait(false);
            return;
        }

        // Closing, then waiting for the read loop and the heartbeat
        await CloseAsync(new ObjectDisposedException(nameof(RequestClient), "The request client has been disposed")).ConfigureAwait(false);
        await _readLoop.ConfigureAwait(false);
        await _heartbeatLoop.ConfigureAwait(false);

        // Throwing the failure of a close that no caller could see
        Exception? detached = Volatile.Read(ref _detachedCloseFailure);
        if (detached is not null)
            ExceptionDispatchInfo.Capture(detached).Throw();
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
