/*
 *	@file	test/BoardGamesPlatform.TestKit/TestPortBlockSet.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Several blocks of test ports of one test project, the next one locked only once the previous one is used up
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
/// Blocks of test ports of a test project that needs more than <see cref="TestPortBlock.BlockSize"/> ports in one run
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a test project that outgrows its block gets a second one of <see cref="TestPortBlocks"/>, without changing its tests:
/// they still ask for the next port.</para>
/// <para><b>Behavior:</b> the blocks are locked in their order, each one only when the previous one is used up, so that a run that needs one
/// block never holds the others.</para>
/// </remarks>
public sealed class TestPortBlockSet : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Start of the range as the environment variable would hold it; null to read the variable
    /// </summary>
    private readonly string? _baseText;

    /// <summary>
    /// Indexes of the blocks, in the order they are used
    /// </summary>
    private readonly int[] _blockIndexes;

    /// <summary>
    /// Blocks locked so far, in the same order
    /// </summary>
    private readonly List<TestPortBlock> _blocks = new List<TestPortBlock>();

    /// <summary>
    /// Lock of the move to the next block
    /// </summary>
    private readonly object _sync = new object();

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

    /// <summary>
    /// Constructor; locks nothing before the first port is asked for
    /// </summary>
    /// <param name="blockIndexes">[Cannot be null or empty] Indexes of the blocks of the test project, from <see cref="TestPortBlocks"/>, in the
    /// order they are used</param>
    /// <exception cref="ArgumentNullException">If the indexes are null</exception>
    /// <exception cref="ArgumentException">If there is no index</exception>
    public TestPortBlockSet(params int[] blockIndexes) : this(Environment.GetEnvironmentVariable(TestPortBlock.BaseVariable), blockIndexes)
    {
        // #N/A
    }

    /// <summary>
    /// Constructor with the start of the range given as a text instead of read from the environment (used by the tests of this class)
    /// </summary>
    /// <param name="baseText">[Can be null] Start of the range as the environment variable would hold it; null or blank for the default</param>
    /// <param name="blockIndexes">[Cannot be null or empty] Indexes of the blocks, in the order they are used</param>
    /// <exception cref="ArgumentNullException">If the indexes are null</exception>
    /// <exception cref="ArgumentException">If there is no index</exception>
    internal TestPortBlockSet(string? baseText, params int[] blockIndexes)
    {
        // Checks
        if (blockIndexes is null)
            throw new ArgumentNullException(nameof(blockIndexes), $"The value of the parameter: {nameof(blockIndexes)} cannot be null");
        if (blockIndexes.Length == 0)
            throw new ArgumentException($"The value of the parameter: {nameof(blockIndexes)} cannot be empty", nameof(blockIndexes));

        // Setting
        _baseText = baseText;
        _blockIndexes = (int[])blockIndexes.Clone();
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
    /// Method used to get the next port, from the current block or else from the next one, locked then
    /// </summary>
    /// <returns>
    /// A port that no other test of the run has had, checked to be free
    /// </returns>
    /// <exception cref="InvalidOperationException">If every block is used up, if a block cannot be locked, or if the port cannot be bound</exception>
    public int Next()
    {
        lock (_sync)
        {
            // Taking a port of the last block locked, else locking the next block
            while (true)
            {
                if (_blocks.Count > 0 && _blocks[^1].TryNext(out int port))
                    return port;
                if (_blocks.Count == _blockIndexes.Length)
                    throw new InvalidOperationException($"The {_blockIndexes.Length} blocks of test ports of this test project are all used up: give it another block in TestPortBlocks");
                _blocks.Add(new TestPortBlock(_blockIndexes[_blocks.Count], _baseText));
            }
        }
    }

    /// <summary>
    /// Method used to release the locks of the blocks before the end of the process (the test projects never call it)
    /// </summary>
    public void Dispose()
    {
        // Releasing the lock files
        lock (_sync)
        {
            foreach (TestPortBlock block in _blocks)
                block.Dispose();
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
