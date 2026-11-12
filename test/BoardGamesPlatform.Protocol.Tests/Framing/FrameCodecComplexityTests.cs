/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Framing/FrameCodecComplexityTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the memory and the work of a frame written or read: bounded, and in proportion to the frame
 */

// Using
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Framing;

/// <summary>
/// Tests used to check the space and time complexity of the frame codec
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> every request and response of every link goes through the codec: a copy of the payload at each frame, or a work
/// that grows faster than the frame, is paid on all the traffic of the platform.</para>
/// <para><b>Behavior:</b> the frames go to and come from streams in memory, so that the codec completes synchronously and its allocations
/// are measured on the thread of the test (<see cref="AllocationMeter"/>). A frame is measured with a small and a large payload: the
/// difference is the cost of each byte of payload (the slope), the small one bounds the fixed cost of a frame. The work is counted in reads
/// of the stream, never measured in time, so that the load of the machine never changes the result.</para>
/// </remarks>
public sealed class FrameCodecComplexityTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Payload of the small frame measured
    /// </summary>
    private const int SmallPayload = 16;

    /// <summary>
    /// Payload of the large frame measured: the largest payload of a link
    /// </summary>
    private const int LargePayload = 64 * 1024;

    /// <summary>
    /// Bytes a frame may allocate whatever its payload: its header, its tasks and their state
    /// </summary>
    private const long FixedCostLimit = 1024;

    /// <summary>
    /// Bytes the difference between the two frames may differ from the expected slope: the alignment of the arrays
    /// </summary>
    private const long SlopeTolerance = 64;

    /// <summary>
    /// Correlation identifier used by the tests
    /// </summary>
    private const ulong CorrelationId = 42;

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
    /// Method used to build the bytes of one frame, as the codec writes it
    /// </summary>
    /// <param name="payloadLength">Bytes of the payload</param>
    /// <returns>
    /// The header and the payload of the frame
    /// </returns>
    private static byte[] FrameBytes(int payloadLength)
    {
        // Writing one frame to a stream in memory
        using MemoryStream written = new MemoryStream();
        new FrameCodec().WriteAsync(written, new Frame(CorrelationId, new byte[payloadLength]), TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        // Function's return
        return written.ToArray();
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the write of one frame, to a stream that never grows
    /// </summary>
    /// <param name="payloadLength">Bytes of the payload</param>
    /// <returns>
    /// The bytes allocated by one write
    /// </returns>
    private static long MeasureWrite(int payloadLength)
    {
        // Arranging a stream that never grows, so that only the codec allocates
        FrameCodec codec = new FrameCodec();
        Frame frame = new Frame(CorrelationId, new byte[payloadLength]);
        MemoryStream stream = new MemoryStream(new byte[FrameCodec.HeaderLength + payloadLength]);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            stream.Position = 0;

            // Function's return
            return new ValueTask(codec.WriteAsync(stream, frame, token));
        });
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the read of one frame
    /// </summary>
    /// <param name="payloadLength">Bytes of the payload</param>
    /// <returns>
    /// The bytes allocated by one read, the payload given to the caller included
    /// </returns>
    private static long MeasureRead(int payloadLength)
    {
        // Arranging the bytes of one frame
        FrameCodec codec = new FrameCodec();
        MemoryStream stream = new MemoryStream(FrameBytes(payloadLength), writable: false);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            stream.Position = 0;

            // Function's return
            return new ValueTask(codec.ReadAsync(stream, token));
        });
    }

    #endregion

    #region Publics

    /// <summary>
    /// A frame written allocates the same few bytes whatever the size of its payload: the payload is never copied into a new buffer (space
    /// O(1) per frame)
    /// </summary>
    [Fact]
    public void AWrittenFrameAllocatesTheSameWhateverItsPayload()
    {
        // Acting
        long small = MeasureWrite(SmallPayload);
        long large = MeasureWrite(LargePayload);

        // Checks: no byte per byte of payload, and a bounded fixed cost
        (large - small).ShouldBeLessThan(SlopeTolerance);
        small.ShouldBeLessThan(FixedCostLimit);
    }

    /// <summary>
    /// A frame read allocates its payload, which it gives to the caller, and a fixed cost that does not grow with it (space O(n) for a
    /// payload of n bytes, the payload itself)
    /// </summary>
    [Fact]
    public void AReadFrameAllocatesItsPayloadAndAFixedCost()
    {
        // Acting
        long small = MeasureRead(SmallPayload);
        long large = MeasureRead(LargePayload);

        // Checks: one byte per byte of payload, and a bounded fixed cost
        (large - small).ShouldBeInRange(LargePayload - SmallPayload - SlopeTolerance, LargePayload - SmallPayload + SlopeTolerance);
        (small - SmallPayload).ShouldBeLessThan(FixedCostLimit);
    }

    /// <summary>
    /// A frame whose bytes arrive one by one is read with one read per byte, never reading a byte twice (time O(n))
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AFrameArrivingByteByByteIsReadInOnePassAsync()
    {
        // Arranging a payload of 4 KiB delivered one byte at a time
        const int PayloadLength = 4096;
        ChunkedReadStream stream = new ChunkedReadStream(FrameBytes(PayloadLength), chunkSize: 1);

        // Acting
        Frame? frame = await new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        frame.ShouldNotBeNull().Payload.Length.ShouldBe(PayloadLength);
        stream.ReadCount.ShouldBe(FrameCodec.HeaderLength + PayloadLength);
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
