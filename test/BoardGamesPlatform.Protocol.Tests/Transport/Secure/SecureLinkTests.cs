/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureLinkTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the secure channel under the link server and the link client: closes, timeouts, tampering, renewal, revocation races, logs
 */

// Using
using System.Net;
using System.Security.Cryptography;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

// Static using
using static BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors.SecureChannelVectors;

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check the secure channel as the tiers use it, through the link server and the link client
/// </summary>
public sealed class SecureLinkTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time a test may run
    /// </summary>
    private const int TestTimeout = 20000;

    #endregion

    #region Variables

    /// <summary>
    /// Logger of the Very Back of the tests
    /// </summary>
    private readonly CapturingLogger<SecureLinkTests> _serverLogger = new CapturingLogger<SecureLinkTests>();

    /// <summary>
    /// Logger of the Back of the tests
    /// </summary>
    private readonly CapturingLogger<SecureLinkTests> _clientLogger = new CapturingLogger<SecureLinkTests>();

    /// <summary>
    /// Authority of the certificates of the tests
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
    public SecureLinkTests()
    {
        // Setting
        _back = TlsCredentials.FromCertificates(PlatformRole.Back, TestCertificates.Suffix, _certificates.Issue(PlatformRole.Back), _certificates.TrustedRoots, [],
            TimeProvider.System);
        _veryBack = TlsCredentials.FromCertificates(PlatformRole.VeryBack, TestCertificates.Suffix, _certificates.Issue(PlatformRole.VeryBack), _certificates.TrustedRoots,
            [], TimeProvider.System);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build keys in memory, the signing key copied so that the test keeps it
    /// </summary>
    /// <param name="own">[Cannot be null] Signing key</param>
    /// <param name="peer">[Cannot be null] Signing key of the peer, pinned</param>
    /// <param name="credentials">[Cannot be null] TLS credentials of the tier</param>
    /// <returns>
    /// The keys
    /// </returns>
    private static SigningKeys Keys(ECDsa own, ECDsa peer, TlsCredentials credentials)
    {
        // Function's return
        return SigningKeys.FromKeys(SecureChannelTestKeys.CreateSet(ECDsa.Create(own.ExportParameters(includePrivateParameters: true)), peer), () => credentials.Certificate);
    }

    /// <summary>
    /// Method used to start a Very Back over mutual TLS and the secure channel, answering the Ping operation
    /// </summary>
    /// <param name="handshakeTimeout">Time allowed to a client for both handshakes and the hello</param>
    /// <param name="stopping">[Cannot be null] Token that stops the server</param>
    /// <param name="running">The task of the server</param>
    /// <param name="clock">Clock of the listener; the system when null</param>
    /// <returns>
    /// The port of the server
    /// </returns>
    private int StartVeryBack(TimeSpan handshakeTimeout, CancellationToken stopping, out Task running, TimeProvider? clock = null)
    {
        // TCP, mutual TLS accepting the Back only, the secure channel, then the link server
        TlsTransportListener tls = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), _veryBack,
            PlatformRole.Back, _serverLogger);
        SecureTransportListener listener = new SecureTransportListener(tls, Keys(_veryBackKey, _backKey, _veryBack), _serverLogger, clock ?? TimeProvider.System,
            HandshakeEntropy.System, SecureRecordStream.DefaultHardLimit, SecureTransportListener.MaxAge);
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register<PingRequest, PingResponse>(BackOperations.Ping,
            (_, _) => Task.FromResult(new PingResponse(DateTimeOffset.UnixEpoch)));
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), "the Very Back", _serverLogger, TimeSpan.Zero,
            handshakeTimeout, LinkServer.DefaultMaxConnections);
        running = server.RunAsync(stopping);

        // Function's return
        return listener.LocalEndPoint.Port;
    }

    /// <summary>
    /// Method used to build the connector of the Back
    /// </summary>
    /// <param name="renewalThreshold">Records of a direction from which a renewal is due</param>
    /// <returns>
    /// The connector
    /// </returns>
    private SecureTransportConnector BackConnector(long renewalThreshold = SecureRecordStream.DefaultRenewalThreshold)
    {
        // Function's return
        return new SecureTransportConnector(new TlsTransportConnector(new TcpTransportConnector(), _back, PlatformRole.VeryBack, _clientLogger),
            Keys(_backKey, _veryBackKey, _back), _clientLogger, TimeProvider.System, HandshakeEntropy.System, SecureRecordStream.DefaultHardLimit, renewalThreshold);
    }

    /// <summary>
    /// Method used to open a TLS session to the Very Back without the secure channel, as a client that holds a certificate only
    /// </summary>
    /// <param name="port">Port of the Very Back</param>
    /// <returns>
    /// The TLS connection
    /// </returns>
    private Task<ITransportConnection> ConnectWithTlsOnlyAsync(int port)
    {
        // Function's return
        return new TlsTransportConnector(new TcpTransportConnector(), _back, PlatformRole.VeryBack, NullLogger.Instance).ConnectAsync("127.0.0.1", port,
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Method used to wait until a condition holds
    /// </summary>
    /// <param name="condition">[Cannot be null] Condition</param>
    /// <returns>
    /// A task that completes once the condition holds
    /// </returns>
    /// <exception cref="TimeoutException">If it does not hold within 10 seconds</exception>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        // Checking every few milliseconds
        DateTime end = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > end)
                throw new TimeoutException("The condition did not hold within 10 seconds");
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// A normal disposal ends the channel cleanly: the peer reads the end of the stream, and never logs a refused record
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ANormalCloseIsACleanEndForThePeerAsync()
    {
        // Arranging a connection between a listener and a connector
        TlsTransportListener tls = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())), _veryBack,
            PlatformRole.Back, _serverLogger);
        await using SecureTransportListener listener = new SecureTransportListener(tls, Keys(_veryBackKey, _backKey, _veryBack), _serverLogger);
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<ITransportConnection> accepting = Task.Run(async () =>
        {
            ITransportConnection accepted = await listener.AcceptAsync(token);

            // Function's return
            return await listener.EstablishAsync(accepted, token);
        }, token);
        ITransportConnection client = await BackConnector().ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
        await using ITransportConnection server = await accepting;

        // Acting: the client only disposes its connection, as a request client does
        await client.DisposeAsync();
        int read = await server.Stream.ReadAsync(new byte[1], token);

        // Checks
        read.ShouldBe(0);
        _serverLogger.Entries.ShouldNotContain(entry => entry.Message.Contains("was refused", StringComparison.Ordinal));
    }

    /// <summary>
    /// A client that holds a certificate but stays silent after TLS, or never sends its third message, is closed within the handshake timeout
    /// of the link server, and frees its place
    /// </summary>
    /// <param name="sendFirstMessage">Whether the client sends a valid M1 first, then never M3</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASilentClientIsClosedWithinTheHandshakeTimeoutAsync(bool sendFirstMessage)
    {
        // Arranging a Very Back with a short handshake timeout
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartVeryBack(TimeSpan.FromMilliseconds(800), stopping.Token, out Task running);
        SigningKeySet backKeys = SecureChannelTestKeys.CreateSet(ECDsa.Create(_backKey.ExportParameters(includePrivateParameters: true)), _veryBackKey);

        // Acting: TLS, then nothing, or a valid M1 and nothing after its answer
        await using (ITransportConnection silent = await ConnectWithTlsOnlyAsync(port))
        {
            if (sendFirstMessage)
            {
                byte[] hello = [.. "BGPS"u8.ToArray(), SecureHandshake.Version, SecureHandshake.Suite, .. Bytes(ClientNonce), .. Bytes(ClientEphemeralPoint),
                    .. backKeys.OwnKey.KeyId.ToArray()];
                await silent.Stream.WriteAsync(hello, token);
                await silent.Stream.FlushAsync(token);
                await silent.Stream.ReadExactlyAsync(new byte[SecureHandshake.ServerHelloLength], token);
            }
            DateTime start = DateTime.UtcNow;
            int read;
            try
            {
                read = await silent.Stream.ReadAsync(new byte[1], token);
            }
            catch (IOException)
            {
                read = 0;
            }

            // Checks: closed by the server, soon
            read.ShouldBe(0);
            (DateTime.UtcNow - start).ShouldBeLessThan(TimeSpan.FromSeconds(5));
        }

        // A real Back still connects: the place of the handshake was freed
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the Very Back", BackConnector(), NullLogger.Instance))
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(DateTimeOffset.UnixEpoch);

        // Stopping the Very Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A client of TLS only, which sends the hello of the protocol instead of the first message of the channel, is refused at the magic
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AClientWithoutTheChannelIsRefusedAtTheMagicAsync()
    {
        // Arranging
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartVeryBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running);

        // Acting: a frame header and a JSON hello, padded as a first message would be
        await using (ITransportConnection plain = await ConnectWithTlsOnlyAsync(port))
        {
            byte[] hello = new byte[SecureHandshake.ClientHelloLength];
            "\0\0\0 \0\0\0\0\0\0\0\0{\"control\":\"hello\"}"u8.ToArray().CopyTo(hello, 0);
            await plain.Stream.WriteAsync(hello, token);
            await plain.Stream.FlushAsync(token);
            await WaitUntilAsync(() => _serverLogger.Entries.Any(entry => entry.Message.Contains("Malformed", StringComparison.Ordinal)));
        }

        // Stopping the Very Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A link client renews its secure connection once it reaches its renewal threshold, every request answered across the renewals
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheLinkClientRenewsItsSecureConnectionAsync()
    {
        // Arranging a renewal due after a few records
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartVeryBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running);
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the Very Back", BackConnector(renewalThreshold: 6), _clientLogger))
        {
            // Acting: pings until a second secure connection is opened
            for (int index = 0; index < 40 && _clientLogger.Entries.Count(entry => entry.Message.Contains("Secure channel opened", StringComparison.Ordinal)) < 2; index++)
            {
                (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(DateTimeOffset.UnixEpoch);
                await Task.Delay(20, token);
            }

            // Checks: renewed, and the next request still answered
            await WaitUntilAsync(() => _clientLogger.Entries.Count(entry => entry.Message.Contains("Secure channel opened", StringComparison.Ordinal)) >= 2);
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(DateTimeOffset.UnixEpoch);
        }

        // Stopping the Very Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A record changed in the middle of a session (a party with the stolen certificates that relays TLS) is refused by the Very Back, logged,
    /// and the caller of the Back gets link.unavailable, never the data
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARecordChangedDuringASessionGivesLinkUnavailableAsync()
    {
        // Arranging the Very Back, and a party in the middle that ends both TLS sessions with the real certificates and relays the bytes
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int veryBackPort = StartVeryBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running);
        await using TlsTransportListener middle = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())),
            _veryBack, PlatformRole.Back, NullLogger.Instance);
        bool isFlipping = false;
        Task relay = Task.Run(async () =>
        {
            ITransportConnection accepted = await middle.AcceptAsync(token);
            await using ITransportConnection fromBack = await middle.EstablishAsync(accepted, token);
            await using ITransportConnection toVeryBack = await new TlsTransportConnector(new TcpTransportConnector(), _back, PlatformRole.VeryBack, NullLogger.Instance)
                .ConnectAsync("127.0.0.1", veryBackPort, token);
            Task up = Task.Run(async () =>
            {
                byte[] buffer = new byte[4096];
                int read;
                while ((read = await fromBack.Stream.ReadAsync(buffer, token)) > 0)
                {
                    if (Volatile.Read(ref isFlipping))
                    {
                        buffer[read - 1] ^= 0x01;
                        Volatile.Write(ref isFlipping, false);
                    }
                    await toVeryBack.Stream.WriteAsync(buffer.AsMemory(0, read), token);
                    await toVeryBack.Stream.FlushAsync(token);
                }
            }, token);
            Task down = toVeryBack.Stream.CopyToAsync(fromBack.Stream, token);
            try
            {
                await Task.WhenAny(up, down);
            }
            catch (Exception exception) when (exception is IOException or OperationCanceledException)
            {
                // One side closed
            }
        }, token);

        // Acting: a first ping goes through, then the next request has one bit changed in its record
        await using (LinkClient client = new LinkClient("127.0.0.1", middle.LocalEndPoint.Port, TimeSpan.FromSeconds(5), "the Very Back", BackConnector(), NullLogger.Instance))
        {
            (await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(DateTimeOffset.UnixEpoch);
            Volatile.Write(ref isFlipping, true);
            LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping,
                new PingRequest(), token));

            // Checks
            failure.Code.ShouldBe(ErrorCodes.LinkUnavailable);
            await WaitUntilAsync(() => _serverLogger.Entries.Any(entry => entry.Level == LogLevel.Error && entry.Message.Contains("RecordAuthenticationFailed", StringComparison.Ordinal)));
        }
        try
        {
            await relay;
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException)
        {
            // The relay ends with the connections
        }

        // Stopping the Very Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A connection asks for a renewal once it is 24 hours old
    /// </summary>
    [Fact]
    public void AConnectionAsksForARenewalAfterADay()
    {
        // Arranging a connection on a fake clock
        FakeTimeProvider clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        SessionSecrets secrets = KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(peer));
        SecureRecordStream stream = new SecureRecordStream(new MemoryStream(), secrets, isClient: true);
        SecureTransportConnection connection = new SecureTransportConnection(new FailingConnection(), stream, SigningPublicKey.FromKey(peer),
            SecureTransportConnector.RenewalAge, clock, NullLogger.Instance, null);

        // Acting
        bool dueAtFirst = connection.IsRenewalDue;
        clock.Advance(SecureTransportConnector.RenewalAge);

        // Checks
        dueAtFirst.ShouldBeFalse();
        connection.IsRenewalDue.ShouldBeTrue();
    }

    /// <summary>
    /// A connection closed from another task whose close record cannot be written is cut in any case, the failure logged, never thrown
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ACloseThatCannotBeWrittenIsLoggedAndCutAsync()
    {
        // Arranging a connection whose stream below fails
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        SessionSecrets secrets = KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(peer));
        FailingConnection inner = new FailingConnection();
        SecureRecordStream stream = new SecureRecordStream(inner.Stream, secrets, isClient: true);
        bool isRemoved = false;
        SecureTransportConnection connection = new SecureTransportConnection(inner, stream, SigningPublicKey.FromKey(peer), SecureTransportConnector.RenewalAge,
            TimeProvider.System, _clientLogger, () => isRemoved = true);

        // Acting: twice, the second call doing nothing
        await connection.CloseForAsync();
        await connection.CloseForAsync();

        // Checks
        inner.IsDisposed.ShouldBeTrue();
        isRemoved.ShouldBeTrue();
        _clientLogger.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not be sent", StringComparison.Ordinal)).ShouldBe(1);
    }

    /// <summary>
    /// The refusals of the server are paced per reason: a frequent reason never hides a rare one
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheRefusalsArePacedPerReasonAsync()
    {
        // Arranging a Very Back on a fake clock, so that the pace never lets a second entry through
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartVeryBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running, new FakeTimeProvider(DateTimeOffset.UtcNow));

        // Acting: three handshakes refused at the magic, then one with a key that is not pinned
        for (int index = 0; index < 3; index++)
        {
            await using ITransportConnection plain = await ConnectWithTlsOnlyAsync(port);
            await plain.Stream.WriteAsync(new byte[SecureHandshake.ClientHelloLength], token);
            await plain.Stream.FlushAsync(token);
            _ = await plain.Stream.ReadAsync(new byte[1], token);
        }
        await using (ITransportConnection unknown = await ConnectWithTlsOnlyAsync(port))
        {
            using ECDsa other = SecureChannelTestKeys.CreateSigningKey();
            byte[] hello = [.. "BGPS"u8.ToArray(), SecureHandshake.Version, SecureHandshake.Suite, .. Bytes(ClientNonce), .. Bytes(ClientEphemeralPoint),
                .. SigningPublicKey.FromKey(other).KeyId.ToArray()];
            await unknown.Stream.WriteAsync(hello, token);
            await unknown.Stream.FlushAsync(token);
            await WaitUntilAsync(() => _serverLogger.Entries.Any(entry => entry.Message.Contains("UnknownKey", StringComparison.Ordinal)));
            _serverLogger.Entries.ShouldContain(entry => entry.Message.Contains(SigningPublicKey.FromKey(other).KeyIdHex, StringComparison.Ordinal));
        }

        // Checks: one entry for the three refusals at the magic
        _serverLogger.Entries.Count(entry => entry.Message.Contains("Malformed", StringComparison.Ordinal)).ShouldBe(1);

        // Stopping the Very Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A key removed between the registration of a handshake and the attachment of its connection keeps the connection from being used
    /// </summary>
    [Fact]
    public void AKeyRemovedBeforeTheAttachmentRefusesTheConnection()
    {
        // Arranging a register over keys loaded from files, and a connection registered with the key of the peer
        string folder = Directory.CreateTempSubdirectory("bgp-registry-").FullName;
        try
        {
            using ECDsa other = SecureChannelTestKeys.CreateSigningKey();
            string keyPath = Path.Combine(folder, "veryback-signing.key");
            string peersPath = Path.Combine(folder, "back.pub");
            File.WriteAllText(keyPath, _veryBackKey.ExportEncryptedPkcs8PrivateKeyPem("signing key password",
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
            File.WriteAllLines(peersPath, [SigningPublicKey.FromKey(_backKey).ToBase64(), SigningPublicKey.FromKey(other).ToBase64()]);
            File.SetLastWriteTimeUtc(peersPath, DateTime.UtcNow.AddMinutes(-5));
            using SigningKeys keys = SigningKeys.FromFiles(keyPath, "signing key password", peersPath, "another password", () => _veryBack.Certificate, TimeProvider.System,
                NullLogger.Instance);
            using SecureConnectionRegistry registry = new SecureConnectionRegistry(keys, null, TimeProvider.System, NullLogger.Instance);
            SecureConnectionRegistry.Entry entry = registry.Register(SigningPublicKey.FromKey(_backKey));
            SessionSecrets secrets = KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(_backKey));
            SecureTransportConnection connection = new SecureTransportConnection(new FailingConnection(), new SecureRecordStream(new MemoryStream(), secrets, isClient: false),
                SigningPublicKey.FromKey(_backKey), SecureTransportListener.MaxAge, TimeProvider.System, NullLogger.Instance, entry.Remove);

            // Acting: the key removed while the handshake ends
            File.WriteAllLines(peersPath, [SigningPublicKey.FromKey(other).ToBase64()]);
            keys.ReloadIfChanged();

            // Checks
            entry.Attach(connection).ShouldBeFalse();
            registry.Count.ShouldBe(1);
            entry.Remove();
            registry.Count.ShouldBe(0);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    /// Method used to release the certificates and the keys
    /// </summary>
    public void Dispose()
    {
        // Releasing everything the tests made
        _back.Dispose();
        _veryBack.Dispose();
        _certificates.Dispose();
        _backKey.Dispose();
        _veryBackKey.Dispose();
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
    /// Connection whose stream fails every write and whose sending side cannot be closed, as a peer gone
    /// </summary>
    private sealed class FailingConnection : ITransportConnection
    {
        /// <summary>
        /// Whether the connection was disposed
        /// </summary>
        public bool IsDisposed { get; private set; }

        /// <inheritdoc/>
        public Stream Stream { get; } = new FailingStream();

        /// <inheritdoc/>
        public EndPoint RemoteEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 1);

        /// <inheritdoc/>
        public string? PeerIdentity => null;

        /// <inheritdoc/>
        public Task CloseSendAsync(CancellationToken cancellationToken = default)
        {
            // Function's return
            throw new IOException("The peer is gone");
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            // Setting
            IsDisposed = true;

            // Function's return
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Stream that fails every write
    /// </summary>
    private sealed class FailingStream : MemoryStream
    {
        /// <inheritdoc/>
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            // Function's return
            throw new IOException("The peer is gone");
        }
    }
}
