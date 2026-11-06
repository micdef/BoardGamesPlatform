/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureHandshake.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Handshake of the secure channel: three fixed-size messages, signed with the pinned keys, then the key schedule
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Authenticated ECDH handshake of the secure channel, specified in <c>doc/dev/secure-channel.md</c>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> prove that the peer holds a signing key pinned by this tier, on top of its TLS certificate, and agree on fresh keys
/// for the records of the connection.</para>
/// <para><b>Behavior:</b> the client sends M1 (magic, version, suite, nonce, ephemeral key, key id), the server answers M2 (the same fields
/// and its signature of the transcript), and the client ends with M3 (its signature). Both signatures cover the hashes of the two TLS
/// certificates of the session, both nonces, both ephemeral keys and both key ids; the labels name the version, the link and the role. Every
/// value received is checked before it is used, and any failure throws a <see cref="SecureChannelException"/> with its reason, with nothing
/// sent to the peer. The last step checks that the key of the peer is still pinned now, so that a reload during the handshake is never
/// missed. A cancellation (the handshake timeout of the caller) throws an <see cref="OperationCanceledException"/>.</para>
/// </remarks>
internal static class SecureHandshake
{
    #region Members

    #region Constants

    /// <summary>
    /// Version of the secure channel
    /// </summary>
    public const byte Version = 1;

    /// <summary>
    /// Only suite of version 1: ECDH and ECDSA over P-256, HKDF-SHA256, AES-256-GCM
    /// </summary>
    public const byte Suite = 1;

    /// <summary>
    /// Size of the fixed fields that M1 and M2 share, in bytes
    /// </summary>
    private const int HelloLength = 4 + 1 + 1 + HandshakeEntropy.NonceLength + P256.PointLength + SigningPublicKey.KeyIdLength;

    /// <summary>
    /// Size of M1, in bytes
    /// </summary>
    public const int ClientHelloLength = HelloLength;

    /// <summary>
    /// Size of M2, in bytes
    /// </summary>
    public const int ServerHelloLength = HelloLength + P256.SignatureLength;

    /// <summary>
    /// Size of M3, in bytes
    /// </summary>
    public const int ClientSignatureLength = P256.SignatureLength;

    /// <summary>
    /// Label of the signature of the server
    /// </summary>
    public const string ServerSignatureLabel = "BGPS v1 back-veryback server signature";

    /// <summary>
    /// Label of the signature of the client
    /// </summary>
    public const string ClientSignatureLabel = "BGPS v1 back-veryback client signature";

    /// <summary>
    /// Offset of the version in M1 and M2
    /// </summary>
    private const int VersionOffset = 4;

    /// <summary>
    /// Offset of the nonce in M1 and M2
    /// </summary>
    private const int NonceOffset = 6;

    /// <summary>
    /// Offset of the ephemeral key in M1 and M2
    /// </summary>
    private const int PointOffset = NonceOffset + HandshakeEntropy.NonceLength;

    /// <summary>
    /// Offset of the key id in M1 and M2
    /// </summary>
    private const int KeyIdOffset = PointOffset + P256.PointLength;

    /// <summary>
    /// Magic of M1 and M2: "BGPS"
    /// </summary>
    private static readonly byte[] _magic = "BGPS"u8.ToArray();

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
    /// Method used to hash the leaf certificate of an end, as the TLS session gives it
    /// </summary>
    /// <param name="certificate">[Cannot be null] Certificate of the session</param>
    /// <returns>
    /// The SHA-256 of its DER
    /// </returns>
    internal static byte[] HashCertificate(X509Certificate certificate)
    {
        // Function's return
        return SHA256.HashData(certificate.GetRawCertData());
    }

    /// <summary>
    /// Method used to refuse a pinned key of the peer that is also the key of the TLS certificate it presented
    /// </summary>
    /// <param name="peerKey">[Cannot be null] Pinned key named by the peer</param>
    /// <param name="peerCertificate">[Cannot be null] TLS certificate of the peer</param>
    /// <exception cref="SecureChannelException">If both are the same key</exception>
    private static void RefuseCertificateKey(SigningPublicKey peerKey, X509Certificate peerCertificate)
    {
        // Comparing the normalized forms; a certificate whose key is not of P-256 cannot be the same key
        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(peerCertificate.GetRawCertData());
        using ECDsa? certificateKey = certificate.GetECDsaPublicKey();
        if (certificateKey is null)
            return;
        SigningPublicKey normalized;
        try
        {
            normalized = SigningPublicKey.FromKey(certificateKey);
        }
        catch (CryptographicException)
        {
            return;
        }
        if (normalized.Equals(peerKey))
            throw new SecureChannelException(SecureChannelFailure.PeerKeyIsCertificateKey);
    }

