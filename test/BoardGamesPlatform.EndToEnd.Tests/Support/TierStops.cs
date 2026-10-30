/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/TierStops.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Stops the started tiers in reverse order, whatever happens to each stop
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Support;

/// <summary>
/// The stop actions of the started tiers
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier that fails or hangs when it stops must never leave the other tiers running with their ports bound, nor hide the
/// failure that made the test stop in the first place.</para>
/// <para><b>Behavior:</b> each started tier registers a named stop action, in start order. <see cref="StopAllAsync"/> runs them in the
/// reverse order, each in its own try and under a bounded grace period (a hung stop ends with a <see cref="TimeoutException"/> instead of
/// waiting for the timeout of the test), and returns the failures instead of throwing, so that the caller decides which exception to throw.
/// Each action runs at most once.</para>
/// </remarks>
internal sealed class TierStops
{
    #region Members

    #region Constants

    /// <summary>
    /// Grace period given to the stop of each tier by default
    /// </summary>
    public static readonly TimeSpan DefaultGrace = TimeSpan.FromSeconds(5);

    #endregion

    #region Variables

    /// <summary>
    /// Stop actions not run yet, in start order
    /// </summary>
    private readonly List<(string Name, Func<CancellationToken, Task> Stop)> _stops = new List<(string Name, Func<CancellationToken, Task> Stop)>();

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

    // #N/A

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

    /// <summary>
    /// Method used to run one stop action under the grace period
    /// </summary>
    /// <param name="name">[Cannot be null] Name of the tier, for the failure message</param>
    /// <param name="stop">[Cannot be null] Stop action; receives a token canceled at the end of the grace period</param>
    /// <param name="grace">Grace period</param>
    /// <param name="cancellationToken">Token of the caller; ends the wait early</param>
    /// <returns>
    /// The failure of the stop (the exception thrown, or a timeout if it hung), or null when the tier stopped
    /// </returns>
    private static async Task<Exception?> RunAsync(string name, Func<CancellationToken, Task> stop, TimeSpan grace, CancellationToken cancellationToken)
    {
        // Running the action, giving up on a hung one when the grace period ends (the action may ignore its token)
        using CancellationTokenSource graceSource = new CancellationTokenSource(grace);
        try
        {
            await stop(graceSource.Token).WaitAsync(grace, cancellationToken);
        }
        catch (TimeoutException exception)
        {
            // Function's return
            return new TimeoutException($"The tier '{name}' did not stop within {grace}", exception);
        }
        catch (Exception exception)
        {
            // Function's return
            return new InvalidOperationException($"The tier '{name}' failed to stop: {exception.Message}", exception);
        }

        // Function's return
        return null;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to register the stop action of a tier that has just started
    /// </summary>
    /// <param name="name">[Cannot be null] Name of the tier</param>
    /// <param name="stop">[Cannot be null] Action that stops the tier and releases it</param>
    public void Add(string name, Func<CancellationToken, Task> stop)
    {
        // Registering
        _stops.Add((name, stop));
    }

    /// <summary>
    /// Method used to stop one tier alone, as when its process dies
    /// </summary>
    /// <param name="name">[Cannot be null] Name of the tier</param>
    /// <param name="grace">Grace period</param>
    /// <param name="cancellationToken">Token of the caller; ends the wait early</param>
    /// <returns>
    /// A task that completes when the tier is stopped; does nothing if it is not registered (already stopped)
    /// </returns>
    /// <exception cref="InvalidOperationException">If the stop failed</exception>
    /// <exception cref="TimeoutException">If the stop did not end within the grace period</exception>
    public async Task StopOneAsync(string name, TimeSpan grace, CancellationToken cancellationToken = default)
    {
        // Taking the action out of the list, so that it never runs twice
        int index = _stops.FindIndex(stop => stop.Name == name);
        if (index < 0)
            return;
        (string Name, Func<CancellationToken, Task> Stop) entry = _stops[index];
        _stops.RemoveAt(index);

        // Running it, and reporting its failure
        Exception? failure = await RunAsync(entry.Name, entry.Stop, grace, cancellationToken);
        if (failure is not null)
            throw failure;
    }

    /// <summary>
    /// Method used to stop every registered tier, in the reverse order of the start
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> stop everything, whatever happens to one tier.</para>
    /// <para><b>Behavior:</b> a failure or a timeout of one stop never prevents the next ones; the failures are collected and returned.</para>
    /// </remarks>
    /// <param name="grace">Grace period of each stop</param>
    /// <param name="cancellationToken">Token of the caller; ends the wait of the current stop early</param>
    /// <returns>
    /// The failures, in stop order; empty when every tier stopped
    /// </returns>
    public async Task<IReadOnlyList<Exception>> StopAllAsync(TimeSpan grace, CancellationToken cancellationToken = default)
    {
        // Stopping the tiers from the last started to the first
        List<Exception> failures = new List<Exception>();
        while (_stops.Count > 0)
        {
            (string Name, Func<CancellationToken, Task> Stop) entry = _stops[_stops.Count - 1];
            _stops.RemoveAt(_stops.Count - 1);
            Exception? failure = await RunAsync(entry.Name, entry.Stop, grace, cancellationToken);
            if (failure is not null)
                failures.Add(failure);
        }

        // Function's return
        return failures;
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
