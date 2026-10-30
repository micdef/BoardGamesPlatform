/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Framing/FrameCodecTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that frames survive any way a stream delivers their bytes, and that malformed or oversized frames are refused
 */

// Using
using System.Buffers.Binary;
using System.Text;
using BoardGamesPlatform.Protocol.Framing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Framing;

/// <summary>
/// Tests used to check the frame codec of the socket protocol
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the framing is the first defense of every link: a wrong boundary corrupts every following message, and an unchecked
/// length lets a peer exhaust the memory of the receiver.</para>
/// <para><b>Behavior:</b> frames are written to a memory stream and read back from it, whole or in small pieces; malformed streams are
/// built byte by byte to check the refusals.</para>
/// </remarks>
public sealed class FrameCodecTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Correlation identifier used by the tests; large enough to use the 8 bytes of the field
    /// </summary>
    private const ulong CorrelationId = 0x0102030405060708;

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
    /// Method used to write frames with the codec and return the bytes produced
    /// </summary>
    /// <param name="codec">[Cannot be null] Codec used to write</param>
    /// <param name="frames">[Cannot be null] Frames to write, in order</param>
    /// <returns>
    /// The bytes written to the stream
    /// </returns>
    private static async Task<byte[]> WriteFramesAsync(FrameCodec codec, params Frame[] frames)
    {
        // Writing the frames
        using MemoryStream stream = new MemoryStream();
        foreach (Frame frame in frames)
            await codec.WriteAsync(stream, frame, TestContext.Current.CancellationToken);

        // Function's return
        return stream.ToArray();
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
    /// A frame read back has the correlation identifier and the payload it was written with
    /// </summary>
    [Fact]
    public async Task AFrameSurvivesARoundTripAsync()
    {
        // Writing a frame
        FrameCodec codec = new FrameCodec();
        byte[] payload = Encoding.UTF8.GetBytes("{\"ping\":true}");
        byte[] bytes = await WriteFramesAsync(codec, new Frame(CorrelationId, payload));

        // Reading it back
        using MemoryStream stream = new MemoryStream(bytes);
        Frame? frame = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        frame.ShouldNotBeNull();
        frame.CorrelationId.ShouldBe(CorrelationId);
        frame.Payload.ToArray().ShouldBe(payload);
    }

    /// <summary>
    /// A frame with an empty payload is valid and survives a round trip
    /// </summary>
    [Fact]
    public async Task AnEmptyPayloadSurvivesARoundTripAsync()
    {
        // Writing a frame without payload
        FrameCodec codec = new FrameCodec();
        byte[] bytes = await WriteFramesAsync(codec, new Frame(CorrelationId, ReadOnlyMemory<byte>.Empty));

        // Reading it back
        using MemoryStream stream = new MemoryStream(bytes);
        Frame? frame = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        bytes.Length.ShouldBe(FrameCodec.HeaderLength);
        frame.ShouldNotBeNull();
        frame.CorrelationId.ShouldBe(CorrelationId);
        frame.Payload.Length.ShouldBe(0);
    }

    /// <summary>
    /// The header is written in the agreed layout: payload length on 4 bytes, then the correlation identifier on 8 bytes, both big-endian
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> pins the wire format, which both ends of a link must share; a change here is a change of the protocol.</para>
    /// </remarks>
    [Fact]
    public async Task TheHeaderFollowsTheWireFormatAsync()
    {
        // Writing a frame of 3 bytes
        byte[] bytes = await WriteFramesAsync(new FrameCodec(), new Frame(CorrelationId, new byte[] { 0xAA, 0xBB, 0xCC }));

        // Check
        bytes.ShouldBe(new byte[] { 0x00, 0x00, 0x00, 0x03, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0xAA, 0xBB, 0xCC });
    }

    #endregion

    #region Partial and grouped reads

    /// <summary>
    /// Frames delivered one byte at a time are read whole and in order
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a TCP read may return any part of a frame; the codec must wait for the missing bytes.</para>
    /// </remarks>
    [Fact]
    public async Task FramesDeliveredOneByteAtATimeAreReadWholeAsync()
    {
        // Writing two frames
        FrameCodec codec = new FrameCodec();
        byte[] bytes = await WriteFramesAsync(codec, new Frame(1, new byte[] { 1, 2, 3 }), new Frame(2, new byte[] { 4, 5 }));

        // Reading them from a stream that delivers one byte per read
        using ChunkedReadStream stream = new ChunkedReadStream(bytes, 1);
        Frame? first = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);
        Frame? second = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        first.ShouldNotBeNull();
        first.CorrelationId.ShouldBe(1UL);
        first.Payload.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        second.ShouldNotBeNull();
        second.CorrelationId.ShouldBe(2UL);
        second.Payload.ToArray().ShouldBe(new byte[] { 4, 5 });
        stream.ReadCount.ShouldBe(bytes.Length);
    }

    /// <summary>
    /// Several frames received in one read are separated, and each read stops exactly at the end of its frame
    /// </summary>
    [Fact]
    public async Task SeveralFramesInOneReadAreSeparatedAsync()
    {
        // Writing three frames into one buffer
        FrameCodec codec = new FrameCodec();
        byte[] bytes = await WriteFramesAsync(codec, new Frame(1, new byte[] { 10 }), new Frame(2, ReadOnlyMemory<byte>.Empty), new Frame(3, new byte[] { 30, 31 }));

        // Reading them from one memory stream
        using MemoryStream stream = new MemoryStream(bytes);
        Frame? first = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);
        long afterFirst = stream.Position;
        Frame? second = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);
        Frame? third = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);
        Frame? end = await codec.ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        afterFirst.ShouldBe(FrameCodec.HeaderLength + 1);
        first.ShouldNotBeNull();
        first.CorrelationId.ShouldBe(1UL);
        second.ShouldNotBeNull();
        second.CorrelationId.ShouldBe(2UL);
        second.Payload.Length.ShouldBe(0);
        third.ShouldNotBeNull();
        third.Payload.ToArray().ShouldBe(new byte[] { 30, 31 });
        end.ShouldBeNull();
    }

    #endregion

    #region End of stream

    /// <summary>
    /// A stream that ends before the first byte of a frame is a clean end of the connection
    /// </summary>
    [Fact]
    public async Task AStreamEndingBetweenFramesIsACleanEndAsync()
    {
        // Reading from an empty stream
        using MemoryStream stream = new MemoryStream(Array.Empty<byte>());
        Frame? frame = await new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken);

        // Check
        frame.ShouldBeNull();
    }

    /// <summary>
    /// A stream that ends inside the header of a frame is refused
    /// </summary>
    [Fact]
    public async Task AStreamEndingInsideTheHeaderIsRefusedAsync()
    {
        // Setting a stream holding 5 of the 12 bytes of a header, delivered in pieces
        byte[] truncated = BuildHeader(3, CorrelationId).AsSpan(0, 5).ToArray();
        using ChunkedReadStream stream = new ChunkedReadStream(truncated, 2);

        // Check
        InvalidDataException exception = await Should.ThrowAsync<InvalidDataException>(() => new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("header");
    }

    /// <summary>
    /// A stream that ends inside the payload of a frame is refused
    /// </summary>
    [Fact]
    public async Task AStreamEndingInsideThePayloadIsRefusedAsync()
    {
        // Setting a stream holding a header that announces 10 bytes, followed by 4 bytes only
        byte[] truncated = BuildHeader(10, CorrelationId).Concat(new byte[] { 1, 2, 3, 4 }).ToArray();
        using ChunkedReadStream stream = new ChunkedReadStream(truncated, 3);

        // Check
        InvalidDataException exception = await Should.ThrowAsync<InvalidDataException>(() => new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("payload");
    }

    /// <summary>
    /// A stream that ends right after a complete header, before any byte of its payload, is refused and not taken for a clean end
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> guards the boundary between the clean end (no byte of a frame) and a truncated frame (a header without its payload).</para>
    /// </remarks>
    [Fact]
    public async Task AStreamEndingRightAfterTheHeaderIsRefusedAsync()
    {
        // Setting a stream holding a header that announces 10 bytes, and nothing else
        using MemoryStream stream = new MemoryStream(BuildHeader(10, CorrelationId));

        // Check
        InvalidDataException exception = await Should.ThrowAsync<InvalidDataException>(() => new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("payload");
    }

    #endregion

    #region Maximum length

    /// <summary>
    /// A frame announcing a payload larger than the maximum is refused before its payload is read
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves that a peer cannot make the receiver allocate the size it announces: the refusal happens right after the
    /// header, whatever follows.</para>
    /// </remarks>
    [Fact]
    public async Task AnOversizedFrameIsRefusedBeforeItsPayloadIsReadAsync()
    {
        // Setting a header announcing the largest possible payload (4 GiB - 1), followed by a few bytes
        byte[] bytes = BuildHeader(uint.MaxValue, CorrelationId).Concat(new byte[] { 1, 2, 3 }).ToArray();
        using MemoryStream stream = new MemoryStream(bytes);

        // Checks
        InvalidDataException exception = await Should.ThrowAsync<InvalidDataException>(() => new FrameCodec().ReadAsync(stream, TestContext.Current.CancellationToken));
        exception.Message.ShouldContain("maximum");
        stream.Position.ShouldBe(FrameCodec.HeaderLength);
    }

    /// <summary>
    /// A frame announcing one byte more than the maximum is refused right after its header
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> pins the limit itself: the maximum is the last accepted length, not an approximate bound.</para>
    /// </remarks>
    [Fact]
    public async Task AFrameOfOneByteOverTheMaximumIsRefusedAsync()
    {
        // Setting a header announcing 17 bytes for a codec accepting 16, followed by the 17 bytes
        byte[] bytes = BuildHeader(17, CorrelationId).Concat(new byte[17]).ToArray();
        using MemoryStream stream = new MemoryStream(bytes);

        // Checks
        await Should.ThrowAsync<InvalidDataException>(() => new FrameCodec(16).ReadAsync(stream, TestContext.Current.CancellationToken));
        stream.Position.ShouldBe(FrameCodec.HeaderLength);
    }

    /// <summary>
    /// A frame whose payload is exactly the maximum length is accepted, as sent by a peer
    /// </summary>
    [Fact]
    public async Task AFrameOfExactlyTheMaximumLengthIsAcceptedAsync()
    {
        // Setting a raw frame of the maximum length of a small codec
        byte[] payload = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        byte[] bytes = BuildHeader(16, CorrelationId).Concat(payload).ToArray();

        // Reading it
        using MemoryStream stream = new MemoryStream(bytes);
        Frame? frame = await new FrameCodec(16).ReadAsync(stream, TestContext.Current.CancellationToken);

        // Checks
        frame.ShouldNotBeNull();
        frame.Payload.ToArray().ShouldBe(payload);
    }

    /// <summary>
    /// The codec refuses to write a payload larger than the maximum, and writes nothing
    /// </summary>
    [Fact]
    public async Task AnOversizedPayloadIsNeverWrittenAsync()
    {
        // Writing a payload one byte too long
        FrameCodec codec = new FrameCodec(16);
        using MemoryStream stream = new MemoryStream();

        // Checks
        await Should.ThrowAsync<ArgumentException>(() => codec.WriteAsync(stream, new Frame(CorrelationId, new byte[17]), TestContext.Current.CancellationToken));
        stream.Length.ShouldBe(0);
    }

    /// <summary>
    /// A codec cannot be built with a maximum length of zero or less, or above the highest allowed maximum
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a configured maximum cannot let a peer make the receiver allocate more than the ceiling of the protocol.</para>
    /// </remarks>
    /// <param name="maxPayloadLength">Invalid maximum length</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(FrameCodec.HighestMaxPayloadLength + 1)]
    [InlineData(int.MaxValue)]
    public void AnInvalidMaximumLengthIsRefused(int maxPayloadLength)
    {
        // Check
        Should.Throw<ArgumentOutOfRangeException>(() => new FrameCodec(maxPayloadLength));
    }

    /// <summary>
    /// A codec can be built with the highest allowed maximum length
    /// </summary>
    [Fact]
    public void TheHighestMaximumLengthIsAccepted()
    {
        // Building the codec
        FrameCodec codec = new FrameCodec(FrameCodec.HighestMaxPayloadLength);

        // Check
        codec.MaxPayloadLength.ShouldBe(FrameCodec.HighestMaxPayloadLength);
    }

    #endregion

    #region Cancellation and arguments

    /// <summary>
    /// A read canceled before it starts ends with an operation-canceled exception
    /// </summary>
    [Fact]
    public async Task ACanceledReadEndsWithACancellationAsync()
    {
        // Setting a stream holding a whole frame, and a token already canceled
        byte[] bytes = await WriteFramesAsync(new FrameCodec(), new Frame(CorrelationId, new byte[] { 1 }));
        using MemoryStream stream = new MemoryStream(bytes);
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Check (a TaskCanceledException is an OperationCanceledException too)
        Exception exception = await Should.ThrowAsync<Exception>(() => new FrameCodec().ReadAsync(stream, cancellation.Token));
        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    /// <summary>
    /// A write canceled before it starts ends with an operation-canceled exception and writes nothing
    /// </summary>
    [Fact]
    public async Task ACanceledWriteEndsWithACancellationAsync()
    {
        // Setting an empty stream and a token already canceled
        using MemoryStream stream = new MemoryStream();
        using CancellationTokenSource cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Checks
        Exception exception = await Should.ThrowAsync<Exception>(() => new FrameCodec().WriteAsync(stream, new Frame(CorrelationId, new byte[] { 1 }), cancellation.Token));
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        stream.Length.ShouldBe(0);
    }

    /// <summary>
    /// A null stream or a null frame is refused
    /// </summary>
    [Fact]
    public async Task NullArgumentsAreRefusedAsync()
    {
        // Setting
        FrameCodec codec = new FrameCodec();
        using MemoryStream stream = new MemoryStream();

        // Checks
        await Should.ThrowAsync<ArgumentNullException>(() => codec.ReadAsync(null!, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => codec.WriteAsync(null!, new Frame(CorrelationId, ReadOnlyMemory<byte>.Empty), TestContext.Current.CancellationToken));
        await Should.ThrowAsync<ArgumentNullException>(() => codec.WriteAsync(stream, null!, TestContext.Current.CancellationToken));
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
