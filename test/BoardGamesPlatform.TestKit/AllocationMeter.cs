/*
 *	@file	test/BoardGamesPlatform.TestKit/AllocationMeter.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Measure of the memory an operation allocates, so that a test can bound the space an operation costs
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Measure of the bytes allocated by an operation on the current thread
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a test of space complexity bounds what one operation allocates (a frame, a record), so that a change that copies
/// a payload again, or allocates per byte, fails a test instead of reaching a tier. A count of bytes does not depend on the load of the
/// machine, unlike a measure of time, so the test is never flaky.</para>
/// <para><b>Behavior:</b> counts the bytes allocated on the current thread (<see cref="GC.GetAllocatedBytesForCurrentThread"/>), which
/// the other tests running in parallel never change. The operation must complete synchronously, on a stream in memory: an asynchronous
/// operation that really waits would go on on another thread, out of the measure, so it throws instead of measuring something wrong. The
/// first runs are left out of the measure, since the first call of a method allocates for the runtime (compilation, caches).</para>
/// </remarks>
public static class AllocationMeter
{
    #region Members

    #region Constants

    /// <summary>
    /// Runs left out of the measure before it starts
    /// </summary>
    public const int DefaultWarmUps = 3;

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
    /// Method used to run the operation once, refusing one that does not complete synchronously
    /// </summary>
    /// <param name="operation">[Cannot be null] Operation to run</param>
    /// <exception cref="InvalidOperationException">If the operation does not complete synchronously</exception>
    private static void RunSynchronously(Func<ValueTask> operation)
    {
        // Running, then checking that nothing went on on another thread
        ValueTask running = operation();
        if (!running.IsCompleted)
            throw new InvalidOperationException("The operation did not complete synchronously: what it allocates on another thread cannot be measured");
        running.GetAwaiter().GetResult();
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to measure the bytes one run of an operation allocates, on average over several runs
    /// </summary>
    /// <param name="operation">[Cannot be null] Operation to measure; it must complete synchronously, and be the same at each run</param>
    /// <param name="runs">Runs measured, at least 1</param>
    /// <param name="warmUps">Runs left out of the measure before it starts</param>
    /// <returns>
    /// The bytes allocated by one run, on average, rounded up
    /// </returns>
    /// <exception cref="ArgumentNullException">If the operation is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the runs are fewer than 1, or the warm-ups negative</exception>
    /// <exception cref="InvalidOperationException">If the operation does not complete synchronously</exception>
    public static long PerRun(Func<ValueTask> operation, int runs = 10, int warmUps = DefaultWarmUps)
    {
        // Checks
        if (operation is null)
            throw new ArgumentNullException(nameof(operation), $"The value of the parameter: {nameof(operation)} cannot be null");
        if (runs < 1)
            throw new ArgumentOutOfRangeException(nameof(runs), runs, $"The value of the parameter: {nameof(runs)} must be at least 1");
        if (warmUps < 0)
            throw new ArgumentOutOfRangeException(nameof(warmUps), warmUps, $"The value of the parameter: {nameof(warmUps)} cannot be negative");

        // Warming up, then measuring the runs
        for (int run = 0; run < warmUps; run++)
            RunSynchronously(operation);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int run = 0; run < runs; run++)
            RunSynchronously(operation);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Function's return
        return (allocated + runs - 1) / runs;
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
