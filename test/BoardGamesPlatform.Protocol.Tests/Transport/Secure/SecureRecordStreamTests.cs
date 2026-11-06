/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureRecordStreamTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the records of the secure channel: vectors, round trips, and every change a party in the middle could make
 */

// Using
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport.Secure;

// Static using
using static BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors.SecureChannelVectors;

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="SecureRecordStream"/>
/// </summary>
public sealed class SecureRecordStreamTests
{
    #region Members

    #region Constants

    // #N/A

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
    /// Method used to derive the secrets of the vectors, new for each stream since a stream erases them
    /// </summary>
    /// <returns>
    /// The secrets
    /// </returns>
    private static SessionSecrets CreateSecrets()
    {
        // Function's return
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        return KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(peer));
    }

    /// <summary>
    /// Method used to write through the stream of the client, and get the bytes it sent
    /// </summary>
    /// <param name="hardLimit">Hard limit of the records</param>
    /// <param name="writes">[Cannot be null] Writes, in their order</param>
    /// <returns>
    /// The bytes written below the stream
    /// </returns>
    private static async Task<byte[]> WriteAsClientAsync(long hardLimit, params byte[][] writes)
    {
        // Writing each piece, then the close record
        using MemoryStream below = new MemoryStream();
        await using (SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true, hardLimit))
        {
            foreach (byte[] write in writes)
                await client.WriteAsync(write, TestContext.Current.CancellationToken);
            await client.WriteCloseAsync(TestContext.Current.CancellationToken);
        }

        // Function's return
        return below.ToArray();
    }

    /// <summary>
    /// Method used to read everything the server stream gives from bytes received
    /// </summary>
    /// <param name="received">[Cannot be null] Bytes received below the stream</param>
    /// <param name="hardLimit">Hard limit of the records</param>
    /// <returns>
    /// The plaintext, up to the close record
    /// </returns>
    private static async Task<byte[]> ReadAsServerAsync(byte[] received, long hardLimit = SecureRecordStream.DefaultHardLimit)
    {
        // Reading in small pieces until the end of the stream
        await using SecureRecordStream server = new SecureRecordStream(new MemoryStream(received), CreateSecrets(), isClient: false, hardLimit);
        using MemoryStream plaintext = new MemoryStream();
        byte[] buffer = new byte[1000];
        int read;
        while ((read = await server.ReadAsync(buffer, TestContext.Current.CancellationToken)) > 0)
            plaintext.Write(buffer, 0, read);

        // Function's return
        return plaintext.ToArray();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The first two records of the client are the ones the Python package "cryptography" sealed, and the server opens them
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task TheRecordsAreTheOnesComputedElsewhereAsync()
    {
        // Acting
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext), Bytes(Record1Plaintext));

        // Checks: the two records, then a close record; the plaintext never appears in what is written
        Convert.ToHexString(written.AsSpan(0, Bytes(Record0).Length)).ShouldBe(Record0);
        Convert.ToHexString(written.AsSpan(Bytes(Record0).Length, Bytes(Record1).Length)).ShouldBe(Record1);
        written.Length.ShouldBe(Bytes(Record0).Length + Bytes(Record1).Length + SecureRecordStream.HeaderLength + SecureRecordStream.TagLength);
        Convert.ToHexString(written).ShouldNotContain(Record0Plaintext);
        (await ReadAsServerAsync(written)).ShouldBe([.. Bytes(Record0Plaintext), .. Bytes(Record1Plaintext)]);
    }

    /// <summary>
    /// Any length of data, cut in writes of any size, reads back the same; a write of zero bytes sends nothing
    /// </summary>
    /// <param name="seed">Seed of the random data, so that a failure can be replayed</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task RandomWritesReadBackTheSameAsync(int seed)
    {
        // Arranging writes from 0 to 70 000 bytes
        Random random = new Random(seed);
        byte[][] writes = Enumerable.Range(0, 12).Select(_ => new byte[random.Next(0, 70_000)]).ToArray();
        foreach (byte[] write in writes)
            random.NextBytes(write);

        // Acting
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, writes);
        byte[] onlyEmpty = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, [], []);

        // Checks
        (await ReadAsServerAsync(written)).ShouldBe(writes.SelectMany(write => write).ToArray());
        onlyEmpty.Length.ShouldBe(SecureRecordStream.HeaderLength + SecureRecordStream.TagLength);
    }

    /// <summary>
    /// A bit changed anywhere in a record is refused: in the first record as a failed key confirmation, later as a failed record
    /// </summary>
    /// <param name="offset">Offset of the changed byte, from the start of the second record when it is not the first</param>
    /// <param name="inFirstRecord">Whether the change is in the first record</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(20, true)]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(10, false)]
    public async Task AChangedBitIsRefusedAsync(int offset, bool inFirstRecord)
    {
        // Arranging: a bit changed in the length, the ciphertext or the tag
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext), Bytes(Record1Plaintext));
        written[(inFirstRecord ? 0 : Bytes(Record0).Length) + offset] ^= 0x01;

        // Acting
        SecureChannelException failure = await Should.ThrowAsync<SecureChannelException>(ReadAsServerAsync(written));

        // Check
        failure.Reason.ShouldBe(inFirstRecord ? SecureChannelFailure.KeyConfirmationFailed : SecureChannelFailure.RecordAuthenticationFailed);
    }

    /// <summary>
    /// Records replayed, swapped or dropped fail their tag, since the sequence number is in the nonce
    /// </summary>
    /// <param name="change">What is done to the records</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData("replay")]
    [InlineData("swap")]
    [InlineData("drop")]
    public async Task RecordsOutOfTheirOrderAreRefusedAsync(string change)
    {
        // Arranging the two records and the close record, then changing their order
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext), Bytes(Record1Plaintext));
        byte[] first = Bytes(Record0);
        byte[] second = Bytes(Record1);
        byte[] close = written[(first.Length + second.Length)..];
        byte[] changed = change switch
        {
            "replay" => [.. first, .. first, .. second, .. close],
            "swap" => [.. second, .. first, .. close],
            _ => [.. first, .. close]
        };

        // Check
        await Should.ThrowAsync<SecureChannelException>(ReadAsServerAsync(changed));
    }

    /// <summary>
    /// A stream cut inside a record, or ended without the close record, is a truncation; a byte after the close record is refused
    /// </summary>
    /// <param name="change">What is done to the stream</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData("insideRecord")]
    [InlineData("withoutClose")]
    [InlineData("afterClose")]
    [InlineData("tooLong")]
    public async Task ACutOrAnExtraByteIsRefusedAsync(string change)
    {
        // Arranging
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext), Bytes(Record1Plaintext));
        int recordsEnd = Bytes(Record0).Length + Bytes(Record1).Length;
        byte[] changed = change switch
        {
            "insideRecord" => written[..(recordsEnd - 3)],
            "withoutClose" => written[..recordsEnd],
            "afterClose" => [.. written, 0x00],
            _ => [.. Bytes(Record0), 0x40, 0x01]
        };

        // Acting
        SecureChannelException failure = await Should.ThrowAsync<SecureChannelException>(ReadAsServerAsync(changed));

        // Check
        failure.Reason.ShouldBe(SecureChannelFailure.RecordAuthenticationFailed);
    }

    /// <summary>
    /// The hard limit refuses a data record beyond it, on the sender and on the receiver, and keeps its last sequence number for the close
    /// record, so that a connection at its limit still ends cleanly; the renewal is due before it
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task TheHardLimitKeepsTheLastRecordForTheCloseAsync()
    {
        // Arranging a limit of 4 records (3 data records and the close), the renewal due at 2
        using MemoryStream below = new MemoryStream();
        CancellationToken token = TestContext.Current.CancellationToken;
        bool dueAfterOne;
        bool dueAfterTwo;
        SecureChannelException failure;
        await using (SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true, hardLimit: 4, renewalThreshold: 2))
        {
            // Acting: three data records, a fourth refused, then the close record
            await client.WriteAsync(new byte[] { 1 }, token);
            dueAfterOne = client.IsRenewalDue;
            await client.WriteAsync(new byte[] { 2 }, token);
            dueAfterTwo = client.IsRenewalDue;
            await client.WriteAsync(new byte[] { 3 }, token);
            failure = await Should.ThrowAsync<SecureChannelException>(client.WriteAsync(new byte[] { 4 }, token).AsTask());

            // The same stream still sends its close record, on the sequence number kept for it
            client.IsSound.ShouldBeTrue();
            await client.WriteCloseAsync(token);
        }
        (await ReadAsServerAsync(below.ToArray(), hardLimit: 4)).ShouldBe(new byte[] { 1, 2, 3 });
        byte[] threeAndClose;
        using (MemoryStream complete = new MemoryStream())
        {
            await using SecureRecordStream client = new SecureRecordStream(complete, CreateSecrets(), isClient: true, hardLimit: 4);
            await client.WriteAsync(new byte[] { 1 }, token);
            await client.WriteAsync(new byte[] { 2 }, token);
            await client.WriteAsync(new byte[] { 3 }, token);
            await client.WriteCloseAsync(token);
            threeAndClose = complete.ToArray();
        }

        // Checks: the sender refused the fourth data record; a receiver with the same limit reads the three and the clean end, one with a
        // smaller limit refuses the third data record
        dueAfterOne.ShouldBeFalse();
        dueAfterTwo.ShouldBeTrue();
        failure.Reason.ShouldBe(SecureChannelFailure.RecordLimitReached);
        (await ReadAsServerAsync(threeAndClose, hardLimit: 4)).ShouldBe(new byte[] { 1, 2, 3 });
        (await Should.ThrowAsync<SecureChannelException>(ReadAsServerAsync(threeAndClose, hardLimit: 3))).Reason.ShouldBe(SecureChannelFailure.RecordLimitReached);
    }

    /// <summary>
    /// A failed read breaks the reads for good (the next ones fail as a lost connection) but never the writes, so that the close record can
    /// still be sent; a disposal is a closed connection, never an ObjectDisposedException
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AFailureBreaksTheStreamForGoodAsync()
    {
        // Arranging a stream whose first record is broken
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext));
        written[5] ^= 0x01;
        await using SecureRecordStream server = new SecureRecordStream(new MemoryStream(written), CreateSecrets(), isClient: false);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting
        await Should.ThrowAsync<SecureChannelException>(server.ReadAsync(new byte[10], token).AsTask());
        IOException read = await Should.ThrowAsync<IOException>(server.ReadAsync(new byte[10], token).AsTask());
        bool isSoundAfterRead = server.IsSound;
        await server.DisposeAsync();
        IOException afterDispose = await Should.ThrowAsync<IOException>(server.WriteAsync(new byte[1], token).AsTask());

        // Checks
        isSoundAfterRead.ShouldBeTrue();
        server.IsSound.ShouldBeFalse();
        read.InnerException.ShouldBeOfType<SecureChannelException>();
        afterDispose.Message.ShouldContain("closed");
    }

    /// <summary>
    /// Writes of the exact sizes at the borders of a record read back the same
    /// </summary>
    /// <param name="size">Size of the write</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData(1)]
    [InlineData(16_383)]
    [InlineData(16_384)]
    [InlineData(16_385)]
    [InlineData(65_536)]
    [InlineData(65_548)]
    public async Task WritesAtTheBordersOfARecordReadBackTheSameAsync(int size)
    {
        // Arranging
        byte[] data = new byte[size];
        new Random(size).NextBytes(data);

        // Acting
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, data);

        // Check
        (await ReadAsServerAsync(written)).ShouldBe(data);
    }

    /// <summary>
    /// Nothing can be written after the close record, and closing twice sends one close record
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task NothingIsWrittenAfterTheCloseAsync()
    {
        // Arranging
        using MemoryStream below = new MemoryStream();
        await using SecureRecordStream client = new SecureRecordStream(below, CreateSecrets(), isClient: true);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting
        await client.WriteCloseAsync(token);
        await client.WriteCloseAsync(token);

        // Checks
        await Should.ThrowAsync<InvalidOperationException>(client.WriteAsync(new byte[1], token).AsTask());
        below.Length.ShouldBe(SecureRecordStream.HeaderLength + SecureRecordStream.TagLength);
    }

    /// <summary>
    /// The records of one direction never open with the keys of the other: a record sent back to its author is refused
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ARecordSentBackToItsAuthorIsRefusedAsync()
    {
        // Arranging: what the client wrote, read by a client
        byte[] written = await WriteAsClientAsync(SecureRecordStream.DefaultHardLimit, Bytes(Record0Plaintext));
        await using SecureRecordStream client = new SecureRecordStream(new MemoryStream(written), CreateSecrets(), isClient: true);

        // Check
        await Should.ThrowAsync<SecureChannelException>(client.ReadAsync(new byte[10], TestContext.Current.CancellationToken).AsTask());
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