    /// <summary>
    /// Method used to hash the transcript, prefixed by the hashes of the certificates
    /// </summary>
    /// <param name="serverCertificateHash">Hash of the certificate of the server</param>
    /// <param name="clientCertificateHash">Hash of the certificate of the client</param>
    /// <param name="parts">Messages, in their order</param>
    /// <returns>
    /// The SHA-256 of all of them
    /// </returns>
    internal static byte[] HashTranscript(byte[] serverCertificateHash, byte[] clientCertificateHash, params ReadOnlyMemory<byte>[] parts)
    {
        // Hashing the parts one after the other
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(serverCertificateHash);
        hash.AppendData(clientCertificateHash);
        foreach (ReadOnlyMemory<byte> part in parts)
            hash.AppendData(part.Span);

        // Function's return
        return hash.GetHashAndReset();
    }

    /// <summary>
    /// Method used to build the data a signature covers: the label, a zero byte, then the hash of the transcript
    /// </summary>
    /// <param name="label">[Cannot be null] Label of the role</param>
    /// <param name="transcriptHash">Hash of the transcript</param>
    /// <returns>
    /// The data to sign or to check
    /// </returns>
    internal static byte[] BuildSignedData(string label, ReadOnlySpan<byte> transcriptHash)
    {
        // Joining the parts
        byte[] labelBytes = Encoding.ASCII.GetBytes(label);
        byte[] data = new byte[labelBytes.Length + 1 + transcriptHash.Length];
        labelBytes.CopyTo(data, 0);
        transcriptHash.CopyTo(data.AsSpan(labelBytes.Length + 1));

        // Function's return
        return data;
    }

    /// <summary>
    /// Method used to write the fixed fields of M1 or M2
    /// </summary>
    /// <param name="message">Message to fill, at least <see cref="HelloLength"/> bytes</param>
    /// <param name="nonce">Nonce of the sender</param>
    /// <param name="point">Ephemeral key of the sender</param>
    /// <param name="keyId">Key id of the sender</param>
    private static void WriteHello(Span<byte> message, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> point, ReadOnlySpan<byte> keyId)
    {
        // Writing each field at its place
        _magic.CopyTo(message);
        message[VersionOffset] = Version;
        message[VersionOffset + 1] = Suite;
        nonce.CopyTo(message[NonceOffset..]);
        point.CopyTo(message[PointOffset..]);
        keyId.CopyTo(message[KeyIdOffset..]);
    }

    /// <summary>
    /// Method used to read and check the fixed fields of M1 or M2
    /// </summary>
    /// <param name="message">Message received, at least <see cref="HelloLength"/> bytes</param>
    /// <param name="keys">[Cannot be null] Keys of this handshake</param>
    /// <returns>
    /// The pinned key named by the sender
    /// </returns>
    /// <exception cref="SecureChannelException">If the magic, the version, the suite, the point or the key id is refused</exception>
    private static SigningPublicKey ReadHello(ReadOnlySpan<byte> message, SigningKeySet keys)
    {
        // Checking the magic first, so that a peer without the channel is told apart from another version
        if (!message[..VersionOffset].SequenceEqual(_magic))
            throw new SecureChannelException(SecureChannelFailure.Malformed);
        if (message[VersionOffset] != Version || message[VersionOffset + 1] != Suite)
            throw new SecureChannelException(SecureChannelFailure.UnsupportedVersion);

        // Checking the point itself, then the key id against the pinned keys
        if (!P256.IsValidPoint(message.Slice(PointOffset, P256.PointLength)))
            throw new SecureChannelException(SecureChannelFailure.Malformed);
        ReadOnlySpan<byte> keyId = message.Slice(KeyIdOffset, SigningPublicKey.KeyIdLength);
        SigningPublicKey? peerKey = keys.FindPeerKey(keyId);

        // Function's return
        return peerKey ?? throw new SecureChannelException(keyId);
    }

    /// <summary>
    /// Method used to read a whole message, a peer that closes being told apart
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream of the connection</param>
    /// <param name="buffer">Buffer of the size of the message</param>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once the message is read
    /// </returns>
    /// <exception cref="SecureChannelException">If the peer closes before the end of the message</exception>
    private static async Task ReadMessageAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        // Reading the fixed size
        try
        {
            await stream.ReadExactlyAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (EndOfStreamException exception)
        {
            throw new SecureChannelException(SecureChannelFailure.PeerClosed, exception);
        }
    }

