/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureTransportTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the secure channel over real mutual TLS sessions: the connection, the parties in the middle, the revocation, the age
 */

// Using
using System.Net;
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="SecureTransportListener"/> and <see cref="SecureTransportConnector"/> over mutual TLS
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of 0.6.0: a party that holds valid TLS certificates but not the signing keys fails the
/// handshake; a party that only relays sees records it cannot read.</para>
/// </remarks>
public sealed class SecureTransportTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Password of the key files of the tests
    /// </summary>
    private const string KeyPassword = "password-of-the-signing-key";

    #endregion

    #region Variables

    /// <summary>
    /// Logger of the Very Back of the tests
    /// </summary>
    private readonly CapturingLogger<SecureTransportTests> _serverLogger = new CapturingLogger<SecureTransportTests>();

    /// <summary>
    /// Logger of the Back of the tests
    /// </summary>
    private readonly CapturingLogger<SecureTransportTests> _clientLogger = new CapturingLogger<SecureTransportTests>();

    /// <summary>
    /// Authority of the certificates of the tests, which also issues the certificates of the parties in the middle
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// TLS credentials of the Back
    /// </summary>
    private readonly TlsCredentials _back;

    /// <summary>
    /// TLS credentials of the Very Back
    /// </summary>
    private readonly TlsCredentials _veryBack;

    /// <summary>
    /// Signing key of the Back
    /// </summary>
    private readonly ECDsa _backKey = SecureChannelTestKeys.CreateSigningKey();

    /// <summary>
    /// Signing key of the Very Back
    /// </summary>
    private readonly ECDsa _veryBackKey = SecureChannelTestKeys.CreateSigningKey();

    /// <summary>
    /// Temporary folder of the key files
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("bgp-secure-transport-").FullName;

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

    /// <summary>
    /// Constructor
    /// </summary>
    public SecureTransportTests()
    {
        // Setting
        _back = Credentials(PlatformRole.Back);
        _veryBack = Credentials(PlatformRole.VeryBack);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to issue new TLS credentials of a role, by the authority of the tests
    /// </summary>
    /// <param name="role">Role</param>
    /// <returns>
    /// The credentials
    /// </returns>
    private TlsCredentials Credentials(PlatformRole role)
    {
        // Function's return
        return TlsCredentials.FromCertificates(role, TestCertificates.Suffix, _certificates.Issue(role), _certificates.TrustedRoots, [], TimeProvider.System);
    }

    /// <summary>
    /// Method used to build keys in memory
    /// </summary>
    /// <param name="own">[Cannot be null] Signing key; copied, so that the test keeps it</param>
    /// <param name="peer">[Cannot be null] Signing key of the peer, pinned</param>
    /// <param name="credentials">[Cannot be null] TLS credentials of the tier</param>
    /// <returns>
    /// The keys
    /// </returns>
    private static SigningKeys Keys(ECDsa own, ECDsa peer, TlsCredentials credentials)
    {
        // Copying the signing key, since the set takes ownership of it
        ECDsa copy = ECDsa.Create(own.ExportParameters(includePrivateParameters: true));

        // Function's return
        return SigningKeys.FromKeys(SecureChannelTestKeys.CreateSet(copy, peer), () => credentials.Certificate);
    }

    /// <summary>
    /// Method used to start a listener of the Very Back
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys of the Very Back</param>
    /// <param name="credentials">TLS credentials of the Very Back; its own when null</param>
    /// <param name="clock">Clock of the ages; the system when null</param>
    /// <param name="maxAge">Age limit of the connections; the delivered one when null</param>
    /// <returns>
    /// The listener
    /// </returns>
    private SecureTransportListener StartVeryBack(SigningKeys keys, TlsCredentials? credentials = null, TimeProvider? clock = null, TimeSpan? maxAge = null)
    {
        // TCP, mutual TLS accepting the Back only, then the secure channel
        TlsTransportListener tls = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), credentials ?? _veryBack,
            PlatformRole.Back, _serverLogger);

        // Function's return
        return new SecureTransportListener(tls, keys, _serverLogger, clock ?? TimeProvider.System, HandshakeEntropy.System, SecureRecordStream.DefaultHardLimit,
            maxAge ?? SecureTransportListener.MaxAge);
    }

    /// <summary>
    /// Method used to build the connector of a Back
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys of the Back</param>
    /// <param name="credentials">TLS credentials of the Back; its own when null</param>
    /// <returns>
    /// The connector
    /// </returns>
    private SecureTransportConnector BackConnector(SigningKeys keys, TlsCredentials? credentials = null)
    {
        // Function's return
        return new SecureTransportConnector(new TlsTransportConnector(new TcpTransportConnector(), credentials ?? _back, PlatformRole.VeryBack, _clientLogger), keys,
            _clientLogger);
    }

    /// <summary>
    /// Method used to accept and establish one connection on a listener
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener</param>
    /// <returns>
    /// The established connection
    /// </returns>
    private static async Task<ITransportConnection> AcceptAsync(ITransportListener listener)
    {
        // Accepting, then running the handshakes; the accepted connection is released on a failure, as the link server does
        ITransportConnection accepted = await listener.AcceptAsync(TestContext.Current.CancellationToken);
        try
        {
            // Function's return
            return await listener.EstablishAsync(accepted, TestContext.Current.CancellationToken);
        }
        catch
        {
            await accepted.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Method used to run a party in the middle: it ends the TLS session of the Back with a certificate of the Very Back, opens its own TLS
    /// session to the Very Back with a certificate of the Back, and relays the bytes inside TLS both ways, recording what the Back sends
    /// </summary>
    /// <param name="asVeryBack">[Cannot be null] TLS credentials it shows to the Back</param>
    /// <param name="asBack">[Cannot be null] TLS credentials it shows to the Very Back</param>
    /// <param name="veryBackPort">Port of the real Very Back</param>
    /// <param name="recorded">[Cannot be null] Bytes the Back sends inside TLS, as the party in the middle reads them</param>
    /// <returns>
    /// The listener of the party in the middle, and the task of the relay
    /// </returns>
    private static (TlsTransportListener Listener, Task Relay) StartMiddle(TlsCredentials asVeryBack, TlsCredentials asBack, int veryBackPort, MemoryStream recorded)
    {
        // Listening as the Very Back, then relaying the first connection
        TlsTransportListener listener = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), asVeryBack,
            PlatformRole.Back, NullLogger.Instance);
        Task relay = Task.Run(async () =>
        {
            CancellationToken token = TestContext.Current.CancellationToken;
            await using ITransportConnection fromBack = await AcceptAsync(listener);
            await using ITransportConnection toVeryBack = await new TlsTransportConnector(new TcpTransportConnector(), asBack, PlatformRole.VeryBack, NullLogger.Instance)
                .ConnectAsync("127.0.0.1", veryBackPort, token);
            Task up = PumpAsync(fromBack.Stream, toVeryBack.Stream, recorded);
            Task down = PumpAsync(toVeryBack.Stream, fromBack.Stream, null);
            await Task.WhenAny(up, down);
        }, TestContext.Current.CancellationToken);

        // Function's return
        return (listener, relay);
    }

    /// <summary>
    /// Method used to copy the bytes of one stream to another until the end or a failure
    /// </summary>
    /// <param name="from">[Cannot be null] Stream read</param>
    /// <param name="to">[Cannot be null] Stream written</param>
    /// <param name="recorded">Copy of the bytes, or null</param>
    /// <returns>
    /// A task that completes at the end of the copy
    /// </returns>
    private static async Task PumpAsync(Stream from, Stream to, MemoryStream? recorded)
    {
        // Copying, a failure of either side ending the copy
        byte[] buffer = new byte[4096];
        try
        {
            int read;
            while ((read = await from.ReadAsync(buffer, TestContext.Current.CancellationToken)) > 0)
            {
                lock (buffer)
                    recorded?.Write(buffer, 0, read);
                await to.WriteAsync(buffer.AsMemory(0, read), TestContext.Current.CancellationToken);
                await to.FlushAsync(TestContext.Current.CancellationToken);
            }
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException)
        {
            // One side closed: the relay ends
        }
    }

    /// <summary>
    /// Method used to write the key files of the Very Back
    /// </summary>
    /// <param name="peerKeys">[Cannot be null] Public keys of the Back to pin</param>
    private void WriteVeryBackFiles(params ECDsa[] peerKeys)
    {
        // Writing the signing key, then the pinned keys
        File.WriteAllText(Path.Combine(_folder, "veryback-signing.key"), _veryBackKey.ExportEncryptedPkcs8PrivateKeyPem(KeyPassword,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
        File.WriteAllLines(Path.Combine(_folder, "back.pub"), peerKeys.Select(key => SigningPublicKey.FromKey(key).ToBase64()));
    }

    #endregion

    #region Publics

    /// <summary>
    /// The Back and the Very Back of the shared test files open a secure channel, and bytes go both ways, a close reading as the end
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task TheBackAndTheVeryBackTalkThroughTheChannelAsync()
    {
        // Arranging the tiers as delivered, from the files of the tests
        TlsCredentials credentials = TestTlsFiles.Shared.LoadCredentials(PlatformRole.VeryBack);
        TlsTransportListener tls = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), credentials,
            PlatformRole.Back, _serverLogger);
        await using SecureTransportListener listener = new SecureTransportListener(tls, TestTlsFiles.Shared.LoadSigningKeys(PlatformRole.VeryBack, credentials), _serverLogger);
        ITransportConnector connector = TestTlsFiles.Shared.CreateConnector(PlatformRole.Back, PlatformRole.VeryBack);
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting
        Task<ITransportConnection> accepting = AcceptAsync(listener);
        await using ITransportConnection client = await connector.ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;
        byte[] large = new byte[40_000];
        new Random(42).NextBytes(large);
        await client.Stream.WriteAsync(large, token);
        await client.Stream.FlushAsync(token);
        byte[] received = new byte[large.Length];
        await server.Stream.ReadExactlyAsync(received, token);
        await server.Stream.WriteAsync("pong"u8.ToArray(), token);
        await server.Stream.FlushAsync(token);
        byte[] answer = new byte[4];
        await client.Stream.ReadExactlyAsync(answer, token);
        await client.CloseSendAsync(token);
        int end = await server.Stream.ReadAsync(new byte[1], token);

        // Checks
        received.ShouldBe(large);
        answer.ShouldBe("pong"u8.ToArray());
        end.ShouldBe(0);
        server.PeerIdentity.ShouldBe(PlatformIdentity.GetDnsName(PlatformRole.Back, TestCertificates.Suffix));
        _serverLogger.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message.Contains("Secure channel opened", StringComparison.Ordinal));
    }

    /// <summary>
    /// Done criterion: a Very Back with a valid certificate but a signing key the Back does not pin is refused by the Back, and the reverse
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task AValidCertificateWithoutTheSigningKeyIsRefusedAsync()
    {
        // Arranging: an impostor of each role, with a valid certificate of its role and a signing key of its own
        using ECDsa impostorKey = SecureChannelTestKeys.CreateSigningKey();
        await using SecureTransportListener impostorVeryBack = StartVeryBack(Keys(impostorKey, _backKey, _veryBack));
        await using SecureTransportListener veryBack = StartVeryBack(Keys(_veryBackKey, _backKey, _veryBack));
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));
        SecureTransportConnector impostorBack = BackConnector(Keys(impostorKey, _veryBackKey, _back));
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting
        Task<ITransportConnection> acceptingImpostorBack = AcceptAsync(veryBack);
        Task<ITransportConnection> acceptingBack = AcceptAsync(impostorVeryBack);
        SecureChannelException refusedServer = await Should.ThrowAsync<SecureChannelException>(back.ConnectAsync("127.0.0.1", impostorVeryBack.LocalEndPoint.Port, token));
        (await Should.ThrowAsync<SecureChannelException>(impostorBack.ConnectAsync("127.0.0.1", veryBack.LocalEndPoint.Port, token))).Reason.ShouldBe(SecureChannelFailure.PeerClosed);
        SecureChannelException refusedClient = await Should.ThrowAsync<SecureChannelException>(acceptingImpostorBack);
        await Should.ThrowAsync<Exception>(acceptingBack);

        // Checks
        refusedServer.Reason.ShouldBe(SecureChannelFailure.UnknownKey);
        refusedClient.Reason.ShouldBe(SecureChannelFailure.UnknownKey);
        _clientLogger.Entries.ShouldContain(entry => entry.Message.Contains("UnknownKey", StringComparison.Ordinal));
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains("UnknownKey", StringComparison.Ordinal));
    }

    /// <summary>
    /// Done criterion: a party in the middle that ends TLS with valid certificates of its own and relays the channel makes the signatures fail
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task APartyInTheMiddleWithItsOwnCertificatesIsRefusedAsync()
    {
        // Arranging: the party in the middle holds valid certificates of both roles, but not the ones of the real tiers
        await using SecureTransportListener veryBack = StartVeryBack(Keys(_veryBackKey, _backKey, _veryBack));
        using MemoryStream recorded = new MemoryStream();
        (TlsTransportListener middle, Task relay) = StartMiddle(Credentials(PlatformRole.VeryBack), Credentials(PlatformRole.Back), veryBack.LocalEndPoint.Port, recorded);
        await using TlsTransportListener middleListener = middle;
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));
        Task<ITransportConnection> accepting = AcceptAsync(veryBack);

        // Acting
        SecureChannelException refused = await Should.ThrowAsync<SecureChannelException>(back.ConnectAsync("127.0.0.1", middle.LocalEndPoint.Port,
            TestContext.Current.CancellationToken));
        await Should.ThrowAsync<Exception>(accepting);
        await relay;

        // Check: the Very Back signed another transcript than the one the Back computed
        refused.Reason.ShouldBe(SecureChannelFailure.BadSignature);
    }

    /// <summary>
    /// A party that holds the very certificates of the tiers (stolen PFX) but not the signing keys can only relay: it reads records, never the
    /// requests, and the link works
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task APartyWithTheStolenCertificatesOnlyRelaysRecordsAsync()
    {
        // Arranging: the party in the middle shows the real certificates of both tiers
        await using SecureTransportListener veryBack = StartVeryBack(Keys(_veryBackKey, _backKey, _veryBack));
        using MemoryStream recorded = new MemoryStream();
        (TlsTransportListener middle, Task relay) = StartMiddle(_veryBack, _back, veryBack.LocalEndPoint.Port, recorded);
        await using TlsTransportListener middleListener = middle;
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));
        Task<ITransportConnection> accepting = AcceptAsync(veryBack);
        CancellationToken token = TestContext.Current.CancellationToken;
        byte[] secret = "{\"operation\":\"ping\",\"body\":{}} a request of the Back"u8.ToArray();

        // Acting
        await using (ITransportConnection client = await back.ConnectAsync("127.0.0.1", middle.LocalEndPoint.Port, token))
        {
            await using ITransportConnection server = await accepting;
            await client.Stream.WriteAsync(secret, token);
            await client.Stream.FlushAsync(token);
            byte[] received = new byte[secret.Length];
            await server.Stream.ReadExactlyAsync(received, token);

            // Checks: the request arrives, and the party in the middle never saw it
            received.ShouldBe(secret);
            recorded.Length.ShouldBeGreaterThan(SecureHandshake.ClientHelloLength + SecureHandshake.ClientSignatureLength);
            Convert.ToHexString(recorded.ToArray()).ShouldContain(Convert.ToHexString("BGPS"u8));
            Convert.ToHexString(recorded.ToArray()).ShouldNotContain(Convert.ToHexString(secret.AsSpan(0, 16)));
        }
        await relay;
    }

    /// <summary>
    /// A server without the secure channel (a TLS server only) never answers the handshake as the channel: the Back refuses it
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task AServerWithoutTheChannelIsRefusedAsync()
    {
        // Arranging: a TLS server that reads a frame header, refuses it as the protocol does, and closes
        await using TlsTransportListener plain = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), _veryBack,
            PlatformRole.Back, NullLogger.Instance);
        Task serving = Task.Run(async () =>
        {
            await using ITransportConnection connection = await AcceptAsync(plain);
            await connection.Stream.ReadExactlyAsync(new byte[12], TestContext.Current.CancellationToken);
        }, TestContext.Current.CancellationToken);
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));

        // Acting
        SecureChannelException refused = await Should.ThrowAsync<SecureChannelException>(back.ConnectAsync("127.0.0.1", plain.LocalEndPoint.Port,
            TestContext.Current.CancellationToken));
        await serving;

        // Check
        refused.Reason.ShouldBe(SecureChannelFailure.PeerClosed);
    }

    /// <summary>
    /// Removing the key of the Back from the file of the Very Back closes the established connection, with a close record first
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task ARemovedKeyClosesTheEstablishedConnectionAsync()
    {
        // Arranging: the Very Back loads its keys from files, the Back's key pinned with another one
        using ECDsa otherBackKey = SecureChannelTestKeys.CreateSigningKey();
        WriteVeryBackFiles(_backKey, otherBackKey);
        File.SetLastWriteTimeUtc(Path.Combine(_folder, "back.pub"), DateTime.UtcNow.AddMinutes(-5));
        using SigningKeys keys = SigningKeys.FromFiles(Path.Combine(_folder, "veryback-signing.key"), KeyPassword, Path.Combine(_folder, "back.pub"), "another password",
            () => _veryBack.Certificate, TimeProvider.System, _serverLogger);
        await using SecureTransportListener veryBack = StartVeryBack(keys);
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<ITransportConnection> accepting = AcceptAsync(veryBack);
        await using ITransportConnection client = await back.ConnectAsync("127.0.0.1", veryBack.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Acting: the key of the Back removed, taken at the next check
        File.WriteAllLines(Path.Combine(_folder, "back.pub"), [SigningPublicKey.FromKey(otherBackKey).ToBase64()]);
        keys.ReloadIfChanged();
        int read = await client.Stream.ReadAsync(new byte[1], token);

        // Checks: the Back reads a clean end (the close record), and the Very Back logged why
        read.ShouldBe(0);
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains("was removed", StringComparison.Ordinal));
    }

    /// <summary>
    /// The Very Back closes a connection older than its age limit, in case the client never renews it
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = 20000)]
    public async Task AnOldConnectionIsClosedByTheServerAsync()
    {
        // Arranging: an age limit of one check, on a fake clock
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        await using SecureTransportListener veryBack = StartVeryBack(Keys(_veryBackKey, _backKey, _veryBack), clock: clock, maxAge: SigningKeys.CheckInterval);
        SecureTransportConnector back = BackConnector(Keys(_backKey, _veryBackKey, _back));
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<ITransportConnection> accepting = AcceptAsync(veryBack);
        await using ITransportConnection client = await back.ConnectAsync("127.0.0.1", veryBack.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Acting
        clock.Advance(SigningKeys.CheckInterval);
        int read = await client.Stream.ReadAsync(new byte[1], token);

        // Checks
        read.ShouldBe(0);
        _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains("never renewed", StringComparison.Ordinal));
    }

    /// <summary>
    /// Method used to release the certificates, the keys and the files
    /// </summary>
    public void Dispose()
    {
        // Releasing everything the tests made
        _back.Dispose();
        _veryBack.Dispose();
        _certificates.Dispose();
        _backKey.Dispose();
        _veryBackKey.Dispose();
        Directory.Delete(_folder, recursive: true);
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
