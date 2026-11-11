/*
 *	@file	src/BoardGamesPlatform.Protocol/Framing/FrameCodec.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Writes frames to a byte stream and reads them back, whatever the way the stream delivers the bytes
 */

// Using
using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Framing;

/// <summary>
/// Codec used to cut a byte stream into frames, and frames into bytes
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a TCP connection delivers a stream of bytes, not messages: one read may return part of a frame, or several frames
/// at once. The codec restores the boundaries of each frame, and refuses a frame larger than the agreed maximum before allocating anything,
/// so that a peer cannot make the receiver reserve an unbounded amount of memory.</para>
/// <para><b>Behavior:</b> each frame is written as a fixed header followed by the payload. All numbers are big-endian (network byte order):</para>
/// <list type="bullet">
/// <item><description>bytes 0 to 3: length of the payload, an unsigned 32-bit integer;</description></item>
/// <item><description>bytes 4 to 11: correlation identifier, an unsigned 64-bit integer;</description></item>
/// <item><description>then the payload, exactly as many bytes as announced.</description></item>
/// </list>
/// <para>The codec works on any <see cref="Stream"/>: a network stream today, a TLS stream from 0.5.0. It is not thread-safe: on one stream,
/// at most one read and one write may run at the same time, and the caller serializes the writes.</para>
/// <para><b>Connection rule:</b> the codec keeps no state between two calls. When a read or a write ends with any exception, cancellation
/// included, the position in the stream is unknown: part of a frame may have been read or sent. The connection must then be closed, never
/// reused, otherwise the next frame can be read wrongly without any error. The only exceptions to this rule are the argument checks, thrown
/// before any byte is read or written.</para>
/// </remarks>
public sealed class FrameCodec
{
    #region Members

    #region Constants

    /// <summary>
    /// Length of the header of a frame, in bytes: the payload length (4) and the correlation identifier (8)
    /// </summary>
    public const int HeaderLength = 12;

    /// <summary>
    /// Maximum payload length used when none is given, in bytes (1 MiB)
    /// </summary>
    public const int DefaultMaxPayloadLength = 1024 * 1024;

    /// <summary>
    /// Highest maximum payload length a codec accepts, in bytes (16 MiB)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the receiver allocates the announced payload before its bytes arrive; this ceiling bounds the memory a single
    /// connection can make it reserve, whatever value is configured.</para>
    /// </remarks>
    public const int HighestMaxPayloadLength = 16 * 1024 * 1024;

    /// <summary>
    /// Largest piece of a frame written in one call, in bytes (64 KiB)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a frame larger than this is written piece by piece, so that the writer can tell, after each piece, that the bytes
    /// keep leaving: on a slow link, the heartbeat then sees a large frame progress instead of a silent peer.</para>
    /// </remarks>
    public const int WriteChunkLength = 64 * 1024;

    #endregion

    #region Variables

