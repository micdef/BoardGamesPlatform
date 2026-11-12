/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureRecordStream.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Byte stream of the secure channel: AES-256-GCM records with an implicit sequence number and a close record
 */

// Using
using System.Buffers.Binary;
using System.Security.Cryptography;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Stream that encrypts what is written into records, and decrypts the records read, over the stream of a TLS session
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the protocol above sees a plain byte stream, as with TLS; the records keep a party in the middle of TLS from reading,
/// changing, replaying, reordering, dropping or cutting anything.</para>
/// <para><b>Behavior:</b></para>
/// <list type="bullet">
/// <item><description>A record is the length of the plaintext (2 bytes, 0 to <see cref="MaxRecordLength"/>), the ciphertext, then the tag of
/// 16 bytes. Its nonce is the IV of the direction XOR the 64-bit sequence number of the record, never sent; its associated data is the length.
/// A record of length 0 is the close record of its direction.</description></item>
/// <item><description>Writes: a write of zero bytes sends nothing; a longer write is cut into records. Writes are serialized inside the stream,
/// so that a close record sent by another task (a revocation) never mixes with a record being written. Reads: one reader at a time, as with
/// any stream of a connection.</description></item>
/// <item><description>A tag that fails (<see cref="SecureChannelFailure.KeyConfirmationFailed"/> for the first record, else
/// <see cref="SecureChannelFailure.RecordAuthenticationFailed"/>), a length above the maximum, a stream that ends inside a record or without the
/// close record, a byte after the close record, or a record beyond the hard limit of a direction throws a <see cref="SecureChannelException"/>.
/// The connection is then unusable, and is closed by its owner. A failed read breaks the reads for good, a failed write the writes, a
/// cancellation included (the next calls fail as a lost connection); a failed read leaves the writes sound, so that the close record is still
/// sent. A write that would pass the hard limit is refused before anything is written, and the stream stays sound.</description></item>
/// <item><description><see cref="IsRenewalDue"/> tells, from the renewal threshold of records on either direction, that the connection should
/// be replaced before its hard limit.</description></item>
/// </list>
/// <para>Disposing the stream erases its keys; the stream below is owned by the connection, which closes it.</para>
/// </remarks>
internal sealed class SecureRecordStream : Stream
{
    #region Members

    #region Constants

    /// <summary>
    /// Largest plaintext of a record, in bytes
    /// </summary>
    public const int MaxRecordLength = 16 * 1024;

    /// <summary>
    /// Size of the length of a record, in bytes
    /// </summary>
    public const int HeaderLength = 2;

    /// <summary>
    /// Size of the tag of a record, in bytes
    /// </summary>
    public const int TagLength = 16;

    /// <summary>
    /// Hard limit of the records of a direction: below the 2^24.5 full-size records RFC 8446 (section 5.5) allows for AES-GCM with one key
    /// </summary>
    public const long DefaultHardLimit = 1L << 24;

    /// <summary>
    /// Number of records of a direction from which the connection should be replaced
    /// </summary>
    public const long DefaultRenewalThreshold = 1L << 23;

    #endregion

    #region Variables

    /// <summary>
    /// Stream of the TLS session below
    /// </summary>
    private readonly Stream _inner;

    /// <summary>
    /// Cipher of the records sent
    /// </summary>
    private readonly AesGcm _sendCipher;

    /// <summary>
    /// Cipher of the records received
    /// </summary>
    private readonly AesGcm _receiveCipher;

    /// <summary>
    /// IV of the records sent
    /// </summary>
    private readonly byte[] _sendIv;

    /// <summary>
    /// IV of the records received
    /// </summary>
    private readonly byte[] _receiveIv;

    /// <summary>
    /// Hard limit of the records of a direction
    /// </summary>
    private readonly long _hardLimit;

    /// <summary>
    /// Records of a direction from which a renewal is due
    /// </summary>
    private readonly long _renewalThreshold;

    /// <summary>
    /// Called with the reason of a record refused, for the logs of the connection; null for nothing
    /// </summary>
    private readonly Action<SecureChannelFailure>? _onFailure;

    /// <summary>
    /// Lock of the writes: one record at a time
    /// </summary>
    private readonly SemaphoreSlim _writeLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Buffer of a whole record read: its ciphertext then its tag
    /// </summary>
    private readonly byte[] _receiveBuffer = new byte[MaxRecordLength + TagLength];

