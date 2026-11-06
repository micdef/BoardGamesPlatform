/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureHandshakeTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the handshake of the secure channel: agreement, and every check that refuses a peer
 */

// Using
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;

// Static using
using static BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors.SecureChannelVectors;

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="SecureHandshake"/>, over a real loopback connection
/// </summary>
public sealed class SecureHandshakeTests : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Authority of the certificates of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// TLS certificate of the client (the Back)
    /// </summary>
    private readonly X509Certificate2 _clientCertificate;

    /// <summary>
    /// TLS certificate of the server (the Very Back)
    /// </summary>
    private readonly X509Certificate2 _serverCertificate;

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
    public SecureHandshakeTests()
    {
        // Setting
        _clientCertificate = _certificates.Issue(PlatformRole.Back);
        _serverCertificate = _certificates.Issue(PlatformRole.VeryBack);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to run both ends of a handshake at the same time
    /// </summary>
    /// <param name="clientKeys">[Cannot be null] Keys of the client</param>
    /// <param name="serverKeys">[Cannot be null] Keys of the server</param>
    /// <param name="serverViewOfClient">Certificate of the client as the server sees it; the real one when null</param>
    /// <returns>
    /// The result of each end: its secrets, or what it threw
    /// </returns>
    private async Task<(Task<SessionSecrets> Client, Task<SessionSecrets> Server)> RunBothAsync(SigningKeySet clientKeys, SigningKeySet serverKeys,
        X509Certificate2? serverViewOfClient = null)
    {
        // Running each end on its side of a loopback connection, each closing its side when it ends
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<SessionSecrets> client = RunAndCloseAsync(clientStream, () => SecureHandshake.RunClientAsync(clientStream, clientKeys, _serverCertificate,
            _clientCertificate, HandshakeEntropy.System, _ => true, token));
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(serverStream, serverKeys, _serverCertificate,
            serverViewOfClient ?? _clientCertificate, HandshakeEntropy.System, _ => true, token));
        try
        {
            await Task.WhenAll(client, server);
        }
        catch (Exception exception) when (exception is SecureChannelException or IOException)
        {
            // Each task keeps its own result, checked by the test
        }

        // Function's return
        return (client, server);
    }

    /// <summary>
    /// Method used to run one end, then close its stream so that the other end never waits for ever
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream of the end</param>
    /// <param name="run">[Cannot be null] Handshake of the end</param>
    /// <returns>
    /// The secrets of the end
    /// </returns>
    private static async Task<SessionSecrets> RunAndCloseAsync(NetworkStream stream, Func<Task<SessionSecrets>> run)
    {
        // Running, the stream being closed in any case
        try
        {
            // Function's return
            return await run();
        }
        finally
        {
            await stream.DisposeAsync();
        }
    }

    /// <summary>
    /// Method used to send raw bytes to a server running the handshake, and get what it threw
    /// </summary>
    /// <param name="serverKeys">[Cannot be null] Keys of the server</param>
    /// <param name="bytes">[Cannot be null] Bytes the client sends, then it closes</param>
    /// <returns>
    /// The failure of the server
    /// </returns>
    private async Task<Exception> SendToServerAsync(SigningKeySet serverKeys, byte[] bytes)
    {
        // Writing the bytes, then closing the client side; the server must refuse them
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        await using NetworkStream client = clientStream;
        await using NetworkStream server = serverStream;
        Task<SessionSecrets> running = SecureHandshake.RunServerAsync(server, serverKeys, _serverCertificate, _clientCertificate, HandshakeEntropy.System, _ => true,
            TestContext.Current.CancellationToken);
        await client.WriteAsync(bytes, TestContext.Current.CancellationToken);
        client.Socket.Shutdown(SocketShutdown.Send);

        // Function's return
        return await Should.ThrowAsync<SecureChannelException>(running);
    }

    /// <summary>
    /// Method used to build a valid M1 of a client, to change before sending it
    /// </summary>
    /// <param name="clientKeys">[Cannot be null] Keys of the client</param>
    /// <returns>
    /// The 135 bytes of M1
    /// </returns>
    private static byte[] BuildClientHello(SigningKeySet clientKeys)
    {
        // Magic, version, suite, nonce, point, key id
        return [.. "BGPS"u8.ToArray(), SecureHandshake.Version, SecureHandshake.Suite, .. Bytes(ClientNonce), .. Bytes(ClientEphemeralPoint), .. clientKeys.OwnKey.KeyId.ToArray()];
    }

    #endregion

    #region Publics

    /// <summary>
    /// Two ends that pin each other agree on the same secrets, each knowing the key of the other
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task BothEndsAgreeOnTheSameSecretsAsync()
    {
        // Arranging
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);

        // Acting
        (Task<SessionSecrets> client, Task<SessionSecrets> server) = await RunBothAsync(clientKeys, serverKeys);

        // Checks
        using SessionSecrets clientSecrets = await client;
        using SessionSecrets serverSecrets = await server;
        clientSecrets.ClientToServerKey.ShouldBe(serverSecrets.ClientToServerKey);
        clientSecrets.ServerToClientKey.ShouldBe(serverSecrets.ServerToClientKey);
        clientSecrets.ClientToServerIv.ShouldBe(serverSecrets.ClientToServerIv);
        clientSecrets.ServerToClientIv.ShouldBe(serverSecrets.ServerToClientIv);
        clientSecrets.ClientToServerKey.ShouldNotBe(clientSecrets.ServerToClientKey);
        clientSecrets.PeerKey.ShouldBe(serverKeys.OwnKey);
        serverSecrets.PeerKey.ShouldBe(clientKeys.OwnKey);
    }

    /// <summary>
    /// A client whose signing key the server does not pin is refused by the server, at its key id
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AClientWithAnotherKeyIsRefusedAsync()
    {
        // Arranging: the server pins another key than the one of the client
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa pinnedInstead = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, pinnedInstead);

        // Acting
        (Task<SessionSecrets> client, Task<SessionSecrets> server) = await RunBothAsync(clientKeys, serverKeys);

        // Checks: nothing is sent to the client, which only sees the connection close; the server tells which key id was named
        SecureChannelException refused = await Should.ThrowAsync<SecureChannelException>(server);
        refused.Reason.ShouldBe(SecureChannelFailure.UnknownKey);
        refused.KeyIdHex.ShouldBe(clientKeys.OwnKey.KeyIdHex);
        (await Should.ThrowAsync<SecureChannelException>(client)).Reason.ShouldBe(SecureChannelFailure.PeerClosed);
    }

    /// <summary>
    /// A server whose signing key the client does not pin is refused by the client
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AServerWithAnotherKeyIsRefusedAsync()
    {
        // Arranging: the client pins another key than the one of the server
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa pinnedInstead = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, pinnedInstead);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);

        // Acting
        (Task<SessionSecrets> client, Task<SessionSecrets> server) = await RunBothAsync(clientKeys, serverKeys);

        // Checks
        (await Should.ThrowAsync<SecureChannelException>(client)).Reason.ShouldBe(SecureChannelFailure.UnknownKey);
        (await Should.ThrowAsync<SecureChannelException>(server)).Reason.ShouldBe(SecureChannelFailure.PeerClosed);
    }

    /// <summary>
    /// A party in the middle that ends TLS with certificates of its own makes the ends see other certificates: the signature fails
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task OtherCertificatesOnEachSideBreakTheSignaturesAsync()
    {
        // Arranging: the server sees another certificate of the client than the one the client presented
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        X509Certificate2 middle = _certificates.Issue(PlatformRole.Back);

        // Acting
        (Task<SessionSecrets> client, Task<SessionSecrets> server) = await RunBothAsync(clientKeys, serverKeys, middle);

        // Checks: the client refuses the signature of the server, made over another transcript
        (await Should.ThrowAsync<SecureChannelException>(client)).Reason.ShouldBe(SecureChannelFailure.BadSignature);
        (await Should.ThrowAsync<SecureChannelException>(server)).Reason.ShouldBe(SecureChannelFailure.PeerClosed);
    }

    /// <summary>
    /// A client whose pinned key is the key of its own TLS certificate is refused by the server
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ASigningKeyThatIsTheCertificateKeyIsRefusedByThePeerAsync()
    {
        // Arranging: the client signs with the key of its certificate, and the server pins it
        ECDsa clientKey = _clientCertificate.GetECDsaPrivateKey()!;
        using ECDsa pinned = _clientCertificate.GetECDsaPublicKey()!;
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, pinned);

        // Acting
        (Task<SessionSecrets> _, Task<SessionSecrets> server) = await RunBothAsync(clientKeys, serverKeys);

        // Check
        (await Should.ThrowAsync<SecureChannelException>(server)).Reason.ShouldBe(SecureChannelFailure.PeerKeyIsCertificateKey);
    }

    /// <summary>
    /// A key removed from the list during the handshake is refused at its end, so that a reload is never missed
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AKeyRemovedDuringTheHandshakeIsRefusedAtItsEndAsync()
    {
        // Arranging
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting: the server tells its list no longer holds the key of the client
        Task<SessionSecrets> client = RunAndCloseAsync(clientStream, () => SecureHandshake.RunClientAsync(clientStream, clientKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => true, token));
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(serverStream, serverKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => false, token));

        // Checks
        (await Should.ThrowAsync<SecureChannelException>(server)).Reason.ShouldBe(SecureChannelFailure.PeerKeyRemoved);
        (await client).Dispose();
    }

    /// <summary>
    /// A session without the certificate of an end is refused before anything is sent
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task ASessionWithoutCertificateIsRefusedAsync()
    {
        // Arranging
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        using MemoryStream stream = new MemoryStream();

        // Acting
        SecureChannelException failure = await Should.ThrowAsync<SecureChannelException>(SecureHandshake.RunClientAsync(stream, clientKeys, null, _clientCertificate,
            HandshakeEntropy.System, _ => true, TestContext.Current.CancellationToken));

        // Checks
        failure.Reason.ShouldBe(SecureChannelFailure.NoCertificate);
        stream.Length.ShouldBe(0);
    }

    /// <summary>
    /// A first message with another magic, version or suite, an invalid point or a key not pinned is refused by the server, with its reason
    /// </summary>
    /// <param name="change">What is changed in M1</param>
    /// <param name="reason">Reason expected</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData("magic", SecureChannelFailure.Malformed)]
    [InlineData("version", SecureChannelFailure.UnsupportedVersion)]
    [InlineData("suite", SecureChannelFailure.UnsupportedVersion)]
    [InlineData("point", SecureChannelFailure.Malformed)]
    [InlineData("keyId", SecureChannelFailure.UnknownKey)]
    [InlineData("serverKeyId", SecureChannelFailure.UnknownKey)]
    [InlineData("short", SecureChannelFailure.PeerClosed)]
    public async Task AChangedFirstMessageIsRefusedAsync(string change, SecureChannelFailure reason)
    {
        // Arranging
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        byte[] hello = BuildClientHello(clientKeys);
        switch (change)
        {
            case "magic":
                hello[0] = (byte)'X';
                break;
            case "version":
                hello[4] = 2;
                break;
            case "suite":
                hello[5] = 2;
                break;
            case "point":
                hello[6 + 32 + 64] ^= 0x01;
                break;
            case "keyId":
                hello[^1] ^= 0x01;
                break;
            case "serverKeyId":
                serverKeys.OwnKey.KeyId.ToArray().CopyTo(hello, hello.Length - SigningPublicKey.KeyIdLength);
                break;
            default:
                hello = hello[..100];
                break;
        }

        // Acting
        Exception failure = await SendToServerAsync(serverKeys, hello);

        // Check
        ((SecureChannelException)failure).Reason.ShouldBe(reason);
    }

    /// <summary>
    /// A replayed first message gets a new answer, so that the signature of the client recorded from another handshake no longer verifies
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AReplayedHandshakeFailsAtTheSignatureOfTheClientAsync()
    {
        // Arranging: a first handshake, whose M1 and M3 a party in the middle records
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        FixedEntropy clientEntropy = new FixedEntropy(Bytes(ClientNonce), Bytes(ClientEphemeralPrivate));
        using MemoryStream recorded = new MemoryStream();
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(serverStream, serverKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => true, token));
        RecordingStream recording = new RecordingStream(clientStream, recorded);
        using SessionSecrets first = await RunAndCloseAsync(clientStream, () => SecureHandshake.RunClientAsync(recording, clientKeys, _serverCertificate, _clientCertificate,
            clientEntropy, _ => true, token));
        (await server).Dispose();

        // Acting: the same M1 and M3 sent again to the server, which answers with a fresh nonce and ephemeral key
        Exception failure = await SendToServerAsync(serverKeys, recorded.ToArray());

        // Check
        ((SecureChannelException)failure).Reason.ShouldBe(SecureChannelFailure.BadSignature);
    }

    /// <summary>
    /// Both signatures cover the transcript exactly as the specification composes it: the hashes of both certificates, then the messages
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task TheSignaturesCoverTheTranscriptAsTheSpecificationSaysAsync()
    {
        // Arranging: both ends record what they write
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        using MemoryStream clientWrites = new MemoryStream();
        using MemoryStream serverWrites = new MemoryStream();
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(new RecordingStream(serverStream, serverWrites), serverKeys,
            _serverCertificate, _clientCertificate, HandshakeEntropy.System, _ => true, token));
        using SessionSecrets clientSecrets = await RunAndCloseAsync(clientStream, () => SecureHandshake.RunClientAsync(new RecordingStream(clientStream, clientWrites),
            clientKeys, _serverCertificate, _clientCertificate, HandshakeEntropy.System, _ => true, token));
        (await server).Dispose();

        // Checks: each signature verifies over the transcript composed here, field by field, as the specification writes it
        byte[] m1 = clientWrites.ToArray()[..SecureHandshake.ClientHelloLength];
        byte[] m3 = clientWrites.ToArray()[SecureHandshake.ClientHelloLength..];
        byte[] m2 = serverWrites.ToArray();
        byte[] certS = SHA256.HashData(_serverCertificate.RawData);
        byte[] certC = SHA256.HashData(_clientCertificate.RawData);
        byte[] t2 = SHA256.HashData([.. certS, .. certC, .. m1, .. m2[..SecureHandshake.ClientHelloLength]]);
        byte[] t3 = SHA256.HashData([.. certS, .. certC, .. m1, .. m2]);
        m3.Length.ShouldBe(SecureHandshake.ClientSignatureLength);
        serverKeys.OwnKey.Verify([.. "BGPS v1 back-veryback server signature"u8.ToArray(), 0x00, .. t2], m2.AsSpan(SecureHandshake.ClientHelloLength)).ShouldBeTrue();
        clientKeys.OwnKey.Verify([.. "BGPS v1 back-veryback client signature"u8.ToArray(), 0x00, .. t3], m3).ShouldBeTrue();
    }

    /// <summary>
    /// A party in the middle that replaces the ephemeral key of the server by another valid point breaks the signature of the server
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task AReplacedEphemeralKeyBreaksTheSignatureAsync()
    {
        // Arranging two connections joined by a party in the middle
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        (NetworkStream clientStream, NetworkStream middleToClient) = await SecureChannelTestKeys.OpenStreamsAsync();
        (NetworkStream middleToServer, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<SessionSecrets> client = RunAndCloseAsync(clientStream, () => SecureHandshake.RunClientAsync(clientStream, clientKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => true, token));
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(serverStream, serverKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => true, token));

        // Acting: M1 relayed as it is, M2 with the ephemeral key of the vector instead of the one of the server
        await using (middleToClient)
        await using (middleToServer)
        {
            byte[] m1 = new byte[SecureHandshake.ClientHelloLength];
            await middleToClient.ReadExactlyAsync(m1, token);
            await middleToServer.WriteAsync(m1, token);
            byte[] m2 = new byte[SecureHandshake.ServerHelloLength];
            await middleToServer.ReadExactlyAsync(m2, token);
            Bytes(ServerEphemeralPoint).CopyTo(m2, 4 + 1 + 1 + HandshakeEntropy.NonceLength);
            await middleToClient.WriteAsync(m2, token);

            // Check
            (await Should.ThrowAsync<SecureChannelException>(client)).Reason.ShouldBe(SecureChannelFailure.BadSignature);
        }
        await Should.ThrowAsync<SecureChannelException>(server);
    }

    /// <summary>
    /// The signature of the server sent back as the signature of the client is refused: the labels of the roles differ
    /// </summary>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Fact]
    public async Task TheSignatureOfTheServerSentBackIsRefusedAsync()
    {
        // Arranging: a client played by the test, with a pinned key id but no signing key
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        (NetworkStream clientStream, NetworkStream serverStream) = await SecureChannelTestKeys.OpenStreamsAsync();
        CancellationToken token = TestContext.Current.CancellationToken;
        Task<SessionSecrets> server = RunAndCloseAsync(serverStream, () => SecureHandshake.RunServerAsync(serverStream, serverKeys, _serverCertificate, _clientCertificate,
            HandshakeEntropy.System, _ => true, token));

        // Acting: M1, then the signature of the server as M3
        await using (clientStream)
        {
            await clientStream.WriteAsync(BuildClientHello(clientKeys), token);
            byte[] m2 = new byte[SecureHandshake.ServerHelloLength];
            await clientStream.ReadExactlyAsync(m2, token);
            await clientStream.WriteAsync(m2.AsMemory(SecureHandshake.ClientHelloLength), token);

            // Check
            (await Should.ThrowAsync<SecureChannelException>(server)).Reason.ShouldBe(SecureChannelFailure.BadSignature);
        }
    }

    /// <summary>
    /// A changed second message is refused by the client with its reason, and the client sends no third message
    /// </summary>
    /// <param name="change">What is changed in M2</param>
    /// <param name="reason">Reason expected</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData("none", SecureChannelFailure.BadSignature)]
    [InlineData("magic", SecureChannelFailure.Malformed)]
    [InlineData("version", SecureChannelFailure.UnsupportedVersion)]
    [InlineData("suite", SecureChannelFailure.UnsupportedVersion)]
    [InlineData("point", SecureChannelFailure.Malformed)]
    [InlineData("keyId", SecureChannelFailure.UnknownKey)]
    [InlineData("zeroSignature", SecureChannelFailure.Malformed)]
    [InlineData("short", SecureChannelFailure.PeerClosed)]
    public async Task AChangedSecondMessageIsRefusedAsync(string change, SecureChannelFailure reason)
    {
        // Arranging an M2 well formed, with the key id the client pins, and a signature in range that the server never made
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        byte[] m2 = [.. "BGPS"u8.ToArray(), SecureHandshake.Version, SecureHandshake.Suite, .. Bytes(ServerNonce), .. Bytes(ServerEphemeralPoint),
            .. SigningPublicKey.FromKey(serverKey).KeyId.ToArray(), .. Bytes(Signature)];
        switch (change)
        {
            case "magic":
                m2[0] = (byte)'X';
                break;
            case "version":
                m2[4] = 2;
                break;
            case "suite":
                m2[5] = 2;
                break;
            case "point":
                m2[6 + 32 + 64] ^= 0x01;
                break;
            case "keyId":
                m2[134] ^= 0x01;
                break;
            case "zeroSignature":
                Array.Clear(m2, SecureHandshake.ClientHelloLength, P256.SignatureLength);
                break;
            case "short":
                m2 = m2[..150];
                break;
        }
        ScriptedStream stream = new ScriptedStream(m2);

        // Acting
        SecureChannelException failure = await Should.ThrowAsync<SecureChannelException>(SecureHandshake.RunClientAsync(stream, clientKeys, _serverCertificate,
            _clientCertificate, HandshakeEntropy.System, _ => true, TestContext.Current.CancellationToken));

        // Checks: the reason, and nothing sent after M1
        failure.Reason.ShouldBe(reason);
        stream.Written.Length.ShouldBe(SecureHandshake.ClientHelloLength);
    }

    /// <summary>
    /// A third message out of range, not signed by the client, or missing is refused by the server, which reads no record
    /// </summary>
    /// <param name="change">What the client sends as M3</param>
    /// <param name="reason">Reason expected</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData("zero", SecureChannelFailure.Malformed)]
    [InlineData("otherSignature", SecureChannelFailure.BadSignature)]
    [InlineData("none", SecureChannelFailure.PeerClosed)]
    public async Task AChangedThirdMessageIsRefusedAsync(string change, SecureChannelFailure reason)
    {
        // Arranging M1 of the client, then the M3 of the case
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        byte[] m3 = change switch
        {
            "zero" => new byte[P256.SignatureLength],
            "otherSignature" => Bytes(Signature),
            _ => []
        };
        ScriptedStream stream = new ScriptedStream([.. BuildClientHello(clientKeys), .. m3]);

        // Acting
        SecureChannelException failure = await Should.ThrowAsync<SecureChannelException>(SecureHandshake.RunServerAsync(stream, serverKeys, _serverCertificate,
            _clientCertificate, HandshakeEntropy.System, _ => true, TestContext.Current.CancellationToken));

        // Checks: the reason, and nothing written after M2
        failure.Reason.ShouldBe(reason);
        stream.Written.Length.ShouldBe(SecureHandshake.ServerHelloLength);
    }

    /// <summary>
    /// Random and mutated messages never succeed and never fail otherwise than with a reason of the closed list, on either side
    /// </summary>
    /// <param name="seed">Seed of the messages, so that a failure can be replayed</param>
    /// <returns>
    /// A task that completes once checked
    /// </returns>
    [Theory]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    public async Task RandomMessagesAreRefusedWithAReasonAsync(int seed)
    {
        // Arranging
        Random random = new Random(seed);
        ECDsa clientKey = SecureChannelTestKeys.CreateSigningKey();
        ECDsa serverKey = SecureChannelTestKeys.CreateSigningKey();
        SigningKeySet clientKeys = SecureChannelTestKeys.CreateSet(clientKey, serverKey);
        SigningKeySet serverKeys = SecureChannelTestKeys.CreateSet(serverKey, clientKey);
        byte[] validHello = BuildClientHello(clientKeys);
        byte[] validServerHello = [.. "BGPS"u8.ToArray(), SecureHandshake.Version, SecureHandshake.Suite, .. Bytes(ServerNonce), .. Bytes(ServerEphemeralPoint),
            .. serverKeys.OwnKey.KeyId.ToArray(), .. Bytes(Signature)];
        CancellationToken token = TestContext.Current.CancellationToken;

        // Acting and checking, 60 messages per side: random bytes of any length, or a valid message with 1 to 3 bytes changed
        for (int index = 0; index < 60; index++)
        {
            byte[] toServer = Mutate(random, validHello, SecureHandshake.ClientHelloLength + P256.SignatureLength);
            byte[] toClient = Mutate(random, validServerHello, SecureHandshake.ServerHelloLength);
            SecureChannelException serverFailure = await Should.ThrowAsync<SecureChannelException>(SecureHandshake.RunServerAsync(new ScriptedStream(toServer),
                serverKeys, _serverCertificate, _clientCertificate, HandshakeEntropy.System, _ => true, token));
            SecureChannelException clientFailure = await Should.ThrowAsync<SecureChannelException>(SecureHandshake.RunClientAsync(new ScriptedStream(toClient),
                clientKeys, _serverCertificate, _clientCertificate, HandshakeEntropy.System, _ => true, token));
            Enum.IsDefined(serverFailure.Reason).ShouldBeTrue();
            Enum.IsDefined(clientFailure.Reason).ShouldBeTrue();
        }
    }

    /// <summary>
    /// Method used to make a random message: random bytes of any length, or a valid message with 1 to 3 bytes changed and random bytes after
    /// </summary>
    /// <param name="random">[Cannot be null] Source of the seed of the test</param>
    /// <param name="valid">[Cannot be null] Valid message</param>
    /// <param name="maxRandomLength">Longest random message</param>
    /// <returns>
    /// The message
    /// </returns>
    private static byte[] Mutate(Random random, byte[] valid, int maxRandomLength)
    {
        // Random bytes half of the time
        if (random.Next(2) == 0)
        {
            byte[] noise = new byte[random.Next(0, maxRandomLength + 1)];
            random.NextBytes(noise);

            // Function's return
            return noise;
        }

        // Else the valid message changed, followed by a random third message
        byte[] mutated = [.. valid, .. new byte[P256.SignatureLength]];
        random.NextBytes(mutated.AsSpan(valid.Length));
        int changes = random.Next(1, 4);
        for (int change = 0; change < changes; change++)
            mutated[random.Next(valid.Length)] ^= (byte)random.Next(1, 256);

        // Function's return
        return mutated;
    }

    /// <summary>
    /// Method used to release the certificates
    /// </summary>
    public void Dispose()
    {
        // Releasing the authority and what it issued
        _certificates.Dispose();
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
