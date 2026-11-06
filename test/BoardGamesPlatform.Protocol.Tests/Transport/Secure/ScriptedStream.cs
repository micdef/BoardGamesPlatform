/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/ScriptedStream.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Stream that gives bytes written in advance by a test, and keeps what is written to it
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
/// One end of a connection played by a test: the peer "sends" bytes fixed in advance, then closes
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the messages a peer could forge (changed, truncated, random) are given to a handshake without sockets, so that many
/// of them run fast; what the handshake writes is kept, to check that it sent nothing.</para>
/// </remarks>
internal sealed class ScriptedStream : Stream
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Bytes the peer sends
    /// </summary>
    private readonly MemoryStream _input;

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
    /// Bytes written by the end under test
    /// </summary>
    public MemoryStream Written { get; } = new MemoryStream();

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

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
    /// <param name="input">[Cannot be null] Bytes the peer sends, then the end of the stream</param>
    public ScriptedStream(byte[] input)
    {
        // Setting
        _input = new MemoryStream(input, writable: false);
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
    public override int Read(byte[] buffer, int offset, int count)
    {
        // Function's return
        return _input.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Function's return
        return _input.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        // Keeping what is written
        Written.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Function's return
        return Written.WriteAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // Nothing buffered
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return Task.CompletedTask;
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

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
