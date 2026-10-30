/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/TierStopsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the stop of the tiers never leaves one running because another failed or hung
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
/// Tests used to check <see cref="TierStops"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves the cleanup of the end-to-end tests: every tier is stopped, in the reverse order, whatever happens to one of
/// them, and a hung stop ends at the end of its grace period.</para>
/// <para><b>Behavior:</b> the tiers are replaced by stop actions that record their order, throw, or never end; no port is used.</para>
/// </remarks>
public sealed class TierStopsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    #endregion

    #region Variables

    /// <summary>
    /// Names of the tiers, in start order
    /// </summary>
    private static readonly string[] _tierNames = { "Very Back", "Back", "Gateway" };

    /// <summary>
    /// Names of the tiers, in stop order
    /// </summary>
    private static readonly string[] _reverseTierNames = { "Gateway", "Back", "Very Back" };

    /// <summary>
    /// Order of the stops when the Back fails to stop
    /// </summary>
    private static readonly string[] _gatewayThenVeryBack = { "Gateway", "Very Back" };

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// The tiers stop in the reverse order of their registration
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TiersStopInReverseOrderAsync()
    {
        // Registering three tiers
        List<string> order = new List<string>();
        TierStops stops = new TierStops();
        foreach (string name in _tierNames)
        {
            string tier = name;
            stops.Add(tier, _ =>
            {
                order.Add(tier);
                return Task.CompletedTask;
            });
        }

        // Stopping them all
        IReadOnlyList<Exception> failures = await stops.StopAllAsync(TierStops.DefaultGrace, TestContext.Current.CancellationToken);

        // Checks
        failures.ShouldBeEmpty();
        order.ShouldBe(_reverseTierNames);
    }

    /// <summary>
    /// A tier whose stop throws does not prevent the tiers below it from stopping, and its failure is returned
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingStopDoesNotPreventTheOthersAsync()
    {
        // Registering three tiers, the middle one failing
        List<string> order = new List<string>();
        TierStops stops = new TierStops();
        stops.Add("Very Back", _ =>
        {
            order.Add("Very Back");
            return Task.CompletedTask;
        });
        stops.Add("Back", _ => throw new InvalidOperationException("stop failure of the Back"));
        stops.Add("Gateway", _ =>
        {
            order.Add("Gateway");
            return Task.CompletedTask;
        });

        // Stopping them all
        IReadOnlyList<Exception> failures = await stops.StopAllAsync(TierStops.DefaultGrace, TestContext.Current.CancellationToken);

        // Checks: the Very Back stopped after the failure of the Back, and the failure names the tier
        order.ShouldBe(_gatewayThenVeryBack);
        failures.Count.ShouldBe(1);
        failures[0].Message.ShouldContain("Back");
        failures[0].Message.ShouldContain("stop failure of the Back");
    }

    /// <summary>
    /// A stop that never ends is given up at the end of its grace period, and the next tiers still stop
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AHungStopEndsAtTheEndOfItsGracePeriodAsync()
    {
        // Registering a Very Back that stops, and a Back that never ends and ignores its token
        bool veryBackStopped = false;
        TaskCompletionSource never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TierStops stops = new TierStops();
        stops.Add("Very Back", _ =>
        {
            veryBackStopped = true;
            return Task.CompletedTask;
        });
        stops.Add("Back", _ => never.Task);

        // Stopping them all with a short grace period
        IReadOnlyList<Exception> failures = await stops.StopAllAsync(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        // Checks
        veryBackStopped.ShouldBeTrue();
        failures.Count.ShouldBe(1);
        failures[0].ShouldBeOfType<TimeoutException>();
        failures[0].Message.ShouldContain("Back");
    }

    /// <summary>
    /// Stopping one tier alone runs its action once, and the final stop does not run it again
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ATierStoppedAloneIsNotStoppedAgainAsync()
    {
        // Registering a tier and stopping it alone, twice
        int stopCount = 0;
        TierStops stops = new TierStops();
        stops.Add("Very Back", _ =>
        {
            stopCount++;
            return Task.CompletedTask;
        });
        await stops.StopOneAsync("Very Back", TierStops.DefaultGrace, TestContext.Current.CancellationToken);
        await stops.StopOneAsync("Very Back", TierStops.DefaultGrace, TestContext.Current.CancellationToken);

        // Stopping everything
        IReadOnlyList<Exception> failures = await stops.StopAllAsync(TierStops.DefaultGrace, TestContext.Current.CancellationToken);

        // Checks
        stopCount.ShouldBe(1);
        failures.ShouldBeEmpty();
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
