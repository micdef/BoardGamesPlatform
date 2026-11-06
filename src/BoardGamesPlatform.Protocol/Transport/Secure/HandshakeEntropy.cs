/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/HandshakeEntropy.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Source of the random values of a handshake: the nonce and the ephemeral key
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
/// Random values of one end of a handshake
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the test vectors need fixed values; production needs fresh ones. The fixed source is internal, reachable by the
/// tests only, never from the settings.</para>
/// <para><b>Behavior:</b> <see cref="System"/> draws each nonce from the generator of the system and makes a new ephemeral key of P-256 for
/// each handshake.</para>
/// </remarks>
internal class HandshakeEntropy
{
    #region Members

    #region Constants

    /// <summary>
    /// Size of a nonce, in bytes
    /// </summary>
    public const int NonceLength = 32;

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

    /// <summary>
    /// Source of the system, fresh values for each handshake
    /// </summary>
    public static HandshakeEntropy System { get; } = new HandshakeEntropy();

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
    /// Method used to draw a nonce
    /// </summary>
    /// <returns>
    /// A new nonce of <see cref="NonceLength"/> bytes
    /// </returns>
    public virtual byte[] CreateNonce()
    {
        // Function's return
        return RandomNumberGenerator.GetBytes(NonceLength);
    }

    /// <summary>
    /// Method used to make an ephemeral key
    /// </summary>
    /// <returns>
    /// A new key of P-256, owned by the caller, who disposes it
    /// </returns>
    public virtual ECDiffieHellman CreateEphemeralKey()
    {
        // Function's return
        return ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
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