    /// <summary>
    /// Length of the record read: kept by the stream, as the nonce and the probe below, since one reader reads at a time
    /// </summary>
    private readonly byte[] _receiveHeader = new byte[HeaderLength];

    /// <summary>
    /// Nonce of the record read
    /// </summary>
    private readonly byte[] _receiveNonce = new byte[SessionSecrets.IvLength];

    /// <summary>
    /// One byte read after the close record of the peer, which must find the end of the stream
    /// </summary>
    private readonly byte[] _closeProbe = new byte[1];

    /// <summary>
    /// Buffer of a whole record written, its length, its ciphertext then its tag, and the nonce of that record: kept by the stream, so that a
    /// record allocates nothing; under <see cref="_writeLock"/>
    /// </summary>
    private readonly byte[] _sendRecord = new byte[HeaderLength + MaxRecordLength + TagLength];

    /// <summary>
    /// Nonce of the record written; under <see cref="_writeLock"/>
    /// </summary>
    private readonly byte[] _sendNonce = new byte[SessionSecrets.IvLength];

    /// <summary>
    /// Plaintext of the last record read, not yet given to the reader
    /// </summary>
    private readonly byte[] _plaintext = new byte[MaxRecordLength];

    /// <summary>
    /// Offset of the next byte of <see cref="_plaintext"/> to give
    /// </summary>
    private int _plaintextOffset;

    /// <summary>
    /// Number of bytes of <see cref="_plaintext"/> left to give
    /// </summary>
    private int _plaintextCount;

    /// <summary>
    /// Number of bytes the last record wrote in <see cref="_plaintext"/>: only these are erased once the record is given, never the whole
    /// buffer, so that a peer that sends tiny records cannot make each one cost the erasure of 16 KiB
    /// </summary>
    private int _plaintextLength;

    /// <summary>
    /// Sequence number of the next record sent
    /// </summary>
    private long _sendSequence;

    /// <summary>
    /// Sequence number of the next record received
    /// </summary>
    private long _receiveSequence;

    /// <summary>
    /// Whether the close record of the peer was received
    /// </summary>
    private bool _hasReceivedClose;

    /// <summary>
    /// Whether the close record of this end was started: true once it is being written, even when its write then failed
    /// </summary>
    private bool _hasSentClose;

    /// <summary>
    /// First failure that broke the reads; null while they are sound. A broken read never stops the close record: the writes are apart
    /// </summary>
    private Exception? _readBrokenBy;

    /// <summary>
    /// First failure that broke the writes; null while they are sound
    /// </summary>
    private Exception? _writeBrokenBy;