    /// <summary>
    /// Method used to agree on the ECDH secret and derive the secrets of the records, every intermediate value erased
    /// </summary>
    /// <param name="ephemeralKey">[Cannot be null] Ephemeral key of this end</param>
    /// <param name="peerPoint">Ephemeral key of the peer, checked</param>
    /// <param name="clientNonce">Nonce of the client</param>
    /// <param name="serverNonce">Nonce of the server</param>
    /// <param name="transcriptHash">Hash of the whole transcript</param>
    /// <param name="peerKey">[Cannot be null] Pinned key of the peer</param>
    /// <returns>
    /// The secrets of the records
    /// </returns>
    private static SessionSecrets Agree(ECDiffieHellman ephemeralKey, ReadOnlySpan<byte> peerPoint, ReadOnlySpan<byte> clientNonce, ReadOnlySpan<byte> serverNonce,
        ReadOnlySpan<byte> transcriptHash, SigningPublicKey peerKey)
    {
        // Agreeing on the raw secret, which only goes into HKDF
        using ECDiffieHellman peer = ECDiffieHellman.Create(P256.ReadPoint(peerPoint));
        using ECDiffieHellmanPublicKey peerPublicKey = peer.PublicKey;
        byte[] sharedSecret = ephemeralKey.DeriveRawSecretAgreement(peerPublicKey);
        try
        {
            // Function's return
            return KeySchedule.Derive(sharedSecret, clientNonce, serverNonce, transcriptHash, peerKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedSecret);
        }
    }

    /// <summary>
    /// Method used to check, as the last step, that the key of the peer is still pinned now
    /// </summary>
    /// <param name="peerKey">[Cannot be null] Pinned key of the peer</param>
    /// <param name="isStillPinned">[Cannot be null] Tells whether a key is in the current list of the tier</param>
    /// <exception cref="SecureChannelException">If the key was removed during the handshake</exception>
    private static void RefuseRemovedKey(SigningPublicKey peerKey, Func<SigningPublicKey, bool> isStillPinned)
    {
        // Check
        if (!isStillPinned(peerKey))
            throw new SecureChannelException(SecureChannelFailure.PeerKeyRemoved);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to run the handshake as the client
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream of the TLS session</param>
    /// <param name="keys">[Cannot be null] Snapshot of the keys of this tier</param>
    /// <param name="serverCertificate">Certificate of the server, as the TLS session gives it</param>
    /// <param name="clientCertificate">Certificate of this tier, as the TLS session gives it</param>
    /// <param name="entropy">[Cannot be null] Source of the nonce and of the ephemeral key</param>
    /// <param name="isStillPinned">[Cannot be null] Tells whether a key is in the current list of the tier, checked as the last step</param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the caller</param>
    /// <returns>
    /// The secrets of the records, owned by the caller, who disposes them
    /// </returns>
    /// <exception cref="SecureChannelException">If a check fails</exception>
    /// <exception cref="IOException">If the connection fails</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public static async Task<SessionSecrets> RunClientAsync(Stream stream, SigningKeySet keys, X509Certificate? serverCertificate, X509Certificate? clientCertificate,
        HandshakeEntropy entropy, Func<SigningPublicKey, bool> isStillPinned, CancellationToken cancellationToken)
    {
        // Hashing the certificates of the session, then sending M1
        X509Certificate server = serverCertificate ?? throw new SecureChannelException(SecureChannelFailure.NoCertificate);
        X509Certificate client = clientCertificate ?? throw new SecureChannelException(SecureChannelFailure.NoCertificate);
        byte[] serverCertificateHash = HashCertificate(server);
        byte[] clientCertificateHash = HashCertificate(client);
        byte[] clientNonce = entropy.CreateNonce();
        using ECDiffieHellman ephemeralKey = entropy.CreateEphemeralKey();
        byte[] clientHello = new byte[ClientHelloLength];
        WriteHello(clientHello, clientNonce, P256.WritePoint(ephemeralKey.ExportParameters(includePrivateParameters: false)), keys.OwnKey.KeyId.Span);
        await stream.WriteAsync(clientHello, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Reading M2 and checking it: its fields, the key of the server against its certificate, then its signature
        byte[] serverHello = new byte[ServerHelloLength];
        await ReadMessageAsync(stream, serverHello, cancellationToken).ConfigureAwait(false);
        SigningPublicKey serverKey = ReadHello(serverHello, keys);
        RefuseCertificateKey(serverKey, server);
        ReadOnlyMemory<byte> serverFields = serverHello.AsMemory(0, HelloLength);
        ReadOnlySpan<byte> serverSignature = serverHello.AsSpan(HelloLength, P256.SignatureLength);
        if (!P256.IsValidSignature(serverSignature))
            throw new SecureChannelException(SecureChannelFailure.Malformed);
        byte[] serverTranscript = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverFields);
        if (!serverKey.Verify(BuildSignedData(ServerSignatureLabel, serverTranscript), serverSignature))
            throw new SecureChannelException(SecureChannelFailure.BadSignature);

        // Signing the transcript with M2, and sending M3
        byte[] clientTranscript = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverHello);
        byte[] clientSignature = keys.Sign(BuildSignedData(ClientSignatureLabel, clientTranscript));
        await stream.WriteAsync(clientSignature, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Checking the key of the server is still pinned, then deriving the secrets
        RefuseRemovedKey(serverKey, isStillPinned);
        byte[] transcriptHash = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverHello, clientSignature);

