/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Framing/ChunkedReadStream.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Read-only stream that delivers its bytes a few at a time, like a slow network connection
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Framing;

/// <summary>
/// Read-only stream used to deliver a known content in small pieces
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a memory stream returns everything in one read, which never exercises the partial reads a TCP connection produces.
/// This stream returns at most a given number of bytes per read, down to one byte at a time.</para>
/// <para><b>Behavior:</b> wraps a byte array; every read returns between one byte and the chunk size, until the end of the content.</para>
/// </remarks>
internal sealed class ChunkedReadStream : Stream
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Content delivered by the stream
    /// </summary>
    private readonly byte[] _content;

    /// <summary>
    /// Largest number of bytes returned by one read
    /// </summary>
    private readonly int _chunkSize;

    /// <summary>
    /// Index of the next byte to deliver
    /// </summary>
    private int _position;

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
    /// Number of reads that returned at least one byte, so that a test can prove the content arrived in pieces
    /// </summary>
    public int ReadCount { get; private set; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="content">[Cannot be null] Content delivered by the stream</param>
    /// <param name="chunkSize">Largest number of bytes returned by one read</param>
    /// <exception cref="ArgumentNullException">If the content is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the chunk size is zero or negative</exception>
    public ChunkedReadStream(byte[] content, int chunkSize)
    {
        // Checks
        if (content is null)
            throw new ArgumentNullException(nameof(content), $"The value of the parameter: {nameof(content)} cannot be null");
        if (chunkSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(chunkSize), chunkSize, $"The value of the parameter: {nameof(chunkSize)} must be greater than zero");

        // Setting
        _content = content;
        _chunkSize = chunkSize;
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

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>
    /// Method used to deliver the next piece of the content
    /// </summary>
    /// <param name="buffer">[Cannot be null] Buffer receiving the bytes</param>
    /// <param name="offset">Index in the buffer of the first byte written</param>
    /// <param name="count">Room left in the buffer</param>
    /// <returns>
    /// The number of bytes delivered, at most the chunk size; zero at the end of the content
    /// </returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Setting the size of this piece
        int length = Math.Min(Math.Min(count, _chunkSize), _content.Length - _position);

        // Delivering it
        Array.Copy(_content, _position, buffer, offset, length);
        _position += length;
        if (length > 0)
            ReadCount++;

        // Function's return
        return length;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // Nothing to flush: the stream is read-only
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