    /// <summary>
    /// Whether the stream was disposed (0: no, 1: yes)
    /// </summary>
    private int _isDisposed;

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
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException("A stream of a connection has no length");

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException("A stream of a connection has no position");
        set => throw new NotSupportedException("A stream of a connection has no position");
    }

    /// <summary>
    /// Whether either direction reached the renewal threshold, so that the connection should be replaced
    /// </summary>
    public bool IsRenewalDue => Interlocked.Read(ref _sendSequence) >= _renewalThreshold || Interlocked.Read(ref _receiveSequence) >= _renewalThreshold;

    /// <summary>
    /// Whether the close record of this end was started: true once it is being written, even when its write then failed
    /// </summary>
    public bool HasSentClose => Volatile.Read(ref _hasSentClose);

    /// <summary>
    /// Whether the stream can still send records, the close record included: not disposed, and its writes not broken by an earlier failure
    /// (a failed or canceled read, as the end of a request client, never stops the close record)
    /// </summary>
    public bool IsSound => Volatile.Read(ref _isDisposed) == 0 && Volatile.Read(ref _writeBrokenBy) is null;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] Stream of the TLS session, not owned</param>
    /// <param name="secrets">[Cannot be null] Secrets of the handshake; erased by this constructor once the ciphers are built</param>
    /// <param name="isClient">Whether this end is the client of the connection</param>
    /// <param name="hardLimit">Hard limit of the records of a direction (smaller in the tests only)</param>
    /// <param name="renewalThreshold">Records of a direction from which a renewal is due (smaller in the tests only)</param>
    /// <param name="onFailure">Called with the reason of a record refused; null for nothing</param>
    /// <exception cref="ArgumentNullException">If the stream or the secrets are null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the hard limit is below 2</exception>
    public SecureRecordStream(Stream inner, SessionSecrets secrets, bool isClient, long hardLimit = DefaultHardLimit, long renewalThreshold = DefaultRenewalThreshold,
        Action<SecureChannelFailure>? onFailure = null)
    {
        // Checks
        if (inner is null)
            throw new ArgumentNullException(nameof(inner), $"The value of the parameter: {nameof(inner)} cannot be null");
        if (secrets is null)
            throw new ArgumentNullException(nameof(secrets), $"The value of the parameter: {nameof(secrets)} cannot be null");
        if (hardLimit < 2)
            throw new ArgumentOutOfRangeException(nameof(hardLimit), $"The value of the parameter: {nameof(hardLimit)} must keep one record for the close, so 2 at least");

        // Setting: the client sends with the keys client → server, the server with the keys server → client
        _inner = inner;
        _sendCipher = new AesGcm(isClient ? secrets.ClientToServerKey : secrets.ServerToClientKey, TagLength);
        _receiveCipher = new AesGcm(isClient ? secrets.ServerToClientKey : secrets.ClientToServerKey, TagLength);
        _sendIv = (byte[])(isClient ? secrets.ClientToServerIv : secrets.ServerToClientIv).Clone();
        _receiveIv = (byte[])(isClient ? secrets.ServerToClientIv : secrets.ClientToServerIv).Clone();
        _hardLimit = hardLimit;
        _renewalThreshold = renewalThreshold;
        _onFailure = onFailure;
        secrets.Dispose();
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build the nonce of a record
    /// </summary>
    /// <param name="iv">IV of the direction</param>
    /// <param name="sequence">Sequence number of the record</param>
    /// <param name="nonce">Nonce of 12 bytes to fill</param>
    private static void BuildNonce(ReadOnlySpan<byte> iv, long sequence, Span<byte> nonce)
    {
        // The IV XOR (four zero bytes ‖ the sequence number, big-endian), as in TLS 1.3
        iv.CopyTo(nonce);
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, sequence);
        for (int index = 0; index < counter.Length; index++)
            nonce[nonce.Length - counter.Length + index] ^= counter[index];
    }

    /// <summary>
    /// Method used to read exactly the bytes asked, a stream that ends before being a truncation
    /// </summary>
    /// <param name="buffer">Bytes to fill</param>
    /// <param name="allowEndAtStart">Whether an end of the stream before the first byte is returned as false instead of thrown</param>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// True once the buffer is full; false when the stream ended before the first byte and that is allowed
    /// </returns>
    /// <exception cref="SecureChannelException">If the stream ends inside the bytes asked</exception>
    private async ValueTask<bool> ReadExactAsync(Memory<byte> buffer, bool allowEndAtStart, CancellationToken cancellationToken)
    {
        // Reading until the buffer is full
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await _inner.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                if (total == 0 && allowEndAtStart)
                    return false;
                throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
            }
            total += read;
        }

        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to read and open the next record into <see cref="_plaintext"/>
    /// </summary>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once a record is open; the close record sets <see cref="_hasReceivedClose"/>
    /// </returns>
    /// <exception cref="SecureChannelException">If the record is refused</exception>
    private async Task ReadRecordAsync(CancellationToken cancellationToken)
    {
        // Reading the length: a stream that ends there without the close record is a truncation too
        byte[] header = _receiveHeader;
        if (!await ReadExactAsync(header, allowEndAtStart: true, cancellationToken).ConfigureAwait(false))
            throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
        int length = BinaryPrimitives.ReadUInt16BigEndian(header);
        if (length > MaxRecordLength)
            throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
        long sequence = _receiveSequence;
        if (sequence >= _hardLimit || (length > 0 && sequence >= _hardLimit - 1))
            throw new SecureChannelException(SecureChannelFailure.RecordLimitReached);

        // Reading the ciphertext and the tag, then opening the record with the nonce of its sequence number
        await ReadExactAsync(_receiveBuffer.AsMemory(0, length + TagLength), allowEndAtStart: false, cancellationToken).ConfigureAwait(false);
        byte[] nonce = _receiveNonce;
        BuildNonce(_receiveIv, sequence, nonce);
        try
        {
            _receiveCipher.Decrypt(nonce, _receiveBuffer.AsSpan(0, length), _receiveBuffer.AsSpan(length, TagLength), _plaintext.AsSpan(0, length), header);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            // Nothing of a refused record stays in the buffer: the cipher clears the plaintext when the tag cannot be validated (documented by
            // AesGcm.Decrypt; checked by a test of the records)
            throw new SecureChannelException(sequence == 0 ? SecureChannelFailure.KeyConfirmationFailed : SecureChannelFailure.RecordAuthenticationFailed, exception);
        }
        Interlocked.Exchange(ref _receiveSequence, sequence + 1);

        // Giving the plaintext, or taking the close: nothing may follow it
        if (length == 0)
        {
            if (await _inner.ReadAsync(_closeProbe, cancellationToken).ConfigureAwait(false) != 0)
                throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
            _hasReceivedClose = true;
            return;
        }
        _plaintextOffset = 0;
        _plaintextCount = length;
        _plaintextLength = length;
    }

    /// <summary>
    /// Method used to refuse a call on a stream that is closed, or whose direction is broken by an earlier failure
    /// </summary>
    /// <param name="isWrite">Whether the call writes; false for a read</param>
    /// <exception cref="IOException">If the stream is closed or the direction broken: the connection is unusable, as when its socket fails</exception>
    private void ThrowIfUnusable(bool isWrite)
    {
        // Checks: an IOException, as for a lost connection, so that the callers end the connection the usual way
        if (Volatile.Read(ref _isDisposed) == 1)
            throw new IOException("The secure channel of the connection is closed");
        if (Volatile.Read(ref isWrite ? ref _writeBrokenBy : ref _readBrokenBy) is { } cause)
            throw new IOException("The secure channel of the connection failed earlier; the connection must be closed", cause);
    }

    /// <summary>
    /// Method used to break the stream after a failure that may have left it inside a record, and tell the failure of the channel
    /// </summary>
    /// <param name="exception">[Cannot be null] Failure</param>
    /// <param name="isWrite">Whether the failure is the one of a write; false for a read</param>
    /// <returns>
    /// The exception to throw: the failure itself, or an <see cref="IOException"/> when the stream was disposed during the call
    /// </returns>
    private Exception Break(Exception exception, bool isWrite)
    {
        // Breaking the direction for every later call, the first failure kept as the cause
        Interlocked.CompareExchange(ref isWrite ? ref _writeBrokenBy : ref _readBrokenBy, exception, null);

        // Telling a refusal of the channel to the connection, for its logs
        if (exception is SecureChannelException secure)
            Tell(secure);

        // Function's return: a stream disposed under a call is a closed connection for the caller
        return exception is ObjectDisposedException && Volatile.Read(ref _isDisposed) == 1
            ? new IOException("The secure channel of the connection was closed during the call", exception)
            : exception;
    }

    /// <summary>
    /// Method used to tell a refusal of the channel to the connection, for its logs; a failure of the log never hides the refusal
    /// </summary>
    /// <param name="refusal">[Cannot be null] Refusal</param>
    private void Tell(SecureChannelException refusal)
    {
        try
        {
            _onFailure?.Invoke(refusal.Reason);
        }
        catch (Exception logFailure) when (logFailure is not OutOfMemoryException)
        {
            // The refusal itself is thrown by the caller: nothing of it is lost
        }
    }

    /// <summary>
    /// Method used to seal and write one record; under <see cref="_writeLock"/>
    /// </summary>
    /// <param name="plaintext">Plaintext of the record, up to <see cref="MaxRecordLength"/> bytes; empty for the close record</param>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once the record is written
    /// </returns>
    /// <exception cref="SecureChannelException">If the hard limit of records is reached</exception>
    /// <exception cref="IOException">If the stream below fails</exception>
    private async Task WriteRecordAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        // Refusing a record beyond the hard limit; the last sequence number is kept for the close record, so that a connection that reaches
        // the limit still ends cleanly
        long sequence = _sendSequence;
        if (sequence >= _hardLimit || (plaintext.Length > 0 && sequence >= _hardLimit - 1))
            throw new SecureChannelException(SecureChannelFailure.RecordLimitReached);

        // Sealing the record into the buffer of the stream: its length, its ciphertext, then its tag
        int length = plaintext.Length;
        byte[] record = _sendRecord;
        BinaryPrimitives.WriteUInt16BigEndian(record, (ushort)length);
        BuildNonce(_sendIv, sequence, _sendNonce);
        _sendCipher.Encrypt(_sendNonce, plaintext.Span, record.AsSpan(HeaderLength, length), record.AsSpan(HeaderLength + length, TagLength), record.AsSpan(0, HeaderLength));
        Interlocked.Exchange(ref _sendSequence, sequence + 1);

        // Writing it
        await _inner.WriteAsync(record.AsMemory(0, HeaderLength + length + TagLength), cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to send the close record of this end: nothing can be written after it
    /// </summary>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once the close record is written and flushed; at once when it was sent already
    /// </returns>
    /// <exception cref="SecureChannelException">If the hard limit of records is reached</exception>
    /// <exception cref="IOException">If the stream below fails</exception>
    public async Task WriteCloseAsync(CancellationToken cancellationToken = default)
    {
        // Writing it once, after any record being written, on a stream that is still sound
        ThrowIfUnusable(isWrite: true);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hasSentClose)
                return;
            ThrowIfUnusable(isWrite: true);
            _hasSentClose = true;
            await WriteRecordAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            throw Break(exception, isWrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Nothing asked, nothing read; a stream closed, or whose reads are broken, reads nothing more
        ThrowIfUnusable(isWrite: false);
        if (buffer.Length == 0)
            return 0;

        // Opening the next record when the last one is given entirely; the close record of the peer is the end of the stream. Any failure,
        // a cancellation included, may leave the stream inside a record: it breaks the reads for good, never the writes, so that the close
        // record can still be sent
        while (_plaintextCount == 0)
        {
            if (_hasReceivedClose)
                return 0;
            try
            {
                await ReadRecordAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                throw Break(exception, isWrite: false);
            }
        }

        // Giving what the reader takes of the record
        int count = Math.Min(buffer.Length, _plaintextCount);
        _plaintext.AsMemory(_plaintextOffset, count).CopyTo(buffer);
        _plaintextOffset += count;
        _plaintextCount -= count;
        if (_plaintextCount == 0)
            CryptographicOperations.ZeroMemory(_plaintext.AsSpan(0, _plaintextLength));

        // Function's return
        return count;
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
        // Function's return: the stream of a connection is read asynchronously; a synchronous read waits for it
        return ReadAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // Nothing to write, no record; a stream closed, or whose writes are broken, writes nothing more
        ThrowIfUnusable(isWrite: true);
        if (buffer.Length == 0)
            return;

        // Writing the records one after the other, none after the close record; a failure while writing, a cancellation included, may leave
        // a part of a record below: it breaks the writes for good
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hasSentClose)
                throw new InvalidOperationException("The close record was sent: nothing can be written after it");
            ThrowIfUnusable(isWrite: true);

            // Refusing at once, before anything is written, a write whose records would pass the hard limit: the stream stays sound, and the
            // close record keeps the last sequence number
            long records = (buffer.Length + MaxRecordLength - 1) / MaxRecordLength;
            if (Interlocked.Read(ref _sendSequence) + records > _hardLimit - 1)
            {
                SecureChannelException limit = new SecureChannelException(SecureChannelFailure.RecordLimitReached);
                Tell(limit);
                throw limit;
            }
            for (int offset = 0; offset < buffer.Length; offset += MaxRecordLength)
            {
                try
                {
                    await WriteRecordAsync(buffer.Slice(offset, Math.Min(MaxRecordLength, buffer.Length - offset)), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    throw Break(exception, isWrite: true);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
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
        // The stream of a connection is written asynchronously; a synchronous write waits for it
        WriteAsync(buffer.AsMemory(offset, count), CancellationToken.None).AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        // Function's return: the records are written at once; flushing the stream below
        return _inner.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        // Flushing the stream below
        _inner.Flush();
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        // Function's return
        throw new NotSupportedException("A stream of a connection cannot seek");
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        // Function's return
        throw new NotSupportedException("A stream of a connection has no length");
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        // Erasing the keys once, and the nonces, which give the IV back from the sequence; the stream below belongs to the connection. The
        // ciphers go first, so that no record is sealed with an erased IV or nonce
        if (disposing && Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _sendCipher.Dispose();
            _receiveCipher.Dispose();
            CryptographicOperations.ZeroMemory(_sendIv);
            CryptographicOperations.ZeroMemory(_receiveIv);
            CryptographicOperations.ZeroMemory(_sendNonce);
            CryptographicOperations.ZeroMemory(_receiveNonce);
            CryptographicOperations.ZeroMemory(_plaintext);

            // The lock of the writes is never disposed: a write may still wait for it or release it, and a semaphore whose wait handle nobody
            // asked for holds no system resource
        }
        base.Dispose(disposing);
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
