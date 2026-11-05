/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Correlation/GatedWriteConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connection of the tests whose writes only go through when the test lets them, as towards a peer that stops reading
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Correlation;

/// <summary>
/// Connection whose writes wait for the test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a write blocked by a peer that never reads depends on the buffers of the system; this connection makes it exact: a
/// write waits until the test lets one through, so a test on a fake clock knows when the writer is stuck.</para>
/// <para><b>Behavior:</b> wraps a real connection; each write through <see cref="Stream"/> tells <see cref="WaitForWriteAsync"/> that it
/// started, then waits for <see cref="AllowWrite"/> (or for its token, which makes it throw an <see cref="OperationCanceledException"/>), then
/// writes to the connection wrapped. Reads, flushes and everything else go to the connection wrapped.</para>
/// </remarks>
internal sealed class GatedWriteConnection : ITransportConnection
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Connection wrapped
    /// </summary>
    private readonly ITransportConnection _inner;

    /// <summary>
    /// Stream whose writes wait
    /// </summary>
    private readonly GatedStream _stream;

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
    public Stream Stream => _stream;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => _inner.RemoteEndPoint;

    /// <inheritdoc/>
    public string? PeerIdentity => _inner.PeerIdentity;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] Connection to wrap; this one takes ownership of it</param>
    public GatedWriteConnection(ITransportConnection inner)
    {
        // Setting
        _inner = inner;
        _stream = new GatedStream(inner.Stream);
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
    /// Method used to wait until a write has started and waits to be let through
    /// </summary>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once a write waits
    /// </returns>
    public Task WaitForWriteAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return _stream.Started.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to let one write through
    /// </summary>
    public void AllowWrite()
    {
        // Opening the gate once
        _stream.Gate.Release();
    }

    /// <inheritdoc/>
    public Task CloseSendAsync(CancellationToken cancellationToken = default)
    {
        // Function's return
        return _inner.CloseSendAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Function's return: closing the connection wrapped (the semaphores hold no system resource)
        return _inner.DisposeAsync();
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

    /// <summary>
    /// Stream that passes everything to the stream wrapped, each write waiting for the gate
    /// </summary>
    /// <param name="inner">Stream wrapped, owned by the connection</param>
    private sealed class GatedStream(Stream inner) : Stream
    {
        /// <summary>
        /// Released when a write starts to wait; never disposed, a semaphore whose wait handle is never asked for holds no system resource
        /// </summary>
        public SemaphoreSlim Started { get; } = new SemaphoreSlim(0);

        /// <summary>
        /// Released by the test to let one write through
        /// </summary>
        public SemaphoreSlim Gate { get; } = new SemaphoreSlim(0);

        /// <inheritdoc/>
        public override bool CanRead => inner.CanRead;

        /// <inheritdoc/>
        public override bool CanSeek => false;

        /// <inheritdoc/>
        public override bool CanWrite => inner.CanWrite;

        /// <inheritdoc/>
        public override long Length => throw new NotSupportedException();

        /// <inheritdoc/>
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        /// <inheritdoc/>
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Function's return
            return inner.ReadAsync(buffer, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Function's return
            return inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Function's return
            return inner.Read(buffer, offset, count);
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Telling the test, waiting for the gate, then writing
            Started.Release();
            await Gate.WaitAsync(cancellationToken);
            await inner.WriteAsync(buffer, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Function's return
            return WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Writing through the gate
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            // Function's return
            return inner.FlushAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override void Flush()
        {
            // Flushing
            inner.Flush();
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
    }
}
