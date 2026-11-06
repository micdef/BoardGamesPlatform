/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SigningPublicKeyTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the normalized form, the key id and the reading of the public signing keys
 */

// Using
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport.Secure;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="SigningPublicKey"/>
/// </summary>
public sealed class SigningPublicKeyTests
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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// A key written in Base64 reads back to the same key, named by the SHA-256 of its normalized form
    /// </summary>
    [Fact]
    public void AKeyReadsBackToItself()
    {
        // Arranging
        using ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        SigningPublicKey publicKey = SigningPublicKey.FromKey(key);

        // Acting
        SigningPublicKey read = SigningPublicKey.FromBase64(publicKey.ToBase64());

        // Checks
        read.ShouldBe(publicKey);
        read.KeyId.ToArray().ShouldBe(SHA256.HashData(publicKey.SubjectPublicKeyInfo.Span));
        read.KeyIdHex.ShouldBe(Convert.ToHexString(read.KeyId.Span));
        read.HasKeyId(publicKey.KeyId.Span).ShouldBeTrue();
        read.SubjectPublicKeyInfo.ToArray().ShouldBe(key.ExportSubjectPublicKeyInfo());
    }

    /// <summary>
    /// The private and the public form of one key give the same key; two keys differ
    /// </summary>
    [Fact]
    public void OneKeyHasOneKeyId()
    {
        // Arranging
        using ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa other = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa publicOnly = ECDsa.Create();
        publicOnly.ImportSubjectPublicKeyInfo(key.ExportSubjectPublicKeyInfo(), out _);

        // Checks
        SigningPublicKey.FromKey(publicOnly).ShouldBe(SigningPublicKey.FromKey(key));
        SigningPublicKey.FromKey(other).ShouldNotBe(SigningPublicKey.FromKey(key));
        SigningPublicKey.FromKey(key).HasKeyId(SigningPublicKey.FromKey(other).KeyId.Span).ShouldBeFalse();
        SigningPublicKey.FromKey(key).HasKeyId(new byte[5]).ShouldBeFalse();
    }

    /// <summary>
    /// A key with explicit parameters, even of P-256, is refused: only the named curve is accepted
    /// </summary>
    [Fact]
    public void ExplicitParametersAreRefused()
    {
        // Arranging: the same key written with the explicit parameters of the curve
        using ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa explicitKey = ECDsa.Create(key.ExportExplicitParameters(includePrivateParameters: false));
        string base64 = Convert.ToBase64String(explicitKey.ExportSubjectPublicKeyInfo());

        // Check
        Should.Throw<CryptographicException>(() => SigningPublicKey.FromBase64(base64));
    }

    /// <summary>
    /// A key of another curve, a text that is not Base64, or bytes after the key are refused
    /// </summary>
    [Fact]
    public void OtherTextsAreRefused()
    {
        // Arranging
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        string trailing = Convert.ToBase64String([.. key.ExportSubjectPublicKeyInfo(), 0x00]);

        // Checks
        Should.Throw<CryptographicException>(() => SigningPublicKey.FromBase64(Convert.ToBase64String(p384.ExportSubjectPublicKeyInfo())));
        Should.Throw<FormatException>(() => SigningPublicKey.FromBase64("not base64!"));
        Should.Throw<CryptographicException>(() => SigningPublicKey.FromBase64(trailing));
        Should.Throw<ArgumentNullException>(() => SigningPublicKey.FromBase64(null!));
    }

    /// <summary>
    /// A signature of the private key verifies; one of another key, or of other data, does not
    /// </summary>
    [Fact]
    public void OnlyTheSignatureOfTheKeyVerifies()
    {
        // Arranging
        using ECDsa key = SecureChannelTestKeys.CreateSigningKey();
        using ECDsa other = SecureChannelTestKeys.CreateSigningKey();
        byte[] data = "data"u8.ToArray();
        byte[] signature = key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        byte[] otherSignature = other.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        SigningPublicKey publicKey = SigningPublicKey.FromKey(key);

        // Checks
        publicKey.Verify(data, signature).ShouldBeTrue();
        publicKey.Verify(data, otherSignature).ShouldBeFalse();
        publicKey.Verify("other"u8, signature).ShouldBeFalse();
        publicKey.Verify(data, new byte[P256.SignatureLength]).ShouldBeFalse();
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
