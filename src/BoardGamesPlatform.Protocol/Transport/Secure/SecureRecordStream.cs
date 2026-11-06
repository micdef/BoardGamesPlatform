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
/// The connection is then unusable, and is closed by its owner.</description></item>
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
    /// Whether the close record of this end was sent
    /// </summary>
    private bool _hasSentClose;

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
    public SecureRecordStream(Stream inner, SessionSecrets secrets, bool isClient, long hardLimit = DefaultHardLimit, long renewalThreshold = DefaultRenewalThreshold,
        Action<SecureChannelFailure>? onFailure = null)
    {
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
        byte[] header = new byte[HeaderLength];
        if (!await ReadExactAsync(header, allowEndAtStart: true, cancellationToken).ConfigureAwait(false))
            throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
        int length = BinaryPrimitives.ReadUInt16BigEndian(header);
        if (length > MaxRecordLength)
            throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
        long sequence = _receiveSequence;
        if (sequence >= _hardLimit)
            throw new SecureChannelException(SecureChannelFailure.RecordLimitReached);

        // Reading the ciphertext and the tag, then opening the record with the nonce of its sequence number
        await ReadExactAsync(_receiveBuffer.AsMemory(0, length + TagLength), allowEndAtStart: false, cancellationToken).ConfigureAwait(false);
        byte[] nonce = new byte[SessionSecrets.IvLength];
        BuildNonce(_receiveIv, sequence, nonce);
        try
        {
            _receiveCipher.Decrypt(nonce, _receiveBuffer.AsSpan(0, length), _receiveBuffer.AsSpan(length, TagLength), _plaintext.AsSpan(0, length), header);
        }
        catch (AuthenticationTagMismatchException exception)
        {
            throw new SecureChannelException(sequence == 0 ? SecureChannelFailure.KeyConfirmationFailed : SecureChannelFailure.RecordAuthenticationFailed, exception);
        }
        Interlocked.Exchange(ref _receiveSequence, sequence + 1);

        // Giving the plaintext, or taking the close: nothing may follow it
        if (length == 0)
        {
            byte[] probe = new byte[1];
            if (await _inner.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
                throw new SecureChannelException(SecureChannelFailure.RecordAuthenticationFailed);
            _hasReceivedClose = true;
            return;
        }
        _plaintextOffset = 0;
        _plaintextCount = length;
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
    private async Task WriteRecordAsync(ReadOnlyMemory<byte> plaintext, CancellationToken cancellationToken)
    {
        // Refusing a record beyond the hard limit
        long sequence = _sendSequence;
        if (sequence >= _hardLimit)
            throw new SecureChannelException(SecureChannelFailure.RecordLimitReached);

        // Sealing the record: its length, its ciphertext, then its tag
        int length = plaintext.Length;
        byte[] record = new byte[HeaderLength + length + TagLength];
        BinaryPrimitives.WriteUInt16BigEndian(record, (ushort)length);
        byte[] nonce = new byte[SessionSecrets.IvLength];
        BuildNonce(_sendIv, sequence, nonce);
        _sendCipher.Encrypt(nonce, plaintext.Span, record.AsSpan(HeaderLength, length), record.AsSpan(HeaderLength + length, TagLength), record.AsSpan(0, HeaderLength));
        Interlocked.Exchange(ref _sendSequence, sequence + 1);

        // Writing it
        await _inner.WriteAsync(record, cancellationToken).ConfigureAwait(false);
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
        // Writing it once, after any record being written
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hasSentClose)
                return;
            _hasSentClose = true;
            await WriteRecordAsync(ReadOnlyMemory<byte>.Empty, cancellationToken).ConfigureAwait(false);
            await _inner.FlushAsync(cancellationToken).ConfigureAwait(false);
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
        // Nothing asked, nothing read
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);
        if (buffer.Length == 0)
            return 0;

        // Opening the next record when the last one is given entirely; the close record of the peer is the end of the stream
        while (_plaintextCount == 0)
        {
            if (_hasReceivedClose)
                return 0;
            try
            {
                await ReadRecordAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SecureChannelException exception)
            {
                _onFailure?.Invoke(exception.Reason);
                throw;
            }
        }

        // Giving what the reader takes of the record
        int count = Math.Min(buffer.Length, _plaintextCount);
        _plaintext.AsMemory(_plaintextOffset, count).CopyTo(buffer);
        _plaintextOffset += count;
        _plaintextCount -= count;
        if (_plaintextCount == 0)
            CryptographicOperations.ZeroMemory(_plaintext);

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
        // Nothing to write, no record
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _isDisposed) == 1, this);
        if (buffer.Length == 0)
            return;

        // Writing the records one after the other, none after the close record
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_hasSentClose)
                throw new InvalidOperationException("The close record was sent: nothing can be written after it");
            for (int offset = 0; offset < buffer.Length; offset += MaxRecordLength)
                await WriteRecordAsync(buffer.Slice(offset, Math.Min(MaxRecordLength, buffer.Length - offset)), cancellationToken).ConfigureAwait(false);
        }
        catch (SecureChannelException exception)
        {
            _onFailure?.Invoke(exception.Reason);
            throw;
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
        // Erasing the keys once; the stream below belongs to the connection
        if (disposing && Interlocked.Exchange(ref _isDisposed, 1) == 0)
        {
            _sendCipher.Dispose();
            _receiveCipher.Dispose();
            CryptographicOperations.ZeroMemory(_sendIv);
            CryptographicOperations.ZeroMemory(_receiveIv);
            CryptographicOperations.ZeroMemory(_plaintext);
            _writeLock.Dispose();
        }
        base.Dispose(disposing);
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
