/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SigningPublicKey.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Public signing key of a tier, in one normalized form, named by its key id
 */

// Using
using System.Security.Cryptography;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Public ECDSA P-256 key that checks the signatures of a tier in the handshake of the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one point of the curve has several encodings (compressed or not, named curve or explicit parameters); comparing the
/// encodings would let a duplicate, a reflected key or the key of a certificate pass under another form.</para>
/// <para><b>Behavior:</b> a key is imported, checked to be on the named curve P-256 with a valid point, then written again by .NET in one
/// normalized form: DER SubjectPublicKeyInfo, named curve, uncompressed point. Its key id is the SHA-256 of that form, and two keys are equal
/// when their key ids are. A key holds no private part.</para>
/// </remarks>
public sealed class SigningPublicKey : IEquatable<SigningPublicKey>
{
    #region Members

    #region Constants

    /// <summary>
    /// Size of a key id, in bytes
    /// </summary>
    public const int KeyIdLength = 32;

    #endregion

    #region Variables

    /// <summary>
    /// Normalized form of the key
    /// </summary>
    private readonly byte[] _subjectPublicKeyInfo;

    /// <summary>
    /// Point of the key in uncompressed form
    /// </summary>
    private readonly byte[] _point;

    /// <summary>
    /// SHA-256 of the normalized form
    /// </summary>
    private readonly byte[] _keyId;

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

    /// <summary>
    /// Key id: SHA-256 of the normalized form, 32 bytes
    /// </summary>
    public ReadOnlyMemory<byte> KeyId => _keyId;

    /// <summary>
    /// Key id in upper-case hexadecimal, for the logs
    /// </summary>
    public string KeyIdHex { get; }

    /// <summary>
    /// Normalized form: DER SubjectPublicKeyInfo, named curve, uncompressed point
    /// </summary>
    public ReadOnlyMemory<byte> SubjectPublicKeyInfo => _subjectPublicKeyInfo;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="point">Valid point of the key, uncompressed</param>
    private SigningPublicKey(byte[] point)
    {
        // Writing the normalized form from the point alone, then naming it
        using ECDsa key = ECDsa.Create(P256.ReadPoint(point));
        _point = point;
        _subjectPublicKeyInfo = key.ExportSubjectPublicKeyInfo();
        _keyId = SHA256.HashData(_subjectPublicKeyInfo);
        KeyIdHex = Convert.ToHexString(_keyId);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build the key from the public parameters of a key already imported
    /// </summary>
    /// <param name="parameters">Public parameters</param>
    /// <returns>
    /// The normalized key
    /// </returns>
    /// <exception cref="CryptographicException">If the curve is not the named curve P-256, or the point is not valid</exception>
    private static SigningPublicKey FromParameters(ECParameters parameters)
    {
        // Checking the curve, then the point itself, never trusting the import for it
        if (!P256.IsCurve(parameters.Curve))
            throw new CryptographicException("The key is not on the named curve P-256");
        byte[] point = P256.WritePoint(parameters);
        if (!P256.IsValidPoint(point))
            throw new CryptographicException("The point of the key is not on the curve P-256");

        // Function's return
        return new SigningPublicKey(point);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to read a public key written in Base64 of its DER SubjectPublicKeyInfo, as in a file of the public keys of a peer
    /// </summary>
    /// <param name="base64">[Cannot be null] Base64 of the key</param>
    /// <returns>
    /// The normalized key
    /// </returns>
    /// <exception cref="ArgumentNullException">If the text is null</exception>
    /// <exception cref="FormatException">If the text is not Base64</exception>
    /// <exception cref="CryptographicException">If it is not a public key of the named curve P-256 with a valid point, or holds more bytes
    /// than the key</exception>
    public static SigningPublicKey FromBase64(string base64)
    {
        // Check
        if (base64 is null)
            throw new ArgumentNullException(nameof(base64), $"The value of the parameter: {nameof(base64)} cannot be null");

        // Importing the key, the whole text being the key and nothing else
        byte[] subjectPublicKeyInfo = Convert.FromBase64String(base64.Trim());
        using ECDsa key = ECDsa.Create();
        key.ImportSubjectPublicKeyInfo(subjectPublicKeyInfo, out int bytesRead);
        if (bytesRead != subjectPublicKeyInfo.Length)
            throw new CryptographicException("The public key is followed by other bytes");

        // Function's return
        return FromParameters(key.ExportParameters(includePrivateParameters: false));
    }

    /// <summary>
    /// Method used to take the public part of a key
    /// </summary>
    /// <param name="key">[Cannot be null] Key, private or public</param>
    /// <returns>
    /// The normalized public key
    /// </returns>
    /// <exception cref="ArgumentNullException">If the key is null</exception>
    /// <exception cref="CryptographicException">If it is not a key of the named curve P-256</exception>
    public static SigningPublicKey FromKey(ECAlgorithm key)
    {
        // Check
        if (key is null)
            throw new ArgumentNullException(nameof(key), $"The value of the parameter: {nameof(key)} cannot be null");

        // Function's return
        return FromParameters(key.ExportParameters(includePrivateParameters: false));
    }

    /// <summary>
    /// Method used to write the key in Base64, one line of a file of the public keys of a peer
    /// </summary>
    /// <returns>
    /// Base64 of the normalized form
    /// </returns>
    public string ToBase64()
    {
        // Function's return
        return Convert.ToBase64String(_subjectPublicKeyInfo);
    }

    /// <summary>
    /// Method used to tell whether this key names itself with the key id given
    /// </summary>
    /// <param name="keyId">Key id received</param>
    /// <returns>
    /// True when the key ids are equal, compared in constant time
    /// </returns>
    public bool HasKeyId(ReadOnlySpan<byte> keyId)
    {
        // Function's return
        return keyId.Length == KeyIdLength && CryptographicOperations.FixedTimeEquals(keyId, _keyId);
    }

    /// <summary>
    /// Method used to check a signature made with the private part of this key
    /// </summary>
    /// <param name="data">Data signed</param>
    /// <param name="signature">Signature in the IEEE P1363 format</param>
    /// <returns>
    /// True when the signature has values in range and verifies
    /// </returns>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        // Refusing the values out of range before the check of the system
        if (!P256.IsValidSignature(signature))
            return false;
        using ECDsa key = ECDsa.Create(P256.ReadPoint(_point));

        // Function's return
        return key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    /// <summary>
    /// Method used to compare two keys
    /// </summary>
    /// <param name="other">Other key</param>
    /// <returns>
    /// True when both have the same key id
    /// </returns>
    public bool Equals(SigningPublicKey? other)
    {
        // Function's return
        return other is not null && _keyId.AsSpan().SequenceEqual(other._keyId);
    }

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        // Function's return
        return obj is SigningPublicKey other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Function's return: the first bytes of a SHA-256 are spread enough
        return BitConverter.ToInt32(_keyId, 0);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        // Function's return
        return KeyIdHex;
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
