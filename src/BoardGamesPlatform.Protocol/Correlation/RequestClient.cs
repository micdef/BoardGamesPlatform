/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/RequestClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link: sends requests on one connection and pairs each response with its request
 */

// Using
using System.Collections.Concurrent;
using BoardGamesPlatform.Protocol.Framing;
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
/// <item><description>Canceling a request stops the wait only: a frame being written is always finished, so that the stream stays intact;
/// a response that arrives after the cancellation is ignored.</description></item>
/// <item><description>A response to an identifier never sent, or a frame with the reserved identifier, is a protocol violation: the connection
/// is closed. The check compares with the last identifier given to a request, not with the last one written: a peer that guesses the
/// identifier of a request still waiting for the write lock could answer it early. The links join tiers of the platform only, and mutual
/// TLS authenticates the peer from 0.5.0.</description></item>
/// <item><description>When the connection closes (peer, failure, violation or disposal), the client closes for good: every pending and later
/// request fails. A new connection needs a new client (reconnection comes with 0.4.0).</description></item>
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
    /// Source canceled when the client closes, which stops the read loop and any write in progress; it is never disposed, since a request may
    /// still read its token while the client closes, and a source without timer nor link holds no system resource
    /// </summary>
    private readonly CancellationTokenSource _lifetime;

    /// <summary>
    /// Read loop, started by the constructor
    /// </summary>
    private readonly Task _readLoop;

    /// <summary>
    /// Completed once the client is closed for good: connection disposed and every pending request failed
    /// </summary>
    private readonly TaskCompletionSource _closed;

    /// <summary>
    /// Last correlation identifier given to a request; 0 before the first one (it would take 2^63 requests on one connection to wrap)
    /// </summary>
    private long _lastCorrelationId;

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
    /// <exception cref="ArgumentNullException">If the connection or the codec is null</exception>
    public RequestClient(ITransportConnection connection, FrameCodec codec)
    {
        // Checks
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");

        // Setting
        _connection = connection;
        _codec = codec;
        _pendingRequests = new ConcurrentDictionary<ulong, TaskCompletionSource<ReadOnlyMemory<byte>>>();
        _stateLock = new Lock();
        _writeLock = new SemaphoreSlim(1, 1);
        _lifetime = new CancellationTokenSource();
        _closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Starting the read loop
        _readLoop = ReadResponsesAsync();
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

                // Refusing the reserved identifier and the identifiers never sent
                if (frame.CorrelationId == Frame.NoCorrelation || frame.CorrelationId > (ulong)Interlocked.Read(ref _lastCorrelationId))
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

    #endregion

    #region Publics

    /// <summary>
    /// Method used to send a request and wait for its response
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> gives the request a new correlation identifier, writes it when no other request is writing, then waits for the
    /// response. Several calls may run at the same time.</para>
    /// </remarks>
    /// <param name="payload">Content of the request; it must not be modified until the method returns</param>
    /// <param name="cancellationToken">Token used to stop waiting; it never interrupts a frame being written</param>
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

        // Stopping the wait on cancellation; the request is forgotten, so that a late response is ignored
        using CancellationTokenRegistration cancellation = cancellationToken.Register(() =>
        {
            if (_pendingRequests.TryRemove(correlationId, out TaskCompletionSource<ReadOnlyMemory<byte>>? canceled))
                canceled.TrySetCanceled(cancellationToken);
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
        try
        {
            if (!request.Task.IsCompleted)
                await _codec.WriteAsync(_connection.Stream, new Frame(correlationId, payload), _lifetime.Token).ConfigureAwait(false);
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

        // Function's return
        return await request.Task.ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to close the client and its connection; the pending requests fail with an <see cref="ObjectDisposedException"/>
    /// </summary>
    /// <returns>
    /// A task that completes when the client is closed for good and its read loop has ended
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once; a second call waits for the first one
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
        {
            await _readLoop.ConfigureAwait(false);
            return;
        }

        // Closing, then waiting for the read loop
        await CloseAsync(new ObjectDisposedException(nameof(RequestClient), "The request client has been disposed")).ConfigureAwait(false);
        await _readLoop.ConfigureAwait(false);
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
