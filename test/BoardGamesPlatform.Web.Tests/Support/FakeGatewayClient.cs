/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/FakeGatewayClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Gateway client that answers with a given time, fails with a given exception, or waits until it is released
 */

// Using
using BoardGamesPlatform.Gateway.Contracts.Ping;
using BoardGamesPlatform.Web.GatewayLink;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// Gateway client replaced by the tests of the pages
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a page be tested without a Gateway, a network or a database.</para>
/// <para><b>Behavior:</b> every call waits for <see cref="Release"/> when the client was built to wait, then answers with the time it was
/// given or throws the exception it was given; counts its calls.</para>
/// </remarks>
public sealed class FakeGatewayClient : IGatewayClient
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Time returned by the Ping operation; null when the client fails
    /// </summary>
    private readonly DateTimeOffset? _serverTime;

    /// <summary>
    /// Exception thrown by the Ping operation; null when the client answers
    /// </summary>
    private readonly Exception? _failure;

    /// <summary>
    /// Gate that holds the calls until <see cref="Release"/>; null when the calls are not held
    /// </summary>
    private readonly TaskCompletionSource? _gate;

    /// <summary>
    /// Number of Ping calls received
    /// </summary>
    private int _pingCalls;

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
    /// Number of Ping calls received
    /// </summary>
    public int PingCalls => Volatile.Read(ref _pingCalls);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor of a client that answers
    /// </summary>
    /// <param name="serverTime">Time returned by the Ping operation</param>
    /// <param name="hold">True to hold every call until <see cref="Release"/></param>
    public FakeGatewayClient(DateTimeOffset serverTime, bool hold = false)
    {
        // Setting
        _serverTime = serverTime;
        _gate = hold ? new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) : null;
    }

    /// <summary>
    /// Constructor of a client that fails
    /// </summary>
    /// <param name="failure">[Cannot be null] Exception thrown by the Ping operation</param>
    public FakeGatewayClient(Exception failure)
    {
        // Setting
        _failure = failure;
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
    /// Method used to let the held calls go on
    /// </summary>
    public void Release()
    {
        // Opening the gate
        _gate?.TrySetResult();
    }

    /// <inheritdoc/>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Counting the call
        Interlocked.Increment(ref _pingCalls);

        // Waiting to be released when asked to
        if (_gate is not null)
            await _gate.Task.WaitAsync(cancellationToken);

        // Failing when asked to
        if (_failure is not null)
            throw _failure;

        // Function's return
        return new PingResponse(_serverTime!.Value);
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
