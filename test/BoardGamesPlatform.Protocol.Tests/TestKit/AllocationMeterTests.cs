/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/TestKit/AllocationMeterTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the meter of the complexity tests sees what an operation allocates, and refuses what it could not measure
 */

// Using
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.TestKit;

/// <summary>
/// Tests used to check the meter of the allocations
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a complexity test passes when the meter counts few bytes: a meter that counted nothing would make every one of
/// them pass, and prove nothing.</para>
/// <para><b>Behavior:</b> a known allocation must be counted, and an operation that goes on asynchronously must be refused.</para>
/// </remarks>
public sealed class AllocationMeterTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Bytes of the array allocated by the known operation
    /// </summary>
    private const int KnownLength = 1000;

    /// <summary>
    /// Bytes of the header of an array on a 64-bit runtime, counted with it, and a margin for its alignment
    /// </summary>
    private const int ArrayOverhead = 64;

    #endregion

    #region Variables

    /// <summary>
    /// Last array allocated by the known operation: kept, so that the runtime cannot place it on the stack, out of the measure
    /// </summary>
    private static byte[] _kept = Array.Empty<byte>();

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
    /// An array allocated by the operation is counted, with its header and nothing else
    /// </summary>
    [Fact]
    public void AKnownAllocationIsCounted()
    {
        // Acting
        long allocated = AllocationMeter.PerRun(() =>
        {
            _kept = new byte[KnownLength];

            // Function's return
            return ValueTask.CompletedTask;
        });

        // Checks
        allocated.ShouldBeInRange(KnownLength, KnownLength + ArrayOverhead);
        _kept.Length.ShouldBe(KnownLength);
    }

    /// <summary>
    /// An operation that goes on on another thread is refused, since what it allocates there would not be counted
    /// </summary>
    [Fact]
    public void AnOperationThatGoesOnAsynchronouslyIsRefused()
    {
        // Acting
        InvalidOperationException refusal = Should.Throw<InvalidOperationException>(() => AllocationMeter.PerRun(async () => await Task.Yield()));

        // Check
        refusal.Message.ShouldContain("synchronously");
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