        // Function's return
        return Agree(ephemeralKey, serverHello.AsSpan(PointOffset, P256.PointLength), clientNonce, serverHello.AsSpan(NonceOffset, HandshakeEntropy.NonceLength),
            transcriptHash, serverKey);
    }

    /// <summary>
    /// Method used to run the handshake as the server
    /// </summary>
    /// <param name="stream">[Cannot be null] Stream of the TLS session</param>
    /// <param name="keys">[Cannot be null] Snapshot of the keys of this tier</param>
    /// <param name="serverCertificate">Certificate of this tier, as the TLS session gives it</param>
    /// <param name="clientCertificate">Certificate of the client, as the TLS session gives it</param>
    /// <param name="entropy">[Cannot be null] Source of the nonce and of the ephemeral key</param>
    /// <param name="isStillPinned">[Cannot be null] Tells whether a key is in the current list of the tier, checked as the last step</param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the caller</param>
    /// <returns>
    /// The secrets of the records, owned by the caller, who disposes them
    /// </returns>
    /// <exception cref="SecureChannelException">If a check fails</exception>
    /// <exception cref="IOException">If the connection fails</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public static async Task<SessionSecrets> RunServerAsync(Stream stream, SigningKeySet keys, X509Certificate? serverCertificate, X509Certificate? clientCertificate,
        HandshakeEntropy entropy, Func<SigningPublicKey, bool> isStillPinned, CancellationToken cancellationToken)
    {
        // Hashing the certificates of the session, then reading M1 and checking it, the key of the client against its certificate
        X509Certificate server = serverCertificate ?? throw new SecureChannelException(SecureChannelFailure.NoCertificate);
        X509Certificate client = clientCertificate ?? throw new SecureChannelException(SecureChannelFailure.NoCertificate);
        byte[] serverCertificateHash = HashCertificate(server);
        byte[] clientCertificateHash = HashCertificate(client);
        byte[] clientHello = new byte[ClientHelloLength];
        await ReadMessageAsync(stream, clientHello, cancellationToken).ConfigureAwait(false);
        SigningPublicKey clientKey = ReadHello(clientHello, keys);
        RefuseCertificateKey(clientKey, client);

        // Answering M2, signed over the transcript so far
        byte[] serverNonce = entropy.CreateNonce();
        using ECDiffieHellman ephemeralKey = entropy.CreateEphemeralKey();
        byte[] serverHello = new byte[ServerHelloLength];
        WriteHello(serverHello, serverNonce, P256.WritePoint(ephemeralKey.ExportParameters(includePrivateParameters: false)), keys.OwnKey.KeyId.Span);
        byte[] serverTranscript = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverHello.AsMemory(0, HelloLength));
        keys.Sign(BuildSignedData(ServerSignatureLabel, serverTranscript)).CopyTo(serverHello, HelloLength);
        await stream.WriteAsync(serverHello, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Reading M3 and checking the signature of the client, before any record is read
        byte[] clientSignature = new byte[ClientSignatureLength];
        await ReadMessageAsync(stream, clientSignature, cancellationToken).ConfigureAwait(false);
        if (!P256.IsValidSignature(clientSignature))
            throw new SecureChannelException(SecureChannelFailure.Malformed);
        byte[] clientTranscript = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverHello);
        if (!clientKey.Verify(BuildSignedData(ClientSignatureLabel, clientTranscript), clientSignature))
            throw new SecureChannelException(SecureChannelFailure.BadSignature);

        // Checking the key of the client is still pinned, then deriving the secrets
        RefuseRemovedKey(clientKey, isStillPinned);
        byte[] transcriptHash = HashTranscript(serverCertificateHash, clientCertificateHash, clientHello, serverHello, clientSignature);

        // Function's return
        return Agree(ephemeralKey, clientHello.AsSpan(PointOffset, P256.PointLength), clientHello.AsSpan(NonceOffset, HandshakeEntropy.NonceLength), serverNonce,
            transcriptHash, clientKey);
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
