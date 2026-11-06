/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/RecordingStream.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Stream that copies every byte written through it, as a party in the middle would record them
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Stream over another one, which records the bytes written, to replay them
/// </summary>
internal sealed class RecordingStream : Stream
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Stream below
    /// </summary>
    private readonly Stream _inner;

    /// <summary>
    /// Copy of the bytes written
    /// </summary>
    private readonly Stream _recorded;

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

    /// <inheritdoc/>
    public override bool CanRead => _inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => _inner.CanWrite;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] Stream below, not owned</param>
    /// <param name="recorded">[Cannot be null] Stream receiving the copy, not owned</param>
    public RecordingStream(Stream inner, Stream recorded)
    {
        // Setting
        _inner = inner;
        _recorded = recorded;
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
    public override void Flush()
    {
        // Flushing below
        _inner.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return _inner.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Function's return
        return _inner.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Function's return
        return _inner.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        // Function's return
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        // Function's return
        throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Recording, then writing below
        _recorded.Write(buffer, offset, count);
        _inner.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Recording, then writing below
        await _recorded.WriteAsync(buffer, cancellationToken);
        await _inner.WriteAsync(buffer, cancellationToken);
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
