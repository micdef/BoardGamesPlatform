/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Correlation/ObservedConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connection of the tests that tells how many bytes its reader has taken, so that a test moves a fake clock only once they are read
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
/// Connection that counts the bytes read from it and written to it
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a test of the heartbeat on a fake clock must move the clock only once the peer has read what was sent, otherwise the
/// clock may run ahead of the reads and the test fails for a reason of its own, not of the code.</para>
/// <para><b>Behavior:</b> wraps a real connection; every read through <see cref="Stream"/> adds its bytes to the count, and
/// <see cref="WaitForReadAsync"/> waits until the count reaches a total. Everything else goes to the connection wrapped.</para>
/// </remarks>
internal sealed class ObservedConnection : ITransportConnection
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
    /// Stream that counts the reads
    /// </summary>
    private readonly CountingStream _stream;

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
    public ObservedConnection(ITransportConnection inner)
    {
        // Setting
        _inner = inner;
        _stream = new CountingStream(inner.Stream);
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
    /// Method used to wait until the reader has taken at least the number of bytes given
    /// </summary>
    /// <param name="total">Bytes read since the start of the connection</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once that many bytes are read
    /// </returns>
    public Task WaitForReadAsync(long total, CancellationToken cancellationToken)
    {
        // Function's return
        return _stream.WaitForReadAsync(total, cancellationToken);
    }

    /// <summary>
    /// Bytes written to the connection so far, once each write ended
    /// </summary>
    public long Written => _stream.Written;

    /// <summary>
    /// Method used to wait until the writer has written at least the number of bytes given, each write ended
    /// </summary>
    /// <param name="total">Bytes written since the start of the connection</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes once that many bytes are written
    /// </returns>
    public Task WaitForWrittenAsync(long total, CancellationToken cancellationToken)
    {
        // Function's return
        return _stream.WaitForWrittenAsync(total, cancellationToken);
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
        // Function's return: closing the connection wrapped (the counter holds no system resource)
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
    /// Stream that passes everything to the stream wrapped, counting the bytes read
    /// </summary>
    /// <param name="inner">Stream wrapped, owned by the connection</param>
    private sealed class CountingStream(Stream inner) : Stream
    {
        /// <summary>
        /// Bytes read so far
        /// </summary>
        private long _read;

        /// <summary>
        /// Bytes written so far, once each write ended
        /// </summary>
        private long _written;

        /// <summary>
        /// Released at each read, so that a waiter checks the count again; never disposed, since a read may still end after the connection is
        /// closed, and a semaphore whose wait handle is never asked for holds no system resource
        /// </summary>
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);

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

        /// <summary>
        /// Method used to wait until the count reaches a total
        /// </summary>
        /// <param name="total">Bytes expected</param>
        /// <param name="cancellationToken">Token of the test</param>
        /// <returns>
        /// A task that completes once that many bytes are read
        /// </returns>
        public async Task WaitForReadAsync(long total, CancellationToken cancellationToken)
        {
            // Checking again after each read
            while (Interlocked.Read(ref _read) < total)
                await _signal.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Bytes written so far, once each write ended
        /// </summary>
        public long Written => Interlocked.Read(ref _written);

        /// <summary>
        /// Method used to wait until the writes reach a total
        /// </summary>
        /// <param name="total">Bytes expected</param>
        /// <param name="cancellationToken">Token of the test</param>
        /// <returns>
        /// A task that completes once that many bytes are written
        /// </returns>
        public async Task WaitForWrittenAsync(long total, CancellationToken cancellationToken)
        {
            // Checking again after each read or write
            while (Interlocked.Read(ref _written) < total)
                await _signal.WaitAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Reading, then counting and telling the waiters
            int read = await inner.ReadAsync(buffer, cancellationToken);
            Interlocked.Add(ref _read, read);
            _signal.Release();

            // Function's return
            return read;
        }

        /// <inheritdoc/>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Function's return
            return ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        /// <inheritdoc/>
        public override int Read(byte[] buffer, int offset, int count)
        {
            // Function's return
            return ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        }

        /// <inheritdoc/>
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Writing, then counting and telling the waiters
            await inner.WriteAsync(buffer, cancellationToken);
            Interlocked.Add(ref _written, buffer.Length);
            _signal.Release();
        }

        /// <inheritdoc/>
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            // Function's return
            return inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        /// <inheritdoc/>
        public override void Write(byte[] buffer, int offset, int count)
        {
            // Writing
            inner.Write(buffer, offset, count);
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
