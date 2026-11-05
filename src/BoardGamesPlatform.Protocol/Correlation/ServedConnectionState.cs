/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/ServedConnectionState.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	State shared by the read loop and the request tasks of one connection served by a RequestServer
 */

// Using
using System.Runtime.ExceptionServices;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Correlation;

/// <summary>
/// State shared by the requests of one connection served by <see cref="RequestServer"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps together what the read loop and the request tasks of one connection share: the slots of the requests handled
/// at the same time, the write lock, the requests in progress with their own cancellation, and the first failure.</para>
/// <para><b>Behavior:</b> the first failure is kept and cancels <see cref="Token"/>, which stops the read loop and every request in progress;
/// later failures, such as the cancellations it causes, are ignored. Each request in progress has its own cancellation source, linked to
/// <see cref="Token"/>: the client can cancel one request without touching the others. A source is disposed exactly once, by whoever is the
/// last to use it (the request that ends, or the cancellation that crosses its end), so that a cancellation never touches a disposed
/// source and no source stays registered on <see cref="Token"/> after its request.</para>
/// </remarks>
internal sealed class ServedConnectionState : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Source canceled at the first failure, linked to the token of the caller
    /// </summary>
    private readonly CancellationTokenSource _cancellation;

    /// <summary>
    /// Requests in progress, by correlation identifier
    /// </summary>
    private readonly Dictionary<ulong, RequestInProgress> _requests;

    /// <summary>
    /// Lock that guards <see cref="_requests"/> and the flags of each request; never held while a cancellation runs
    /// </summary>
    private readonly Lock _requestsLock;

    /// <summary>
    /// First failure of the service; null while none happened
    /// </summary>
    private Exception? _failure;

    /// <summary>
    /// Time stamp of the clock of the server when the last frame was received (the start of the service before the first one)
    /// </summary>
    private long _lastReceived;

    /// <summary>
    /// Whether the peer closed the connection (0: no, 1: yes); the cancellations that follow are then not failures
    /// </summary>
    private int _peerClosed;

    /// <summary>
    /// Timer that cancels the requests still in progress once the peer has closed; null before
    /// </summary>
    private ITimer? _drainDeadline;

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
    /// Slots of the requests handled at the same time
    /// </summary>
    public SemaphoreSlim Slots { get; }

    /// <summary>
    /// Lock that lets one frame at a time be written
    /// </summary>
    public SemaphoreSlim WriteLock { get; }

    /// <summary>
    /// Token canceled when the service ends
    /// </summary>
    public CancellationToken Token => _cancellation.Token;

    /// <summary>
    /// Time stamp of the clock of the server when the last frame was received
    /// </summary>
    public long LastReceived => Interlocked.Read(ref _lastReceived);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="maxConcurrentRequests">Number of requests handled at the same time, at most</param>
    /// <param name="startTimestamp">Time stamp of the clock of the server at the start of the service</param>
    /// <param name="cancellationToken">Token of the caller, which also ends the service</param>
    public ServedConnectionState(int maxConcurrentRequests, long startTimestamp, CancellationToken cancellationToken)
    {
        // Setting
        _lastReceived = startTimestamp;
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _requests = new Dictionary<ulong, RequestInProgress>();
        _requestsLock = new Lock();
        Slots = new SemaphoreSlim(maxConcurrentRequests, maxConcurrentRequests);
        WriteLock = new SemaphoreSlim(1, 1);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// Method used to record the first failure and end the service
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> once the peer has closed, an <see cref="OperationCanceledException"/> is not recorded: it is the end of the
    /// drain, or the stop of the tier, and either is a normal end for a connection the client left.</para>
    /// </remarks>
    /// <param name="exception">[Cannot be null] Failure to record; ignored when another one is already recorded, or when it is a cancellation
    /// after the close of the peer</param>
    public void Fail(Exception exception)
    {
        // Keeping the first failure, unless it is a cancellation caused by the close of the peer, then canceling the requests in progress
        // and the read loop
        if (exception is not OperationCanceledException || Volatile.Read(ref _peerClosed) == 0)
            Interlocked.CompareExchange(ref _failure, exception, null);
        _cancellation.Cancel();
    }

    /// <summary>
    /// Method used to bound the end of the service once the peer has closed its sending side, without recording a failure
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the requests in progress may still be answered until the delay elapses; then <see cref="Token"/> is canceled,
    /// so that they stop. The cancellations this causes are not recorded as failures.</para>
    /// </remarks>
    /// <param name="delay">Time left to the requests in progress</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the server</param>
    public void EndOnPeerClose(TimeSpan delay, TimeProvider timeProvider)
    {
        // Marking the close, then canceling the requests in progress once the delay elapses
        Volatile.Write(ref _peerClosed, 1);
        _drainDeadline = timeProvider.CreateTimer(_ => CancelQuietly(), null, delay, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    /// Method used to record that a frame was received
    /// </summary>
    /// <param name="timestamp">Time stamp of the clock of the server</param>
    public void MarkReceived(long timestamp)
    {
        // Setting
        Interlocked.Exchange(ref _lastReceived, timestamp);
    }

    /// <summary>
    /// Method used to cancel the requests in progress when the drain delay elapses, the state being perhaps already released
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> runs on the thread of the timer, so it never throws: the loss of the requests is recorded as a
    /// <see cref="TimeoutException"/>, which the service throws when it ends, and the link server logs.</para>
    /// </remarks>
    private void CancelQuietly()
    {
        // Recording why the requests still in progress are canceled, so that their loss is reported, then canceling; a service that ended
        // meanwhile has released its source, and there is nothing left to cancel
        Interlocked.CompareExchange(ref _failure, new TimeoutException("Requests were still in progress when the drain delay after the close of the client elapsed; they are canceled"), null);
        try
        {
            _cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The service ended before the delay: nothing to cancel
        }
        catch (Exception exception)
        {
            // A callback of a handler failed during the cancellation: kept, never thrown on the thread of the timer
            Interlocked.CompareExchange(ref _failure, exception, null);
        }
    }

    /// <summary>
    /// Method used to throw the first failure, if any, with its original stack trace
    /// </summary>
    public void ThrowIfFailed()
    {
        // Throwing the recorded failure
        Exception? failure = Volatile.Read(ref _failure);
        if (failure is not null)
            ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>
    /// Method used to record a request that starts, with its own cancellation
    /// </summary>
    /// <param name="correlationId">Correlation identifier of the request</param>
    /// <param name="token">Token of the request: canceled when the client cancels it, or when the service ends</param>
    /// <returns>
    /// True  ==> The request is recorded; <see cref="EndRequest"/> must be called when it ends<br/>
    /// False ==> A request with the same identifier is still in progress: nothing is recorded
    /// </returns>
    public bool TryStartRequest(ulong correlationId, out CancellationToken token)
    {
        // Refusing an identifier still in progress, else recording the request
        lock (_requestsLock)
        {
            if (_requests.ContainsKey(correlationId))
            {
                token = CancellationToken.None;

                // Function's return
                return false;
            }
            RequestInProgress request = new RequestInProgress(CancellationTokenSource.CreateLinkedTokenSource(_cancellation.Token));
            _requests.Add(correlationId, request);
            token = request.Source.Token;
        }

        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to forget a request that ended, and release its cancellation source unless a cancellation is using it
    /// </summary>
    /// <param name="correlationId">Correlation identifier of the request</param>
    public void EndRequest(ulong correlationId)
    {
        // Forgetting the request; the cancellation in progress, if any, releases the source when it ends
        RequestInProgress? release = null;
        lock (_requestsLock)
        {
            if (_requests.Remove(correlationId, out RequestInProgress? request))
            {
                request.HasEnded = true;
                if (!request.IsCanceling)
                    release = request;
            }
        }

        // Releasing the source outside the lock
        release?.Source.Dispose();
    }

    /// <summary>
    /// Method used to cancel one request in progress, at the demand of the client
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> a request that already ended, or that never existed, is ignored. The cancellation runs outside the lock, since it
    /// may run the code of the handler (its callbacks); if the request ends meanwhile, its source is released here, once the cancellation is
    /// done.</para>
    /// </remarks>
    /// <param name="correlationId">Correlation identifier of the request to cancel</param>
    public void CancelRequest(ulong correlationId)
    {
        // Marking the request, so that its end does not release the source while it is canceled
        RequestInProgress? request;
        lock (_requestsLock)
        {
            if (!_requests.TryGetValue(correlationId, out request) || request.IsCanceling)
            {
                // Function's return: nothing to cancel
                return;
            }
            request.IsCanceling = true;
        }

        // Canceling, then releasing the source if the request ended meanwhile
        try
        {
            request.Source.Cancel();
        }
        finally
        {
            bool release;
            lock (_requestsLock)
            {
                request.IsCanceling = false;
                release = request.HasEnded;
            }
            if (release)
                request.Source.Dispose();
        }
    }

    /// <summary>
    /// Method used to release the cancellation sources and the semaphores, once every request has ended
    /// </summary>
    public void Dispose()
    {
        // Releasing the sources of the requests that did not end (none once the service awaited them), then the rest
        lock (_requestsLock)
        {
            foreach (RequestInProgress request in _requests.Values)
                request.Source.Dispose();
            _requests.Clear();
        }
        _drainDeadline?.Dispose();
        _cancellation.Dispose();
        Slots.Dispose();
        WriteLock.Dispose();
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

    /// <summary>
    /// One request in progress: its cancellation source and who still uses it
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the flags are read and written under the lock of the state only.</para>
    /// </remarks>
    private sealed class RequestInProgress
    {
        #region Members

        #region Constants

        // #N/A

        #endregion

        #region Variables

        // #N/A

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
        /// Cancellation source of the request, linked to the token of the service
        /// </summary>
        public CancellationTokenSource Source { get; }

        /// <summary>
        /// Whether a cancellation of the client is running on the source
        /// </summary>
        public bool IsCanceling { get; set; }

        /// <summary>
        /// Whether the request has ended
        /// </summary>
        public bool HasEnded { get; set; }

        #endregion

        #region Delegates

        // #N/A

        #endregion

        #endregion

        #region Constructors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="source">[Cannot be null] Cancellation source of the request</param>
        public RequestInProgress(CancellationTokenSource source)
        {
            // Setting
            Source = source;
        }

        #endregion

        #region Destructors

        // #N/A

        #endregion

        #region Methods

        #region Privates

        // #N/A

        #endregion

        #region Publics

        // #N/A

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
}
