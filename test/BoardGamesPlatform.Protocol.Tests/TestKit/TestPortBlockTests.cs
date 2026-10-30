/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/TestKit/TestPortBlockTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the rules of the blocks of fixed test ports: the start of the range and the lock of a block
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
/// Tests used to check <see cref="TestPortBlock"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the blocks of test ports are what keeps the test projects, and the runs of one test project, from sharing a port:
/// their rules are proven here, not trusted.</para>
/// <para><b>Behavior:</b> the tests give the start of the range to the internal constructor as a text, so that no environment variable is
/// set for the whole process (the other tests run in parallel and read it). Each test uses its own start, far from the default range, and
/// releases its locks at the end; no port is bound, only lock files of the temporary folder are taken.</para>
/// </remarks>
public sealed class TestPortBlockTests
{
    #region Members

    #region Constants

    // #N/A

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// A start of the range that is not a multiple of the block size is refused, because it would overlap partly the block of another run
    /// </summary>
    /// <param name="baseText">Start of the range, as the environment variable would hold it</param>
    [Theory]
    [InlineData("17050")]
    [InlineData("30001")]
    [InlineData("30099")]
    public void AStartThatIsNotAMultipleOfTheBlockSizeIsRefused(string baseText)
    {
        // Checks
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new TestPortBlock(0, baseText));
        exception.Message.ShouldContain("multiple of 100");
        exception.Message.ShouldContain(TestPortBlock.BaseVariable);
        exception.Message.ShouldContain($"'{baseText}'");
    }

    /// <summary>
    /// A start that is not a number, or outside the allowed range, is refused
    /// </summary>
    /// <param name="baseText">Start of the range, as the environment variable would hold it</param>
    [Theory]
    [InlineData("abc")]
    [InlineData("-100")]
    [InlineData("0")]
    [InlineData("31800")]
    [InlineData("50000")]
    public void AStartOutsideTheAllowedRangeIsRefused(string baseText)
    {
        // Checks
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new TestPortBlock(0, baseText));
        exception.Message.ShouldContain(TestPortBlock.BaseVariable);
    }

    /// <summary>
    /// A start that is a multiple of the block size gives the block its first port: the start plus the block size times the index
    /// </summary>
    [Fact]
    public void ABlockStartsAtTheStartPlusItsIndexTimesTheBlockSize()
    {
        // Taking a block of a start of its own
        using TestPortBlock block = new TestPortBlock(2, "30200");

        // Checks
        block.First.ShouldBe(30400);
    }

    /// <summary>
    /// A second lock of the same block in the same process is refused with the message of a second test run, and the sharing violation is
    /// the cause
    /// </summary>
    [Fact]
    public void ASecondLockOfTheSameBlockIsRefusedAsAnotherTestRun()
    {
        // Taking the block, then taking it again
        using TestPortBlock first = new TestPortBlock(0, "30500");
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new TestPortBlock(0, "30500"));

        // Checks: the message names the ports and the variable; the cause is the sharing violation (Win32 error 32, in the low bits of the HResult)
        exception.Message.ShouldContain("Another test run uses the test ports 30500-30599");
        exception.Message.ShouldContain(TestPortBlock.BaseVariable);
        IOException cause = exception.InnerException.ShouldBeOfType<IOException>();
        (cause.HResult & 0xFFFF).ShouldBe(32);
    }

    /// <summary>
    /// Two blocks that do not overlap can be locked at the same time, and a block can be locked again once it is released
    /// </summary>
    [Fact]
    public void DisjointBlocksAreLockedTogetherAndAReleasedBlockCanBeLockedAgain()
    {
        // Taking two blocks of the same start, which are disjoint
        using TestPortBlock zero = new TestPortBlock(0, "30600");
        using TestPortBlock one = new TestPortBlock(1, "30600");
        zero.First.ShouldBe(30600);
        one.First.ShouldBe(30700);

        // Releasing a block, then taking it again
        zero.Dispose();
        using TestPortBlock again = new TestPortBlock(0, "30600");

        // Checks
        again.First.ShouldBe(30600);
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
