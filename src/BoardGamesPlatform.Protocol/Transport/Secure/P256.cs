/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/P256.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks of the public values of the curve NIST P-256: points and signatures, done by the channel itself
 */

// Using
using System.Numerics;
using System.Security.Cryptography;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Constants and checks of the curve NIST P-256, used by the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the documentation of .NET does not say that the import of a public key checks that its point is on the curve, on
/// Windows (CNG) or on Linux (OpenSSL); a point off the curve is the base of the invalid curve attacks. The channel therefore checks every
/// point it receives itself, before any import, and every signature value.</para>
/// <para><b>Behavior:</b> a point is the uncompressed form, <c>0x04</c> then <c>x</c> and <c>y</c> on 32 bytes each; it is valid when both
/// coordinates are below the prime of the curve and <c>y² = x³ − 3x + b (mod p)</c>. The point at infinity has no uncompressed form, and
/// P-256 has a cofactor of 1, so a point on the curve is in the group. A signature (IEEE P1363, <c>r</c> then <c>s</c>) is valid in form when
/// both values are in [1, n − 1]. The constants are those of FIPS 186-4; a test compares them with the curve .NET exports.</para>
/// </remarks>
internal static class P256
{
    #region Members

    #region Constants

    /// <summary>
    /// Size of a coordinate, and of each half of a signature, in bytes
    /// </summary>
    public const int CoordinateLength = 32;

    /// <summary>
    /// Size of a point in uncompressed form, in bytes
    /// </summary>
    public const int PointLength = 1 + (2 * CoordinateLength);

    /// <summary>
    /// Size of a signature in the IEEE P1363 format, in bytes
    /// </summary>
    public const int SignatureLength = 2 * CoordinateLength;

    /// <summary>
    /// First byte of a point in uncompressed form
    /// </summary>
    public const byte UncompressedPrefix = 0x04;

    /// <summary>
    /// Object identifier of the curve
    /// </summary>
    public const string Oid = "1.2.840.10045.3.1.7";

    /// <summary>
    /// Prime of the field: 2^256 − 2^224 + 2^192 + 2^96 − 1
    /// </summary>
    public static readonly BigInteger Prime = Parse("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF");

    /// <summary>
    /// Coefficient b of the equation of the curve
    /// </summary>
    public static readonly BigInteger B = Parse("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");

    /// <summary>
    /// Order of the group
    /// </summary>
    public static readonly BigInteger Order = Parse("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");

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
    /// Method used to read a constant written in hexadecimal
    /// </summary>
    /// <param name="hex">[Cannot be null] Big-endian hexadecimal digits</param>
    /// <returns>
    /// The positive number
    /// </returns>
    private static BigInteger Parse(string hex)
    {
        // Function's return: unsigned, big-endian
        return new BigInteger(Convert.FromHexString(hex), isUnsigned: true, isBigEndian: true);
    }

    /// <summary>
    /// Method used to read a big-endian unsigned number
    /// </summary>
    /// <param name="bytes">Bytes of the number</param>
    /// <returns>
    /// The positive number
    /// </returns>
    private static BigInteger Read(ReadOnlySpan<byte> bytes)
    {
        // Function's return
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to tell whether a point in uncompressed form is a valid point of the curve
    /// </summary>
    /// <param name="point">Bytes of the point, as received</param>
    /// <returns>
    /// True when it has the uncompressed form, both coordinates are below the prime, and it is on the curve
    /// </returns>
    public static bool IsValidPoint(ReadOnlySpan<byte> point)
    {
        // Checking the form, then the coordinates
        if (point.Length != PointLength || point[0] != UncompressedPrefix)
            return false;
        BigInteger x = Read(point.Slice(1, CoordinateLength));
        BigInteger y = Read(point.Slice(1 + CoordinateLength, CoordinateLength));
        if (x >= Prime || y >= Prime)
            return false;

        // Checking the equation y² = x³ − 3x + b (mod p), every term reduced into [0, p)
        BigInteger left = BigInteger.ModPow(y, 2, Prime);
        BigInteger right = (BigInteger.ModPow(x, 3, Prime) - (3 * x) + B) % Prime;
        if (right.Sign < 0)
            right += Prime;

        // Function's return
        return left == right;
    }

    /// <summary>
    /// Method used to tell whether a signature in the IEEE P1363 format has values in range
    /// </summary>
    /// <param name="signature">Bytes of the signature, as received</param>
    /// <returns>
    /// True when it has 64 bytes and both r and s are in [1, n − 1]
    /// </returns>
    public static bool IsValidSignature(ReadOnlySpan<byte> signature)
    {
        // Checking the size, then each half
        if (signature.Length != SignatureLength)
            return false;
        BigInteger r = Read(signature[..CoordinateLength]);
        BigInteger s = Read(signature[CoordinateLength..]);

        // Function's return
        return r.Sign > 0 && r < Order && s.Sign > 0 && s < Order;
    }

    /// <summary>
    /// Method used to recognize the curve NIST P-256, by its object identifier or, when the system gives only a name, by that name
    /// </summary>
    /// <param name="curve">Curve of a key</param>
    /// <returns>
    /// True when the curve is named and is NIST P-256; false for explicit parameters, even of the same curve
    /// </returns>
    public static bool IsCurve(ECCurve curve)
    {
        // Function's return: a named curve, P-256 under any of its names
        return curve.IsNamed && (curve.Oid.Value == Oid || curve.Oid.FriendlyName is "nistP256" or "ECDSA_P256" or "ECDH_P256" or "secp256r1" or "prime256v1");
    }

    /// <summary>
    /// Method used to write the public point of a key in uncompressed form
    /// </summary>
    /// <param name="parameters">Parameters of the key, of the curve P-256</param>
    /// <returns>
    /// The 65 bytes of the point
    /// </returns>
    /// <exception cref="CryptographicException">If a coordinate does not have 32 bytes</exception>
    public static byte[] WritePoint(ECParameters parameters)
    {
        // Check
        if (parameters.Q.X is not { Length: CoordinateLength } || parameters.Q.Y is not { Length: CoordinateLength })
            throw new CryptographicException("The point of the key does not have the size of the curve P-256");

        // Joining the prefix and the coordinates
        byte[] point = new byte[PointLength];
        point[0] = UncompressedPrefix;
        parameters.Q.X.CopyTo(point, 1);
        parameters.Q.Y.CopyTo(point, 1 + CoordinateLength);

        // Function's return
        return point;
    }

    /// <summary>
    /// Method used to build the public parameters of a point already checked
    /// </summary>
    /// <param name="point">Bytes of a point checked by <see cref="IsValidPoint"/></param>
    /// <returns>
    /// The parameters of the named curve P-256 with that point
    /// </returns>
    public static ECParameters ReadPoint(ReadOnlySpan<byte> point)
    {
        // Function's return
        return new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint
            {
                X = point.Slice(1, CoordinateLength).ToArray(),
                Y = point.Slice(1 + CoordinateLength, CoordinateLength).ToArray()
            }
        };
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
