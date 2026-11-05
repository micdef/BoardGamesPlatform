/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/BlockingPingProcedure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake of the Ping procedure that never answers on its own: it waits until its call is canceled, and tells when
 */

// Using
using BoardGamesPlatform.VeryBack.Procedures;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Support;

/// <summary>
/// Fake of <see cref="IPingProcedure"/> that plays a long call to the database
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that a request abandoned by a front stops the call in progress in the Very Back, the last tier before the
/// database: its token must be canceled, otherwise the call would run for nobody.</para>
/// <para><b>Behavior:</b> a call completes <see cref="Entered"/>, then waits for its token; when the token is canceled it completes
/// <see cref="Canceled"/> and ends with the cancellation, as the SQL client does.</para>
/// </remarks>
internal sealed class BlockingPingProcedure : IPingProcedure
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Completed when a call starts
    /// </summary>
    private readonly TaskCompletionSource _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Completed when the token of a call is canceled
    /// </summary>
    private readonly TaskCompletionSource _canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Task that completes when a call starts
    /// </summary>
    public Task Entered => _entered.Task;

    /// <summary>
    /// Task that completes when the token of a call is canceled
    /// </summary>
    public Task Canceled => _canceled.Task;

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
    /// Method used to play a call that only ends when it is canceled
    /// </summary>
    /// <param name="cancellationToken">Token of the call, given by the Very Back</param>
    /// <returns>
    /// A task that never completes with a value: it ends with the cancellation of its token
    /// </returns>
    /// <exception cref="OperationCanceledException">When the token is canceled</exception>
    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        // Telling that the call runs, then waiting for its cancellation
        _entered.TrySetResult();
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _canceled.TrySetResult();
            throw;
        }

        // Function's return (never reached: the delay is infinite)
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
