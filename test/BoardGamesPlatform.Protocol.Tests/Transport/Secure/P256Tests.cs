/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/P256Tests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the constants of the curve P-256 and the checks of points and signatures done by the channel
 */

// Using
using System.Numerics;
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport.Secure;

// Static using
using static BoardGamesPlatform.Protocol.Tests.Transport.Secure.Vectors.SecureChannelVectors;

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Tests used to check <see cref="P256"/>
/// </summary>
public sealed class P256Tests
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
    /// Method used to write a number on 32 big-endian bytes
    /// </summary>
    /// <param name="value">Number below 2^256</param>
    /// <returns>
    /// The 32 bytes
    /// </returns>
    private static byte[] Write32(BigInteger value)
    {
        // Padding the unsigned bytes on the left
        byte[] bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        byte[] padded = new byte[P256.CoordinateLength];
        bytes.CopyTo(padded, P256.CoordinateLength - bytes.Length);

        // Function's return
        return padded;
    }

    #endregion

    #region Publics

    /// <summary>
    /// The prime, the coefficient b and the order are the ones .NET gives for the named curve
    /// </summary>
    [Fact]
    public void TheConstantsAreTheOnesOfTheSystem()
    {
        // Arranging
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Acting
        ECCurve curve = key.ExportExplicitParameters(includePrivateParameters: false).Curve;

        // Checks
        new BigInteger(curve.Prime, isUnsigned: true, isBigEndian: true).ShouldBe(P256.Prime);
        new BigInteger(curve.B, isUnsigned: true, isBigEndian: true).ShouldBe(P256.B);
        new BigInteger(curve.Order, isUnsigned: true, isBigEndian: true).ShouldBe(P256.Order);
        new BigInteger(curve.A, isUnsigned: true, isBigEndian: true).ShouldBe(P256.Prime - 3);
    }

    /// <summary>
    /// The points of keys made by OpenSSL and by .NET are valid
    /// </summary>
    [Fact]
    public void PointsOfRealKeysAreValid()
    {
        // Arranging
        using ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

        // Checks
        P256.IsValidPoint(Bytes(ClientEphemeralPoint)).ShouldBeTrue();
        P256.IsValidPoint(Bytes(ServerEphemeralPoint)).ShouldBeTrue();
        P256.IsValidPoint(P256.WritePoint(key.ExportParameters(includePrivateParameters: false))).ShouldBeTrue();
    }

    /// <summary>
    /// A point off the curve, with another first byte, a coordinate not below the prime, the zero point or another size is refused
    /// </summary>
    [Fact]
    public void InvalidPointsAreRefused()
    {
        // Arranging
        byte[] offCurve = Bytes(ClientEphemeralPoint);
        offCurve[^1] ^= 0x01;
        byte[] compressedPrefix = Bytes(ClientEphemeralPoint);
        compressedPrefix[0] = 0x02;
        byte[] xAbovePrime = [P256.UncompressedPrefix, .. Write32(P256.Prime), .. Bytes(ClientEphemeralPoint).AsSpan(33).ToArray()];
        byte[] xPlusPrime = Bytes(ClientEphemeralPoint);
        byte[] zero = new byte[P256.PointLength];
        zero[0] = P256.UncompressedPrefix;

        // Checks
        P256.IsValidPoint(offCurve).ShouldBeFalse();
        P256.IsValidPoint(compressedPrefix).ShouldBeFalse();
        P256.IsValidPoint(xAbovePrime).ShouldBeFalse();
        P256.IsValidPoint(zero).ShouldBeFalse();
        P256.IsValidPoint(xPlusPrime.AsSpan(0, P256.PointLength - 1)).ShouldBeFalse();
        P256.IsValidPoint([.. xPlusPrime, 0x00]).ShouldBeFalse();
        P256.IsValidPoint([]).ShouldBeFalse();
    }

    /// <summary>
    /// A signature with r or s out of [1, n − 1], or of another size, is refused in form
    /// </summary>
    [Fact]
    public void SignaturesOutOfRangeAreRefused()
    {
        // Arranging
        byte[] valid = Bytes(Signature);
        byte[] zeroR = [.. new byte[32], .. valid.AsSpan(32).ToArray()];
        byte[] zeroS = [.. valid.AsSpan(0, 32).ToArray(), .. new byte[32]];
        byte[] orderR = [.. Write32(P256.Order), .. valid.AsSpan(32).ToArray()];
        byte[] orderS = [.. valid.AsSpan(0, 32).ToArray(), .. Write32(P256.Order)];
        byte[] lastS = [.. valid.AsSpan(0, 32).ToArray(), .. Write32(P256.Order - 1)];

        // Checks
        P256.IsValidSignature(valid).ShouldBeTrue();
        P256.IsValidSignature(lastS).ShouldBeTrue();
        P256.IsValidSignature(zeroR).ShouldBeFalse();
        P256.IsValidSignature(zeroS).ShouldBeFalse();
        P256.IsValidSignature(orderR).ShouldBeFalse();
        P256.IsValidSignature(orderS).ShouldBeFalse();
        P256.IsValidSignature(valid.AsSpan(1)).ShouldBeFalse();
    }

    /// <summary>
    /// What the import of the system does with a point off the curve is recorded, whatever it does: the channel never relies on it
    /// </summary>
    [Fact]
    public void TheImportOfTheSystemIsNeverReliedOn()
    {
        // Arranging
        byte[] offCurve = Bytes(ClientEphemeralPoint);
        offCurve[^1] ^= 0x01;

        // Acting: the import may accept or refuse the point; the channel checks it first in any case
        bool imported;
        try
        {
            using ECDiffieHellman key = ECDiffieHellman.Create(P256.ReadPoint(offCurve));
            imported = true;
        }
        catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
        {
            // CNG refuses it with a PlatformNotSupportedException ("parameters are not valid for this platform")
            imported = false;
        }

        // Checks
        TestContext.Current.SendDiagnosticMessage($"The import of {(OperatingSystem.IsWindows() ? "CNG" : "OpenSSL")} {(imported ? "accepts" : "refuses")} a point off the curve");
        P256.IsValidPoint(offCurve).ShouldBeFalse();
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