    /// <summary>
    /// Backing field of <see cref="MaxPayloadLength"/>
    /// </summary>
    private readonly int _maxPayloadLength;

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
    /// Largest payload this codec writes or accepts, in bytes
    /// </summary>
    public int MaxPayloadLength => _maxPayloadLength;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="maxPayloadLength">Largest payload written or accepted, in bytes, from 1 to <see cref="HighestMaxPayloadLength"/>;
    /// both ends of a link must use the same value</param>
    /// <exception cref="ArgumentOutOfRangeException">If the maximum length is zero, negative or higher than <see cref="HighestMaxPayloadLength"/></exception>
    public FrameCodec(int maxPayloadLength = DefaultMaxPayloadLength)
    {
        // Check
        if (maxPayloadLength <= 0 || maxPayloadLength > HighestMaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(maxPayloadLength), maxPayloadLength, $"The value of the parameter: {nameof(maxPayloadLength)} must be between 1 and {HighestMaxPayloadLength}");

        // Setting
        _maxPayloadLength = maxPayloadLength;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to fill a buffer from a stream, telling the caller after each read that returned bytes
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream to read from</param>
    /// <param name="buffer">Buffer to fill</param>
    /// <param name="onProgress">Called after each read that returned bytes; null when nobody needs it</param>
    /// <param name="cancellationToken">Token used to cancel the read</param>
    /// <returns>
    /// The number of bytes read: the length of the buffer, or less when the stream ended first
    /// </returns>
    private static async Task<int> FillAsync(Stream stream, Memory<byte> buffer, Action? onProgress, CancellationToken cancellationToken)
    {
        // Reading until the buffer is full or the stream ends
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
            onProgress?.Invoke();
        }

        // Function's return
        return total;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to write one frame to a stream
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> builds the header and the payload into one buffer taken from the shared pool (never a new one per frame, and
    /// erased before it goes back), writes it in a single call and flushes the stream: a small frame leaves as one network segment (one TLS
    /// record from 0.5.0) instead of two. A payload larger than the maximum is refused before anything is written. The payload must not be
    /// modified until the returned task completes.</para>
    /// <para>The same as <see cref="WriteAsync(Stream, Frame, Action?, CancellationToken)"/>, without telling the progress.</para>
    /// <para>After an exception, see the connection rule of <see cref="FrameCodec"/>: the connection is closed. A canceled write has an
    /// unknown outcome: the frame may have been sent in full, in part or not at all.</para>
    /// </remarks>
    /// <param name="stream">[Cannot be null] Stream to write to</param>
    /// <param name="frame">[Cannot be null] Frame to write</param>
    /// <param name="cancellationToken">Token used to cancel the write</param>
    /// <exception cref="ArgumentNullException">If the stream or the frame is null (before any write)</exception>
    /// <exception cref="ArgumentException">If the payload is larger than the maximum length (before any write)</exception>
    /// <exception cref="IOException">If the stream fails while writing, for example when the peer resets the connection</exception>
    /// <exception cref="OperationCanceledException">If the write is canceled</exception>
    /// <exception cref="ObjectDisposedException">If the stream is closed</exception>
    /// <exception cref="NotSupportedException">If the stream cannot be written, or if a TLS stream already runs another write</exception>
    /// <exception cref="InvalidOperationException">If the stream is not ready, for example a TLS stream before its authentication</exception>
    public Task WriteAsync(Stream stream, Frame frame, CancellationToken cancellationToken = default)
    {
        // Function's return
        return WriteAsync(stream, frame, null, cancellationToken);
    }

    /// <summary>
    /// Method used to write one frame to a stream, telling the caller when its bytes leave
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> builds the header and the payload into one buffer taken from the shared pool, so that a frame allocates no
    /// buffer of its size. The bytes of the frame are erased before the buffer goes back, since a payload may hold a secret: this erases the
    /// copy of the codec only, never the payload of the caller. The stream must not touch the buffer once its write task ended, as the
    /// contract of <see cref="Stream"/> requires. A frame up to
    /// <see cref="WriteChunkLength"/> bytes, header included, is written in a single call, so that a small frame leaves as one network
    /// segment (one TLS record from 0.5.0). A larger frame is written in pieces of that length, and <paramref name="onProgress"/> is called
    /// after each one: the bytes keep leaving, so the peer is reading them. The stream is flushed at the end. A payload larger than the
    /// maximum is refused before anything is written. The payload must not be modified until the returned task completes.</para>
    /// <para>After an exception, see the connection rule of <see cref="FrameCodec"/>: the connection is closed. A canceled write has an
    /// unknown outcome: the frame may have been sent in full, in part or not at all.</para>
    /// </remarks>
    /// <param name="stream">[Cannot be null] Stream to write to</param>
    /// <param name="frame">[Cannot be null] Frame to write</param>
    /// <param name="onProgress">Called after each piece written, the last one included; null when nobody needs it</param>
    /// <param name="cancellationToken">Token used to cancel the write</param>
    /// <exception cref="ArgumentNullException">If the stream or the frame is null (before any write)</exception>
    /// <exception cref="ArgumentException">If the payload is larger than the maximum length (before any write)</exception>
    /// <exception cref="IOException">If the stream fails while writing, for example when the peer resets the connection</exception>
    /// <exception cref="OperationCanceledException">If the write is canceled</exception>
    /// <exception cref="ObjectDisposedException">If the stream is closed</exception>
    /// <exception cref="NotSupportedException">If the stream cannot be written, or if a TLS stream already runs another write</exception>
    /// <exception cref="InvalidOperationException">If the stream is not ready, for example a TLS stream before its authentication</exception>
    public async Task WriteAsync(Stream stream, Frame frame, Action? onProgress, CancellationToken cancellationToken = default)
    {
        // Checks
        if (stream is null)
            throw new ArgumentNullException(nameof(stream), $"The value of the parameter: {nameof(stream)} cannot be null");
        if (frame is null)
            throw new ArgumentNullException(nameof(frame), $"The value of the parameter: {nameof(frame)} cannot be null");
        if (frame.Payload.Length > _maxPayloadLength)
            throw new ArgumentException($"The payload of the frame ({frame.Payload.Length} bytes) is larger than the maximum length ({_maxPayloadLength} bytes)", nameof(frame));

        // Setting the whole frame in one buffer of the shared pool, which may be longer than the frame: the header, then the payload
        int frameLength = HeaderLength + frame.Payload.Length;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(frameLength);
        try
        {
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(0, 4), (uint)frame.Payload.Length);
            BinaryPrimitives.WriteUInt64BigEndian(buffer.AsSpan(4, 8), frame.CorrelationId);
            frame.Payload.Span.CopyTo(buffer.AsSpan(HeaderLength));

            // Writing the frame in a single call when it is small, else piece by piece, telling the progress after each piece
            for (int offset = 0; offset < frameLength; offset += WriteChunkLength)
            {
                int length = Math.Min(WriteChunkLength, frameLength - offset);
                await stream.WriteAsync(buffer.AsMemory(offset, length), cancellationToken).ConfigureAwait(false);
                onProgress?.Invoke();
            }
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // Erasing the bytes of the frame, whatever the write did, then giving the buffer back: the next one who takes it never reads this
            // payload. Only the bytes written are erased, never the whole buffer, which the pool may have made up to twice as long
            CryptographicOperations.ZeroMemory(buffer.AsSpan(0, frameLength));
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Method used to read the next frame from a stream
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> waits until a whole header is received, checks the announced length against the maximum, then waits until
    /// the whole payload is received; the bytes may arrive in any number of reads. A stream that ends between two frames is a normal end
    /// of the connection; a stream that ends inside a frame is an error. The payload of the returned frame is a new array, owned by the caller.</para>
    /// <para>After an exception, cancellation included, see the connection rule of <see cref="FrameCodec"/>: the connection is closed, since
    /// its next bytes can no longer be trusted. A canceled read is never retried on the same stream.</para>
    /// <para>The same as <see cref="ReadAsync(Stream, Action?, CancellationToken)"/>, without telling the progress.</para>
    /// </remarks>
    /// <param name="stream">[Cannot be null] Stream to read from</param>
    /// <param name="cancellationToken">Token used to cancel the read</param>
    /// <returns>
    /// The frame read<br/>
    /// Null ==> The stream ended cleanly, before the first byte of a frame: the peer sends nothing more (it may still be reading, after
    /// a half close)
    /// </returns>
    /// <exception cref="ArgumentNullException">If the stream is null (before any read)</exception>
    /// <exception cref="InvalidDataException">If the stream ends inside a frame, or if the announced payload is larger than the maximum length</exception>
    /// <exception cref="IOException">If the stream fails while reading, for example when the peer resets the connection</exception>
    /// <exception cref="OperationCanceledException">If the read is canceled</exception>
    /// <exception cref="ObjectDisposedException">If the stream is closed</exception>
    /// <exception cref="NotSupportedException">If the stream cannot be read, or if a TLS stream already runs another read</exception>
    /// <exception cref="InvalidOperationException">If the stream is not ready, for example a TLS stream before its authentication</exception>
    public Task<Frame?> ReadAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        // Function's return
        return ReadAsync(stream, null, cancellationToken);
    }

    /// <summary>
    /// Method used to read the next frame from a stream, telling the caller each time bytes arrive
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> waits until a whole header is received, checks the announced length against the maximum, then waits until
    /// the whole payload is received; the bytes may arrive in any number of reads, and <paramref name="onProgress"/> is called after each
    /// read that returned bytes: on a slow link, the heartbeat sees a large frame arrive instead of a silent peer. A stream that ends between
    /// two frames is a normal end of the connection; a stream that ends inside a frame is an error. The payload of the returned frame is a
    /// new array, owned by the caller.</para>
    /// <para>After an exception, cancellation included, see the connection rule of <see cref="FrameCodec"/>: the connection is closed, since
    /// its next bytes can no longer be trusted. A canceled read is never retried on the same stream.</para>
    /// </remarks>
    /// <param name="stream">[Cannot be null] Stream to read from</param>
    /// <param name="onProgress">Called after each read that returned bytes; null when nobody needs it</param>
    /// <param name="cancellationToken">Token used to cancel the read</param>
    /// <returns>
    /// The frame read<br/>
    /// Null ==> The stream ended cleanly, before the first byte of a frame: the peer sends nothing more (it may still be reading, after
    /// a half close)
    /// </returns>
    /// <exception cref="ArgumentNullException">If the stream is null (before any read)</exception>
    /// <exception cref="InvalidDataException">If the stream ends inside a frame, or if the announced payload is larger than the maximum length</exception>
    /// <exception cref="IOException">If the stream fails while reading, for example when the peer resets the connection</exception>
    /// <exception cref="OperationCanceledException">If the read is canceled</exception>
    /// <exception cref="ObjectDisposedException">If the stream is closed</exception>
    /// <exception cref="NotSupportedException">If the stream cannot be read, or if a TLS stream already runs another read</exception>
    /// <exception cref="InvalidOperationException">If the stream is not ready, for example a TLS stream before its authentication</exception>
    public async Task<Frame?> ReadAsync(Stream stream, Action? onProgress, CancellationToken cancellationToken = default)
    {
        // Check
        if (stream is null)
            throw new ArgumentNullException(nameof(stream), $"The value of the parameter: {nameof(stream)} cannot be null");

        // Reading the header (a stream that ends before its first byte is a clean end of the connection)
        byte[] header = new byte[HeaderLength];
        int headerBytes = await FillAsync(stream, header, onProgress, cancellationToken).ConfigureAwait(false);
        if (headerBytes == 0)
        {
            // Function's return
            return null;
        }
        if (headerBytes < HeaderLength)
            throw new InvalidDataException($"The stream ended inside the header of a frame ({headerBytes} of {HeaderLength} bytes received)");

        // Checking the announced length before allocating the payload
        uint payloadLength = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(0, 4));
        ulong correlationId = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(4, 8));
        if (payloadLength > (uint)_maxPayloadLength)
            throw new InvalidDataException($"The frame announces a payload of {payloadLength} bytes, larger than the maximum length ({_maxPayloadLength} bytes)");

        // Reading the payload (the length fits in an int: it is not larger than the maximum)
        byte[] payload = new byte[(int)payloadLength];
        int payloadBytes = await FillAsync(stream, payload, onProgress, cancellationToken).ConfigureAwait(false);
        if (payloadBytes < payload.Length)
            throw new InvalidDataException($"The stream ended inside the payload of a frame ({payloadBytes} of {payload.Length} bytes received)");

        // Function's return
        return new Frame(correlationId, payload);
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
