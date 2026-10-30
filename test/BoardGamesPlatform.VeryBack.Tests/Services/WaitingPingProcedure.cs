/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Services/WaitingPingProcedure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake of the Ping procedure that waits until its call is canceled, used to test a stop during a request
 */

// Using
using BoardGamesPlatform.VeryBack.Procedures;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Services;

/// <summary>
/// Fake of <see cref="IPingProcedure"/> that never answers
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> simulates a database call in progress when the host stops.</para>
/// <para><b>Behavior:</b> signals that the call has started, then waits for its token, as the SQL client does with the token of a
/// call.</para>
/// </remarks>
internal sealed class WaitingPingProcedure : IPingProcedure
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Completed when a call has started
    /// </summary>
    private readonly TaskCompletionSource _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Task completed when a call has started
    /// </summary>
    public Task Started => _started.Task;

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
    /// Method used to wait until the call is canceled (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token of the call</param>
    /// <returns>
    /// Never an answer: the task is canceled with the token
    /// </returns>
    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        // Signaling the start, then waiting for the token
        _started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);

        // Function's return (never reached)
        return DateTimeOffset.MinValue;
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
