/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/HangingBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Back client whose Ping never answers and tells when it is canceled
 */

// Using
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Gateway.BackLink;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// Back client that waits for the cancellation of the call
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test prove that the hub gives the Back client a token canceled when the front disconnects. The cancellation is
/// observed on the Gateway side: the protocol has no message to tell the Back that a request is abandoned (canceling stops the wait only), so a
/// fake Back could not see it.</para>
/// <para><b>Behavior:</b> <see cref="CallReceived"/> completes when Ping is called; the call then waits until its token is canceled, which
/// completes <see cref="CallCanceled"/> and ends the call as canceled.</para>
/// </remarks>
public sealed class HangingBackClient : IBackClient
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Completed when Ping is called
    /// </summary>
    private readonly TaskCompletionSource _callReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the token of the call is canceled
    /// </summary>
    private readonly TaskCompletionSource _callCanceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Task completed when Ping is called
    /// </summary>
    public Task CallReceived => _callReceived.Task;

    /// <summary>
    /// Task completed when the token given to Ping is canceled
    /// </summary>
    public Task CallCanceled => _callCanceled.Task;

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

    /// <inheritdoc/>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Telling that the call arrived, then waiting for the cancellation
        _callReceived.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _callCanceled.TrySetResult();
            throw;
        }

        // Never reached: the delay only ends by cancellation
        throw new InvalidOperationException("The hanging Back client was not canceled");
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
