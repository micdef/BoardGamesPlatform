/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/KeyScheduleTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the primitives and the key schedule of the secure channel against vectors computed elsewhere
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
/// Tests used to check <see cref="KeySchedule"/>, and the primitives it relies on, with vectors that the code under test never computed
/// </summary>
public sealed class KeyScheduleTests
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
    /// Method used to rebuild an ephemeral key from its fixed scalar
    /// </summary>
    /// <param name="privateScalar">[Cannot be null] Hexadecimal of the scalar</param>
    /// <returns>
    /// The key
    /// </returns>
    private static ECDiffieHellman CreateKey(string privateScalar)
    {
        // Function's return
        return new FixedEntropy(new byte[HandshakeEntropy.NonceLength], Bytes(privateScalar)).CreateEphemeralKey();
    }

    #endregion

    #region Publics

    /// <summary>
    /// HKDF of .NET gives the values of the first test case of RFC 5869 (appendix A.1)
    /// </summary>
    [Fact]
    public void HkdfGivesTheFirstVectorOfRfc5869()
    {
        // Arranging the inputs of the RFC
        byte[] inputKey = Enumerable.Repeat((byte)0x0B, 22).ToArray();
        byte[] salt = Convert.FromHexString("000102030405060708090A0B0C");
        byte[] info = Convert.FromHexString("F0F1F2F3F4F5F6F7F8F9");

        // Acting
        byte[] prk = HKDF.Extract(HashAlgorithmName.SHA256, inputKey, salt);
        byte[] output = HKDF.Expand(HashAlgorithmName.SHA256, prk, 42, info);

        // Checks
        Convert.ToHexString(prk).ShouldBe("077709362C2E32DF0DDC3F0DC47BBA6390B6C73BB50F9C3122EC844AD7C2B3E5");
        Convert.ToHexString(output).ShouldBe("3CB25F25FAACD57A90434F64D0362F2A2D2D0A90CF1A5A4C5DB02D56ECC4C5BF34007208D5B887185865");
    }

    /// <summary>
    /// The ephemeral keys rebuilt from their scalars have the points, and agree on the secret, that OpenSSL computed
    /// </summary>
    [Fact]
    public void EcdhGivesTheSecretComputedElsewhere()
    {
        // Arranging
        using ECDiffieHellman client = CreateKey(ClientEphemeralPrivate);
        using ECDiffieHellman server = CreateKey(ServerEphemeralPrivate);

        // Acting
        byte[] clientPoint = P256.WritePoint(client.ExportParameters(includePrivateParameters: false));
        byte[] serverPoint = P256.WritePoint(server.ExportParameters(includePrivateParameters: false));
        byte[] fromClient = client.DeriveRawSecretAgreement(server.PublicKey);
        byte[] fromServer = server.DeriveRawSecretAgreement(client.PublicKey);

        // Checks
        Convert.ToHexString(clientPoint).ShouldBe(ClientEphemeralPoint);
        Convert.ToHexString(serverPoint).ShouldBe(ServerEphemeralPoint);
        Convert.ToHexString(fromClient).ShouldBe(SharedSecret);
        Convert.ToHexString(fromServer).ShouldBe(SharedSecret);
    }

    /// <summary>
    /// The key schedule gives the four values that an HKDF written from the RFC computed
    /// </summary>
    [Fact]
    public void TheKeyScheduleGivesTheValuesComputedElsewhere()
    {
        // Arranging
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        SigningPublicKey peerKey = SigningPublicKey.FromKey(peer);

        // Acting
        using SessionSecrets secrets = KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), peerKey);

        // Checks
        Convert.ToHexString(HKDF.Extract(HashAlgorithmName.SHA256, Bytes(SharedSecret), [.. Bytes(ClientNonce), .. Bytes(ServerNonce)])).ShouldBe(PseudoRandomKey);
        Convert.ToHexString(secrets.ClientToServerKey).ShouldBe(ClientToServerKey);
        Convert.ToHexString(secrets.ServerToClientKey).ShouldBe(ServerToClientKey);
        Convert.ToHexString(secrets.ClientToServerIv).ShouldBe(ClientToServerIv);
        Convert.ToHexString(secrets.ServerToClientIv).ShouldBe(ServerToClientIv);
        secrets.PeerKey.ShouldBe(peerKey);
    }

    /// <summary>
    /// The info of a value writes each part after its length, so that two different inputs never give the same info
    /// </summary>
    [Fact]
    public void TheInfoWritesEachPartAfterItsLength()
    {
        // Acting
        byte[] info = KeySchedule.BuildInfo(12, "ab", [0xFF, 0xFE]);

        // Check: length 12 on two bytes, label of 2 bytes, hash of 2 bytes
        Convert.ToHexString(info).ShouldBe("000C02616202FFFE");
    }

    /// <summary>
    /// Disposing the secrets erases them
    /// </summary>
    [Fact]
    public void DisposingTheSecretsErasesThem()
    {
        // Arranging
        using ECDsa peer = SecureChannelTestKeys.CreateSigningKey();
        SessionSecrets secrets = KeySchedule.Derive(Bytes(SharedSecret), Bytes(ClientNonce), Bytes(ServerNonce), Bytes(TranscriptHash), SigningPublicKey.FromKey(peer));

        // Acting
        secrets.Dispose();

        // Checks
        secrets.ClientToServerKey.ShouldAllBe(value => value == 0);
        secrets.ServerToClientKey.ShouldAllBe(value => value == 0);
        secrets.ClientToServerIv.ShouldAllBe(value => value == 0);
        secrets.ServerToClientIv.ShouldAllBe(value => value == 0);
    }

    /// <summary>
    /// A signature computed by OpenSSL verifies with the public key read from its point, and fails once changed
    /// </summary>
    [Fact]
    public void ASignatureComputedElsewhereVerifies()
    {
        // Arranging
        using ECDsa key = ECDsa.Create(P256.ReadPoint(Bytes(SigningPoint)));
        SigningPublicKey publicKey = SigningPublicKey.FromKey(key);
        byte[] changed = Bytes(Signature);
        changed[^1] ^= 0x01;

        // Checks
        publicKey.Verify(Bytes(SignedMessage), Bytes(Signature)).ShouldBeTrue();
        publicKey.Verify(Bytes(SignedMessage), changed).ShouldBeFalse();
        Bytes(SignedMessage).ShouldBe(SecureHandshake.BuildSignedData(SecureHandshake.ServerSignatureLabel, Bytes(TranscriptHash)));
    }

    /// <summary>
    /// The signed data of the client has its own label, and a signature computed by OpenSSL over it verifies
    /// </summary>
    [Fact]
    public void TheClientLabelIsTheOneComputedElsewhere()
    {
        // Arranging
        using ECDsa key = ECDsa.Create(P256.ReadPoint(Bytes(SigningPoint)));
        SigningPublicKey publicKey = SigningPublicKey.FromKey(key);

        // Checks
        SecureHandshake.BuildSignedData(SecureHandshake.ClientSignatureLabel, Bytes(TranscriptHash)).ShouldBe(Bytes(ClientSignedMessage));
        publicKey.Verify(Bytes(ClientSignedMessage), Bytes(ClientSignature)).ShouldBeTrue();
        publicKey.Verify(Bytes(SignedMessage), Bytes(ClientSignature)).ShouldBeFalse();
    }

    /// <summary>
    /// The transcript hashes the hashes of both certificates, then the messages in their order, as computed elsewhere
    /// </summary>
    [Fact]
    public void TheTranscriptIsComposedAsComputedElsewhere()
    {
        // Arranging the messages of the vector
        byte[] m1 = Enumerable.Range(0, 135).Select(index => (byte)index).ToArray();
        byte[] m2 = Enumerable.Range(0, 199).Select(index => (byte)(index * 7 % 256)).ToArray();
        byte[] m3 = Enumerable.Range(100, 64).Select(index => (byte)index).ToArray();
        byte[] certS = Bytes(TranscriptServerCertificate);
        byte[] certC = Bytes(TranscriptClientCertificate);

        // Checks
        Convert.ToHexString(SecureHandshake.HashTranscript(certS, certC, m1, m2.AsMemory(0, 135))).ShouldBe(TranscriptT2);
        Convert.ToHexString(SecureHandshake.HashTranscript(certS, certC, m1, m2)).ShouldBe(TranscriptT3);
        Convert.ToHexString(SecureHandshake.HashTranscript(certS, certC, m1, m2, m3)).ShouldBe(TranscriptTh);
        Convert.ToHexString(SecureHandshake.HashTranscript(certC, certS, m1, m2, m3)).ShouldNotBe(TranscriptTh);
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
