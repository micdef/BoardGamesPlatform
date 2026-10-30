/*
 *	@file	test/BoardGamesPlatform.TestKit/TestPortBlock.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fixed TCP ports reserved for the network tests: one block of 100 ports per test project
 */

// Using
using System.Globalization;
using System.Net;
using System.Net.Sockets;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Block of fixed TCP ports given to one test project
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the network tests never depend on the ports the system hands out (decided by the project lead): they use the fixed
/// range 17000-17999, below 32768, so outside the dynamic range of Windows (49152-65535, where Hyper-V also reserves moving blocks) and of
/// Linux (32768-60999). Each test project owns its own block of <see cref="BlockSize"/> ports (see <see cref="TestPortBlocks"/>), so that the
/// test projects can still run at the same time without ever sharing a port.</para>
/// <para><b>Behavior:</b></para>
/// <list type="bullet">
/// <item><description>The block is locked for the whole test run of the project, through a lock file held open in the temporary folder:
/// a second run of the same test project at the same time (another worktree, a coding agent) fails at once with a clear message, instead of
/// sharing the ports.</description></item>
/// <item><description>Each call to <see cref="Next"/> gives the next port of the block, never the same one twice in a run, after checking
/// that it can be bound on the loopback address and on all the addresses; a port that cannot be bound fails the test at once, with a message
/// naming the port and the variable that moves the range.</description></item>
/// <item><description>The start of the range is <see cref="DefaultBase"/>, or the value of the environment variable
/// <see cref="BaseVariable"/> on a machine where something already uses that range.</description></item>
/// <item><description>Not detected: a program listening on the IPv6 wildcard in dual mode; the connection of a test then reaches that
/// program, and the test fails loudly.</description></item>
/// </list>
/// </remarks>
public sealed class TestPortBlock
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of ports of each block
    /// </summary>
    public const int BlockSize = 100;

    /// <summary>
    /// Number of blocks of the range (17000-17999 by default)
    /// </summary>
    public const int BlockCount = 10;

    /// <summary>
    /// First port of the range when the environment variable is not set
    /// </summary>
    public const int DefaultBase = 17000;

    /// <summary>
    /// Environment variable that moves the range, for a machine where something already uses 17000-17999
    /// </summary>
    public const string BaseVariable = "BOARDGAMESPLATFORM_TEST_PORT_BASE";

    /// <summary>
    /// Highest start of the range: the range must stay below 32768, outside the dynamic ports of Windows and of Linux
    /// </summary>
    private const int MaxBase = 32768 - (BlockSize * BlockCount);

    /// <summary>
    /// Lowest start of the range: above the well-known ports
    /// </summary>
    private const int MinBase = 1024;

    #endregion

    #region Variables

    /// <summary>
    /// Offset of the last port given in the block; -1 before the first one
    /// </summary>
    private int _lastOffset = -1;

    /// <summary>
    /// Lock file of the block, held open until the test process ends (the system releases it then, even after a crash)
    /// </summary>
    private readonly FileStream _lock;

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
    /// First port of the block
    /// </summary>
    public int First { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; locks the block for the test run of the project
    /// </summary>
    /// <param name="blockIndex">Index of the block of the test project, from <see cref="TestPortBlocks"/></param>
    /// <exception cref="ArgumentOutOfRangeException">If the index is outside the range</exception>
    /// <exception cref="InvalidOperationException">If the environment variable holds an unusable start, or another run holds the block</exception>
    public TestPortBlock(int blockIndex)
    {
        // Checks
        if (blockIndex < 0 || blockIndex >= BlockCount)
            throw new ArgumentOutOfRangeException(nameof(blockIndex), $"The value of the parameter: {nameof(blockIndex)} must be between 0 and {BlockCount - 1}");

        // Setting, then locking the block
        First = ReadBase() + (blockIndex * BlockSize);
        _lock = LockBlock(First);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to read the start of the range
    /// </summary>
    /// <returns>
    /// The start of the range
    /// </returns>
    /// <exception cref="InvalidOperationException">If the environment variable holds an unusable start</exception>
    private static int ReadBase()
    {
        // Reading the variable, the default when it is not set
        string? text = Environment.GetEnvironmentVariable(BaseVariable);
        if (string.IsNullOrWhiteSpace(text))
            return DefaultBase;

        // Checking it keeps the whole range below the dynamic ports
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) || value < MinBase || value > MaxBase)
            throw new InvalidOperationException($"{BaseVariable} must be a port from {MinBase} to {MaxBase}, so that the {BlockSize * BlockCount} test ports stay below 32768, not '{text}'");

        // Function's return
        return value;
    }

    /// <summary>
    /// Method used to lock a block for the test run, so that two runs of the same test project never share its ports
    /// </summary>
    /// <param name="first">First port of the block</param>
    /// <returns>
    /// The lock file, held open
    /// </returns>
    /// <exception cref="InvalidOperationException">If another test run holds the block</exception>
    private static FileStream LockBlock(int first)
    {
        // Opening the lock file of the block, shared with nobody
        string path = Path.Combine(Path.GetTempPath(), $"BoardGamesPlatform-test-ports-{first}.lock");
        try
        {
            // Function's return
            return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException($"Another test run uses the test ports {first}-{first + BlockSize - 1} (another worktree or a coding agent running the same " +
                $"test project): let it end, or give this run other ports with the environment variable {BaseVariable}", exception);
        }
    }

    /// <summary>
    /// Method used to try to bind a port on an address, then release it at once
    /// </summary>
    /// <param name="address">[Cannot be null] Address to bind</param>
    /// <param name="port">Port to bind</param>
    /// <returns>
    /// Null when the port could be bound, else the socket error
    /// </returns>
    private static SocketException? TryBind(IPAddress address, int port)
    {
        // Binding then releasing the port
        TcpListener probe = new TcpListener(address, port);
        try
        {
            probe.Start();

            // Function's return
            return null;
        }
        catch (SocketException exception)
        {
            // Function's return
            return exception;
        }
        finally
        {
            probe.Stop();
        }
    }

    /// <summary>
    /// Method used to find why a port cannot be used: a program bound to it on the loopback address or on all the addresses
    /// </summary>
    /// <param name="port">Port to check</param>
    /// <returns>
    /// Null when the port is free on both, else the first socket error
    /// </returns>
    private static SocketException? FindHolder(int port)
    {
        // Function's return
        return TryBind(IPAddress.Loopback, port) ?? TryBind(IPAddress.Any, port);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to get the next port of the block, checked to be free
    /// </summary>
    /// <returns>
    /// A port of the block that no other test of the run has had, and that could be bound on the loopback address and on all the addresses
    /// </returns>
    /// <exception cref="InvalidOperationException">If the block is used up, or if the port cannot be bound</exception>
    public int Next()
    {
        // Taking the next offset of the block
        int offset = Interlocked.Increment(ref _lastOffset);
        if (offset >= BlockSize)
            throw new InvalidOperationException($"The {BlockSize} test ports from {First} are all used by this test project: give it a second block in TestPortBlocks");

        // Checking the port, then giving it
        int port = First + offset;
        SocketException? holder = FindHolder(port);
        if (holder is not null)
        {
            throw new InvalidOperationException($"The test port {port} cannot be bound ({holder.SocketErrorCode}): another program uses it. " +
                $"Stop that program, or move the test ports with the environment variable {BaseVariable}", holder);
        }

        // Function's return
        return port;
    }

    /// <summary>
    /// Method used to check that a port of the delivered configuration of a tier is free, before a test starts the tier on it
    /// </summary>
    /// <param name="port">Port written in the delivered configuration</param>
    /// <param name="tierName">[Cannot be null] Name of the tier, for the message, for example "Very Back"</param>
    /// <exception cref="InvalidOperationException">If the port cannot be bound</exception>
    public static void EnsureDeliveredPortFree(int port, string tierName)
    {
        // Checking the port; the variable of the test ports does not move a delivered port, so the remedy is to stop the tier
        SocketException? holder = FindHolder(port);
        if (holder is not null)
        {
            throw new InvalidOperationException($"The port {port} of the delivered configuration of the {tierName} cannot be bound ({holder.SocketErrorCode}): " +
                $"a {tierName} probably runs on this machine. Stop it, then run the tests again", holder);
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
