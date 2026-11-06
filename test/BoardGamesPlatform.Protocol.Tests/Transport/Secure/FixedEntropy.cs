/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/FixedEntropy.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fixed nonce and ephemeral key of a handshake, for the test vectors and the replay tests
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
/// Fixed source of the random values of a handshake, for the test vectors and for the replay tests
/// </summary>
internal sealed class FixedEntropy : HandshakeEntropy
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Nonce given each time
    /// </summary>
    private readonly byte[] _nonce;

    /// <summary>
    /// Private scalar of the ephemeral key given each time
    /// </summary>
    private readonly byte[] _privateScalar;

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
    /// <param name="nonce">[Cannot be null] Nonce of 32 bytes</param>
    /// <param name="privateScalar">[Cannot be null] Private scalar of the ephemeral key</param>
    public FixedEntropy(byte[] nonce, byte[] privateScalar)
    {
        // Setting
        _nonce = nonce;
        _privateScalar = privateScalar;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    // #N/A

    #endregion

    #region Publics

    /// <inheritdoc/>
    public override byte[] CreateNonce()
    {
        // Function's return
        return (byte[])_nonce.Clone();
    }

    /// <inheritdoc/>
    public override ECDiffieHellman CreateEphemeralKey()
    {
        // Rebuilding the key from its scalar, the system computing its point
        ECDiffieHellman key = ECDiffieHellman.Create();
        key.ImportParameters(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = (byte[])_privateScalar.Clone() });

        // Function's return
        return key;
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
