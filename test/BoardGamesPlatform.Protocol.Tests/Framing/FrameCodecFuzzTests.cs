/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Framing/FrameCodecFuzzTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the properties of the frame codec on random frames and random bytes, with fixed seeds so that every failure can be replayed
 */

// Using
using System.Buffers.Binary;
using BoardGamesPlatform.Protocol.Framing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Framing;

/// <summary>
/// Property-based and fuzz tests used to check the frame codec of the socket protocol
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the codec reads bytes sent by a peer that cannot be trusted. Fixed examples prove the cases the authors thought of;
/// random inputs look for the others: whatever bytes arrive, the codec must return a frame, return null at a clean end, or throw
/// <see cref="InvalidDataException"/>, never anything else, and never wait forever.</para>
/// <para><b>Behavior:</b> every random value comes from a <see cref="Random"/> built with a fixed seed, so a run is always the same.
/// Every check prints its seed in its failure message: building a <see cref="Random"/> with that seed replays the failing case.</para>
/// </remarks>
public sealed class FrameCodecFuzzTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Maximum payload length of the codec used by the tests, in bytes; small, so that random payloads reach the limit often
    /// </summary>
    private const int MaxPayloadLength = 256;

    /// <summary>
    /// Number of seeds, hence of random cases, run by each test; the seeds are 1 to this value
    /// </summary>
    private const int SeedCount = 200;

    /// <summary>
    /// Largest number of frames written in one random stream
    /// </summary>
    private const int MaxFrameCount = 8;

    /// <summary>
    /// Longest random byte sequence given to the codec, in bytes
    /// </summary>
    private const int MaxRandomLength = 4096;

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
    /// Method used to build a random correlation identifier
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the two edges, zero and the highest value, come up often; the other values cover the 64 bits.</para>
    /// </remarks>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// The correlation identifier
    /// </returns>
    private static ulong NextCorrelationId(Random random)
    {
        // Picking an edge value or a random one
        int choice = random.Next(0, 4);
        if (choice == 0)
        {
            // Function's return
            return 0;
        }
        if (choice == 1)
        {
            // Function's return
            return ulong.MaxValue;
        }

        // Building a random value
        byte[] bytes = new byte[8];
        random.NextBytes(bytes);

        // Function's return
        return BinaryPrimitives.ReadUInt64BigEndian(bytes);
    }

    /// <summary>
    /// Method used to build a random frame whose payload fits in the maximum length
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> an empty payload and a payload of exactly the maximum length come up often; the other lengths are uniform.</para>
    /// </remarks>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <returns>
    /// The frame
    /// </returns>
    private static Frame NextFrame(Random random)
    {
        // Choosing the payload length
        int choice = random.Next(0, 4);
        int length = choice == 0 ? 0 : choice == 1 ? MaxPayloadLength : random.Next(0, MaxPayloadLength + 1);

        // Filling the payload
        byte[] payload = new byte[length];
        random.NextBytes(payload);

        // Function's return
        return new Frame(NextCorrelationId(random), payload);
    }

    /// <summary>
    /// Method used to write frames with the codec and return the bytes produced
    /// </summary>
    /// <param name="codec">[Cannot be null] Codec used to write</param>
    /// <param name="frames">[Cannot be null] Frames to write, in order</param>
    /// <returns>
    /// The bytes written to the stream
    /// </returns>
    private static async Task<byte[]> WriteFramesAsync(FrameCodec codec, IReadOnlyList<Frame> frames)
    {
        // Writing the frames
        using MemoryStream stream = new MemoryStream();
        foreach (Frame frame in frames)
            await codec.WriteAsync(stream, frame, TestContext.Current.CancellationToken);

        // Function's return
        return stream.ToArray();
    }

    /// <summary>
    /// Method used to build a random list of frames
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the random values</param>
    /// <param name="minimumCount">Smallest number of frames in the list</param>
    /// <returns>
    /// The frames, from <paramref name="minimumCount"/> to <see cref="MaxFrameCount"/> of them
    /// </returns>
    private static List<Frame> NextFrames(Random random, int minimumCount)
    {
        // Building the frames
        int count = random.Next(minimumCount, MaxFrameCount + 1);
        List<Frame> frames = new List<Frame>(count);
        for (int index = 0; index < count; index++)
            frames.Add(NextFrame(random));

        // Function's return
        return frames;
    }

    /// <summary>
    /// Method used to build a raw header, as a peer would send it
    /// </summary>
    /// <param name="payloadLength">Payload length announced by the header</param>
    /// <param name="correlationId">Correlation identifier of the header</param>
    /// <returns>
    /// The 12 bytes of the header
    /// </returns>
    private static byte[] BuildHeader(uint payloadLength, ulong correlationId)
    {
        // Setting the header
        byte[] header = new byte[FrameCodec.HeaderLength];
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), payloadLength);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(4, 8), correlationId);

        // Function's return
        return header;
    }

    #endregion

    #region Publics

    #region Round trips

    /// <summary>
    /// Random frames written to a stream are read back identical and in order, whatever the size of the pieces the stream delivers
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the property behind the whole framing: the boundaries of the frames survive any split of the bytes.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task RandomFramesSurviveARoundTripInRandomChunksAsync()
    {
        // Running the cases
        FrameCodec codec = new FrameCodec(MaxPayloadLength);
        for (int seed = 1; seed <= SeedCount; seed++)
        {
            // Writing random frames
            Random random = new Random(seed);
            List<Frame> frames = NextFrames(random, 0);
            byte[] bytes = await WriteFramesAsync(codec, frames);
            int chunkSize = random.Next(1, 65);
            string context = $"seed {seed}, chunk size {chunkSize}";

            // Reading them back in pieces
            using ChunkedReadStream stream = new ChunkedReadStream(bytes, chunkSize);
            foreach (Frame expected in frames)
            {
                Frame? actual = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);

                // Checks
                actual.ShouldNotBeNull(context);
                actual.CorrelationId.ShouldBe(expected.CorrelationId, context);
                actual.Payload.ToArray().ShouldBe(expected.Payload.ToArray(), context);
            }

            // Check: the clean end
            (await codec.ReadAsync(stream, TestContext.Current.CancellationToken)).ShouldBeNull(context);
        }
    }

    #endregion

    #region Random bytes

    /// <summary>
    /// Random bytes, or valid frames with some bytes changed, only ever give frames, a clean end or an <see cref="InvalidDataException"/>
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a hostile peer sends anything; any other exception type would escape the handling of the connection, and a
    /// loop that never ends would hold it forever.</para>
    /// <para><b>Behavior:</b> odd seeds give purely random bytes (almost always refused at the first header); even seeds give valid frames
    /// with a few bytes overwritten, which reaches the payload and the following headers. Each frame read consumes at least a header, so
    /// the read loop is bounded by the length of the input.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task RandomBytesOnlyGiveFramesANullOrAnInvalidDataExceptionAsync()
    {
        // Running the cases
        FrameCodec codec = new FrameCodec(MaxPayloadLength);
        for (int seed = 1; seed <= SeedCount; seed++)
        {
            // Building the input
            Random random = new Random(seed);
            byte[] bytes;
            if (seed % 2 == 1)
            {
                bytes = new byte[random.Next(0, MaxRandomLength + 1)];
                random.NextBytes(bytes);
            }
            else
            {
                bytes = await WriteFramesAsync(codec, NextFrames(random, 1));
                int changes = random.Next(1, 6);
                for (int change = 0; change < changes; change++)
                    bytes[random.Next(0, bytes.Length)] = (byte)random.Next(0, 256);
                if (random.Next(0, 2) == 0)
                    bytes = bytes[..random.Next(0, bytes.Length + 1)];
            }

            // Reading until the end, within a bound
            using MemoryStream stream = new MemoryStream(bytes);
            int maxReads = (bytes.Length / FrameCodec.HeaderLength) + 2;
            bool ended = false;
            for (int read = 0; read < maxReads && !ended; read++)
            {
                try
                {
                    Frame? frame = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);
                    if (frame is null)
                        ended = true;
                    else
                        frame.Payload.Length.ShouldBeLessThanOrEqualTo(MaxPayloadLength, $"seed {seed}");
                }
                catch (InvalidDataException)
                {
                    // The expected refusal: the connection would be closed here
                    ended = true;
                }
                catch (Exception exception) when (exception is not Shouldly.ShouldAssertException)
                {
                    Assert.Fail($"seed {seed}: unexpected {exception.GetType().Name}: {exception.Message}");
                }
            }

            // Check: the loop ended by itself, not by its bound
            ended.ShouldBeTrue($"seed {seed}: the reads never ended within {maxReads} reads");
        }
    }

    #endregion

    #region Truncation

    /// <summary>
    /// A valid stream cut inside its last frame throws an <see cref="InvalidDataException"/>; cut between two frames it ends cleanly
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a connection lost in the middle of a frame must never be taken for a normal end, and never give a partial frame.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task ATruncatedStreamIsRefusedInsideAFrameAndCleanBetweenFramesAsync()
    {
        // Running the cases
        FrameCodec codec = new FrameCodec(MaxPayloadLength);
        for (int seed = 1; seed <= SeedCount / 4; seed++)
        {
            // Writing random frames and locating the start of the last one
            Random random = new Random(seed);
            List<Frame> frames = NextFrames(random, 1);
            byte[] bytes = await WriteFramesAsync(codec, frames);
            int lastStart = bytes.Length - FrameCodec.HeaderLength - frames[^1].Payload.Length;

            // Cutting at every offset inside the last frame
            for (int cut = lastStart + 1; cut < bytes.Length; cut++)
            {
                string context = $"seed {seed}, cut at {cut} of {bytes.Length}";
                using MemoryStream stream = new MemoryStream(bytes, 0, cut);
                for (int index = 0; index < frames.Count - 1; index++)
                    (await codec.ReadAsync(stream, TestContext.Current.CancellationToken)).ShouldNotBeNull(context);

                // Check
                await Should.ThrowAsync<InvalidDataException>(() => codec.ReadAsync(stream, TestContext.Current.CancellationToken), context);
            }

            // Cutting at the frame boundary
            using MemoryStream boundaryStream = new MemoryStream(bytes, 0, lastStart);
            for (int index = 0; index < frames.Count - 1; index++)
                (await codec.ReadAsync(boundaryStream, TestContext.Current.CancellationToken)).ShouldNotBeNull($"seed {seed}");

            // Check
            (await codec.ReadAsync(boundaryStream, TestContext.Current.CancellationToken)).ShouldBeNull($"seed {seed}: cut at the boundary {lastStart}");
        }
    }

    #endregion

    #region Announced length

    /// <summary>
    /// An announced length above the maximum is refused after the header, before any payload byte is read
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the refusal protects the memory of the receiver; it must come before the allocation and the read of the payload.</para>
    /// <para><b>Behavior:</b> the header is followed by enough bytes to hold the payload of the largest accepted frame, so a read that did
    /// not stop would move the position past the header. Besides random lengths, the lengths just above the maximum and the highest
    /// 32-bit value are always tried.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task AnAnnouncedLengthAboveTheMaximumIsRefusedBeforeAnyPayloadByteIsReadAsync()
    {
        // Choosing the lengths: the edges, then random ones up to the highest 32-bit value
        FrameCodec codec = new FrameCodec(MaxPayloadLength);
        Random random = new Random(1);
        List<uint> lengths = new List<uint> { MaxPayloadLength + 1, MaxPayloadLength + 2, int.MaxValue, (uint)int.MaxValue + 1, uint.MaxValue };
        for (int index = 0; index < SeedCount; index++)
            lengths.Add((uint)random.NextInt64(MaxPayloadLength + 1L, (long)uint.MaxValue + 1));

        // Running the cases
        foreach (uint length in lengths)
        {
            // Building a header followed by bytes that must stay unread
            string context = $"announced length {length}";
            byte[] bytes = new byte[FrameCodec.HeaderLength + MaxPayloadLength];
            BuildHeader(length, NextCorrelationId(random)).CopyTo(bytes, 0);
            using MemoryStream stream = new MemoryStream(bytes);

            // Checks
            await Should.ThrowAsync<InvalidDataException>(() => codec.ReadAsync(stream, TestContext.Current.CancellationToken), context);
            stream.Position.ShouldBe(FrameCodec.HeaderLength, context);
        }
    }

    #endregion

    #region Oversized payload on write

    /// <summary>
    /// A payload above the maximum is refused by the write with an <see cref="ArgumentException"/>, and nothing is written
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the sender applies the same limit as the receiver, and a refused frame must leave the stream untouched, since
    /// a half-written frame would corrupt the connection.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task APayloadAboveTheMaximumIsRefusedByTheWriteAndNothingIsWrittenAsync()
    {
        // Running the cases
        FrameCodec codec = new FrameCodec(MaxPayloadLength);
        for (int seed = 1; seed <= SeedCount; seed++)
        {
            // Building a frame whose payload is 1 to 1024 bytes above the maximum
            Random random = new Random(seed);
            byte[] payload = new byte[MaxPayloadLength + random.Next(1, 1025)];
            random.NextBytes(payload);
            Frame frame = new Frame(NextCorrelationId(random), payload);
            using MemoryStream stream = new MemoryStream();

            // Checks
            await Should.ThrowAsync<ArgumentException>(() => codec.WriteAsync(stream, frame, TestContext.Current.CancellationToken), $"seed {seed}");
            stream.Length.ShouldBe(0, $"seed {seed}: bytes were written");
        }
    }

    #endregion

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
