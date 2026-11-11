/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureRecordStreamComplexityTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the memory, the work and the erasure of the records of the secure channel: a fixed cost, one record per 16 KiB
 */

// Using
using System.Reflection;
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.TestKit;

// Static using
using static BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors.SecureChannelVectors;

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check the space and time complexity of the records of the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> every byte between the Back and the Very Back goes through a record: a buffer allocated per record, of the size of
/// the record, is paid on all their traffic. Since the buffers of the records are kept by the stream, their erasure is checked too: once a
/// record is given, or refused, nothing of its plaintext stays in the stream.</para>
/// <para><b>Behavior:</b> the records go to and come from streams in memory, so that the stream completes synchronously and its
/// allocations are measured on the thread of the test (<see cref="AllocationMeter"/>), with a short and a full record: the difference is
/// the cost of each byte of record (the slope), the short one bounds the fixed cost of a record. The work is counted in writes of the stream
/// below, never measured in time. The buffer of the plaintext, which the stream never shows, is read by reflection.</para>
/// </remarks>
public sealed class SecureRecordStreamComplexityTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Bytes of the short record measured
    /// </summary>
    private const int ShortRecord = 16;

    /// <summary>
    /// Bytes a record may allocate whatever its length: the task of the call, and the objects that hold its progress
    /// </summary>
    private const long FixedCostLimit = 1024;

    /// <summary>
    /// Bytes the two records may differ by: the alignment of the arrays
    /// </summary>
    private const long SlopeTolerance = 64;

    /// <summary>
    /// Name of the buffer of the plaintext of the stream, whose erasure is checked
    /// </summary>
    private const string PlaintextField = "_plaintext";

    #endregion

    #region Variables

    /// <summary>
    /// Names of the nonces of the stream, the IV of their direction XOR a sequence number, erased by the disposal
    /// </summary>
    private static readonly string[] _nonceFields = new string[] { "_sendNonce", "_receiveNonce" };

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
    /// Method used to derive the secrets of a session, the same for both ends
    /// </summary>
    /// <returns>
    /// The secrets, owned by the stream that takes them
    /// </returns>
    private static SessionSecrets CreateSecrets()
    {
        // Function's return
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        return KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(peer));
    }

    /// <summary>
    /// Method used to read a buffer of a stream, which the stream never shows: the erasure of the records is checked on it
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream read</param>
    /// <param name="name">[Cannot be null] Name of the field of the buffer</param>
    /// <returns>
    /// The buffer of the stream
    /// </returns>
    /// <exception cref="InvalidOperationException">If the buffer is no longer found under its name: the test must follow the stream</exception>
    private static byte[] Buffer(SecureRecordStream stream, string name)
    {
        // Function's return
        FieldInfo field = typeof(SecureRecordStream).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"SecureRecordStream no longer has a field {name}: update this test, which checks its erasure");
        return field.GetValue(stream) as byte[] ?? throw new InvalidOperationException($"The field {name} of SecureRecordStream is not a byte array");
    }

    /// <summary>
    /// Method used to read the buffer of the plaintext of a stream
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream read</param>
    /// <returns>
    /// The buffer of the plaintext of the stream
    /// </returns>
    /// <exception cref="InvalidOperationException">If the buffer is no longer found under its name: the test must follow the stream</exception>
    private static byte[] PlaintextBuffer(SecureRecordStream stream)
    {
        // Function's return
        return Buffer(stream, PlaintextField);
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the write of one record, to a stream that never grows
    /// </summary>
    /// <param name="length">Bytes of the record</param>
    /// <returns>
    /// The bytes allocated by one write
    /// </returns>
    private static long MeasureWrite(int length)
    {
        // Arranging a stream that never grows, so that only the records allocate
        MemoryStream below = new MemoryStream(new byte[SecureRecordStream.HeaderLength + length + SecureRecordStream.TagLength]);
        using SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true);
        byte[] plaintext = new byte[length];
        CancellationToken token = TestContext.Current.CancellationToken;

        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            below.Position = 0;

            // Function's return
            return client.WriteAsync(plaintext, token);
        });
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the read of one record
    /// </summary>
    /// <param name="length">Bytes of the record</param>
    /// <returns>
    /// The bytes allocated by one read
    /// </returns>
    private static long MeasureRead(int length)
    {
        // Arranging one record per run of both series, written by the client before the measure
        const int Runs = 10;
        MemoryStream written = new MemoryStream();
        using (SecureRecordStream client = new SecureRecordStream(written, CreateSecrets(), isClient: true))
        {
            for (int record = 0; record < (2 * Runs) + AllocationMeter.DefaultWarmUps; record++)
                client.WriteAsync(new byte[length], TestContext.Current.CancellationToken).AsTask().GetAwaiter().GetResult();
        }
        using SecureRecordStream server = new SecureRecordStream(new MemoryStream(written.ToArray(), writable: false), CreateSecrets(), isClient: false);
        byte[] plaintext = new byte[length];
        CancellationToken token = TestContext.Current.CancellationToken;

        // Function's return: each run reads one whole record
        return AllocationMeter.PerRun(async () =>
        {
            int read = await server.ReadAsync(plaintext, token);
            if (read != length)
                throw new InvalidOperationException($"One read gave {read} bytes instead of the {length} of its record");
        }, Runs);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A record written allocates the same few bytes whatever its length: no buffer per record (space O(1) per record)
    /// </summary>
    [Fact]
    public void AWrittenRecordAllocatesTheSameWhateverItsLength()
    {
        // Acting
        long small = MeasureWrite(ShortRecord);
        long full = MeasureWrite(SecureRecordStream.MaxRecordLength);

        // Checks: no byte per byte of record, and a bounded fixed cost
        (full - small).ShouldBeLessThan(SlopeTolerance);
        small.ShouldBeLessThan(FixedCostLimit);
    }

    /// <summary>
    /// A record read allocates the same few bytes whatever its length: it is opened into the buffers of the stream (space O(1) per record).
    /// The read was already so before the records were kept in the stream: this test keeps it so
    /// </summary>
    [Fact]
    public void AReadRecordAllocatesTheSameWhateverItsLength()
    {
        // Acting
        long small = MeasureRead(ShortRecord);
        long full = MeasureRead(SecureRecordStream.MaxRecordLength);

        // Checks: no byte per byte of record, and a bounded fixed cost
        (full - small).ShouldBeLessThan(SlopeTolerance);
        small.ShouldBeLessThan(FixedCostLimit);
    }

    /// <summary>
    /// A write is cut into the fewest records: one write of the stream below per 16 KiB, each record with its length and its tag only (time
    /// O(n), and 18 bytes per 16 KiB)
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AWriteIsCutIntoTheFewestRecordsAsync()
    {
        // Arranging a write of 100 KiB: 6 full records and a last one of 4 KiB
        const int WriteLength = 100 * 1024;
        const int Records = (WriteLength + SecureRecordStream.MaxRecordLength - 1) / SecureRecordStream.MaxRecordLength;
        CountingMemoryStream below = new CountingMemoryStream();
        await using SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true);

        // Acting
        await client.WriteAsync(new byte[WriteLength], TestContext.Current.CancellationToken);

        // Checks: one write per record, and 18 bytes added per record
        below.Writes.ShouldBe(Records);
        below.Length.ShouldBe(WriteLength + (Records * (SecureRecordStream.HeaderLength + SecureRecordStream.TagLength)));
    }

    /// <summary>
    /// Once a record is given, no byte of its plaintext stays in the stream: a full record, then a record of 1 byte sealed and opened in the
    /// same buffers, are both read back whole, and the buffer of the plaintext is empty after each one
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AGivenRecordLeavesNoPlaintextInTheStreamAsync()
    {
        // Arranging a full record, then a record of 1 byte, both sealed in the same buffers of the client
        CancellationToken token = TestContext.Current.CancellationToken;
        byte[] full = Enumerable.Repeat((byte)0xAB, SecureRecordStream.MaxRecordLength).ToArray();
        byte[] single = new byte[] { 0xCD };
        MemoryStream written = new MemoryStream();
        await using (SecureRecordStream client = new SecureRecordStream(written, CreateSecrets(), isClient: true))
        {
            await client.WriteAsync(full, token);
            await client.WriteAsync(single, token);
        }
        await using SecureRecordStream server = new SecureRecordStream(new MemoryStream(written.ToArray(), writable: false), CreateSecrets(), isClient: false);
        byte[] plaintext = PlaintextBuffer(server);

        // Acting
        byte[] readFull = new byte[SecureRecordStream.MaxRecordLength];
        int fullRead = await server.ReadAsync(readFull, token);
        bool isEmptyAfterFull = plaintext.All(value => value == 0);
        byte[] readSingle = new byte[1];
        int singleRead = await server.ReadAsync(readSingle, token);
        bool isEmptyAfterSingle = plaintext.All(value => value == 0);

        // Checks
        fullRead.ShouldBe(SecureRecordStream.MaxRecordLength);
        readFull.ShouldBe(full);
        singleRead.ShouldBe(1);
        readSingle.ShouldBe(single);
        isEmptyAfterFull.ShouldBeTrue();
        isEmptyAfterSingle.ShouldBeTrue();
    }

    /// <summary>
    /// A refused record leaves no byte of plaintext in the stream, whatever the cipher wrote before it checked the tag
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ARefusedRecordLeavesNoPlaintextInTheStreamAsync()
    {
        // Arranging a full record whose ciphertext is changed
        CancellationToken token = TestContext.Current.CancellationToken;
        MemoryStream written = new MemoryStream();
        await using (SecureRecordStream client = new SecureRecordStream(written, CreateSecrets(), isClient: true))
            await client.WriteAsync(Enumerable.Repeat((byte)0xAB, SecureRecordStream.MaxRecordLength).ToArray(), token);
        byte[] bytes = written.ToArray();
        bytes[SecureRecordStream.HeaderLength + 10] ^= 0x01;
        await using SecureRecordStream server = new SecureRecordStream(new MemoryStream(bytes, writable: false), CreateSecrets(), isClient: false);

        // Acting
        await Should.ThrowAsync<SecureChannelException>(server.ReadAsync(new byte[SecureRecordStream.MaxRecordLength], token).AsTask());

        // Check
        PlaintextBuffer(server).All(value => value == 0).ShouldBeTrue();
    }

    /// <summary>
    /// A disposed stream keeps no nonce: a nonce gives the IV of its direction back from its sequence number, and the IV is erased
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ADisposedStreamKeepsNoNonceAsync()
    {
        // Arranging a stream that has sealed a record and opened one, so that both nonces hold a value
        CancellationToken token = TestContext.Current.CancellationToken;
        MemoryStream written = new MemoryStream();
        await using (SecureRecordStream client = new SecureRecordStream(written, CreateSecrets(), isClient: true))
            await client.WriteAsync(new byte[] { 1 }, token);
        MemoryStream below = new MemoryStream();
        below.Write(written.ToArray());
        below.Position = 0;
        SecureRecordStream stream = new SecureRecordStream(below, CreateSecrets(), isClient: false);
        _ = await stream.ReadAsync(new byte[1], token);
        await stream.WriteAsync(new byte[] { 2 }, token);
        bool wereSet = _nonceFields.All(name => Buffer(stream, name).Any(value => value != 0));

        // Acting
        await stream.DisposeAsync();

        // Checks
        wereSet.ShouldBeTrue();
        _nonceFields.ShouldAllBe(name => Buffer(stream, name).All(value => value == 0));
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
    /// Stream in memory that counts the writes it receives
    /// </summary>
    private sealed class CountingMemoryStream : MemoryStream
    {
        /// <summary>
        /// Number of writes received
        /// </summary>
        public int Writes { get; private set; }

        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Counting, then writing
            Writes++;

            // Function's return
            return base.WriteAsync(buffer, cancellationToken);
        }
    }
}
