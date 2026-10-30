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
/// at the same time, the write lock and the first failure.</para>
/// <para><b>Behavior:</b> the first failure is kept and cancels <see cref="Token"/>, which stops the read loop and the requests in progress;
/// later failures, such as the cancellations it causes, are ignored.</para>
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
    /// First failure of the service; null while none happened
    /// </summary>
    private Exception? _failure;

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
    /// Lock that lets one response at a time be written
    /// </summary>
    public SemaphoreSlim WriteLock { get; }

    /// <summary>
    /// Token canceled when the service ends
    /// </summary>
    public CancellationToken Token => _cancellation.Token;

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
    /// <param name="cancellationToken">Token of the caller, which also ends the service</param>
    public ServedConnectionState(int maxConcurrentRequests, CancellationToken cancellationToken)
    {
        // Setting
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
    /// <param name="exception">[Cannot be null] Failure to record; ignored when another one is already recorded</param>
    public void Fail(Exception exception)
    {
        // Keeping the first failure, then canceling the requests in progress and the read loop
        Interlocked.CompareExchange(ref _failure, exception, null);
        _cancellation.Cancel();
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
    /// Method used to release the cancellation source and the semaphores, once every request has ended
    /// </summary>
    public void Dispose()
    {
        // Releasing
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
}
