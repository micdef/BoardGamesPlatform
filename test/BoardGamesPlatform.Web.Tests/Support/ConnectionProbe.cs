/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/ConnectionProbe.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Counts, fails or holds the negotiation requests of a SignalR connection to the Gateway started in process
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// Probe set on the HTTP handler of a SignalR connection to the in-process Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test prove how many times a connection was started, that a failed start is retried, and that a start in
/// progress can be canceled, without a network.</para>
/// <para><b>Behavior:</b> every negotiation request (the first request of a start) is counted. The next requests can be made to fail
/// with <see cref="HttpRequestException"/> (<see cref="FailNext"/>), or the next negotiation can be held until its request is canceled
/// (<see cref="HoldNext"/>).</para>
/// </remarks>
public sealed class ConnectionProbe
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Number of negotiation requests received
    /// </summary>
    private int _negotiations;

    /// <summary>
    /// Number of negotiation requests that still have to fail
    /// </summary>
    private int _failuresLeft;

    /// <summary>
    /// 1 when the next negotiation has to be held, otherwise 0
    /// </summary>
    private int _holdNext;

    /// <summary>
    /// Completed when a negotiation is being held
    /// </summary>
    private readonly TaskCompletionSource _held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Number of negotiation requests received, that is the number of starts attempted
    /// </summary>
    public int Negotiations => Volatile.Read(ref _negotiations);

    /// <summary>
    /// Completes when a negotiation is being held
    /// </summary>
    public Task Held => _held.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

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
    /// Method used to make the next negotiation requests fail
    /// </summary>
    /// <param name="count">Number of negotiation requests that fail</param>
    public void FailNext(int count)
    {
        // Setting
        Volatile.Write(ref _failuresLeft, count);
    }

    /// <summary>
    /// Method used to hold the next negotiation until its request is canceled
    /// </summary>
    public void HoldNext()
    {
        // Setting
        Volatile.Write(ref _holdNext, 1);
    }

    /// <summary>
    /// Method used by the handler to apply the probe to a request
    /// </summary>
    /// <param name="request">[Cannot be null] Request about to be sent</param>
    /// <param name="cancellationToken">Token of the request</param>
    /// <returns>
    /// A task that ends when the request can go on
    /// </returns>
    /// <exception cref="HttpRequestException">If the request has to fail</exception>
    public async Task BeforeSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Only the negotiation request is probed
        if (request.RequestUri is null || !request.RequestUri.AbsolutePath.EndsWith("/negotiate", StringComparison.Ordinal))
            return;

        // Counting it
        Interlocked.Increment(ref _negotiations);

        // Failing it when asked to
        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
            throw new HttpRequestException("Simulated failure of the Gateway");
        Interlocked.Exchange(ref _failuresLeft, 0);

        // Holding it until it is canceled when asked to
        if (Interlocked.Exchange(ref _holdNext, 0) == 1)
        {
            _held.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
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
