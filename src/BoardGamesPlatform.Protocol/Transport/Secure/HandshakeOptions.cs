/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/HandshakeOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the secure channel of a tier: its signing key and the file of the public keys of its peer
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Settings of the secure channel of the Back → Very Back link, bound from the "Handshake" section
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the same section in the Back and the Very Back; checked at startup by <see cref="HandshakeOptionsValidator"/>, then
/// by the load of the keys (<see cref="SigningKeys.FromOptions"/>), so that a tier never starts without valid keys (fail closed).</para>
/// <para><b>Behavior:</b> a relative path is taken from the folder of the tier (its content root). The password is a secret: empty in the
/// committed files, given by the user secrets in development and by an environment variable of the service on the servers
/// (<c>Handshake__SigningKeyPassword</c>), distinct from the password of the PFX.</para>
/// </remarks>
public sealed class HandshakeOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section
    /// </summary>
    public const string SectionName = "Handshake";

    /// <summary>
    /// Shortest password of the signing key accepted
    /// </summary>
    public const int MinPasswordLength = 12;

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
    /// PEM file of the signing key of the tier, an encrypted PKCS#8 private key
    /// </summary>
    public string SigningKeyPath { get; set; } = string.Empty;

    /// <summary>
    /// Password of the signing key (a secret: never in a committed file)
    /// </summary>
    public string SigningKeyPassword { get; set; } = string.Empty;

    /// <summary>
    /// File of the public keys of the peer, one Base64 SubjectPublicKeyInfo per line
    /// </summary>
    public string PeerPublicKeysPath { get; set; } = string.Empty;

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

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    public override string ToString()
    {
        // Function's return: never the password
        return $"{nameof(HandshakeOptions)} {{ {nameof(SigningKeyPath)} = {SigningKeyPath}, {nameof(PeerPublicKeysPath)} = {PeerPublicKeysPath} }}";
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
