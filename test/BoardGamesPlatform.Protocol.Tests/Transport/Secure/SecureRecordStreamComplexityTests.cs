/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureRecordStreamComplexityTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the memory and the work of the records of the secure channel: a fixed cost per record, and one record per 16 KiB
 */

// Using
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
/// the record, is paid on all their traffic.</para>
/// <para><b>Behavior:</b> the records go to and come from streams in memory, so that the stream completes synchronously and its
/// allocations are measured on the thread of the test (<see cref="AllocationMeter"/>), with a short and a full record: the difference is
/// the cost of each byte of record (the slope), the short one bounds the fixed cost of a record. The work is counted in records, from the
/// bytes written, never measured in time.</para>
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
    /// Bytes a record may allocate whatever its length: the tasks of the call and their state
    /// </summary>
    private const long FixedCostLimit = 1024;

    /// <summary>
    /// Bytes the two records may differ by: the alignment of the arrays
    /// </summary>
    private const long SlopeTolerance = 64;

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
        // Arranging one record per run, written by the client before the measure
        const int Runs = 10;
        MemoryStream written = new MemoryStream();
        using (SecureRecordStream client = new SecureRecordStream(written, CreateSecrets(), isClient: true))
        {
            for (int record = 0; record < Runs + AllocationMeter.DefaultWarmUps; record++)
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
    /// A record read allocates the same few bytes whatever its length: it is opened into the buffer of the stream (space O(1) per record)
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
    /// A write is cut into the fewest records: one per 16 KiB, each with its length and its tag only (time O(n), and 18 bytes per 16 KiB)
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
        MemoryStream below = new MemoryStream();
        await using SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true);

        // Acting
        await client.WriteAsync(new byte[WriteLength], TestContext.Current.CancellationToken);

        // Check
        below.Length.ShouldBe(WriteLength + (Records * (SecureRecordStream.HeaderLength + SecureRecordStream.TagLength)));
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
