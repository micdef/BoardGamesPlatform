/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SessionSecrets.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Keys and IVs of the records of one connection, derived by the handshake of the secure channel
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
/// Result of a handshake of the secure channel: the key and the IV of each direction, and the key of the peer
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> the secrets are erased when the object is disposed; the record stream disposes them once it built its ciphers.</para>
/// </remarks>
internal sealed class SessionSecrets : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Size of a key of the records, in bytes (AES-256)
    /// </summary>
    public const int KeyLength = 32;

    /// <summary>
    /// Size of an IV of the records, in bytes (the nonce of AES-GCM)
    /// </summary>
    public const int IvLength = 12;

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
    /// Key of the records client → server
    /// </summary>
    public byte[] ClientToServerKey { get; }

    /// <summary>
    /// Key of the records server → client
    /// </summary>
    public byte[] ServerToClientKey { get; }

    /// <summary>
    /// IV of the records client → server
    /// </summary>
    public byte[] ClientToServerIv { get; }

    /// <summary>
    /// IV of the records server → client
    /// </summary>
    public byte[] ServerToClientIv { get; }

    /// <summary>
    /// Pinned key the peer proved it holds
    /// </summary>
    public SigningPublicKey PeerKey { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="clientToServerKey">Key of the records client → server</param>
    /// <param name="serverToClientKey">Key of the records server → client</param>
    /// <param name="clientToServerIv">IV of the records client → server</param>
    /// <param name="serverToClientIv">IV of the records server → client</param>
    /// <param name="peerKey">Pinned key of the peer</param>
    public SessionSecrets(byte[] clientToServerKey, byte[] serverToClientKey, byte[] clientToServerIv, byte[] serverToClientIv, SigningPublicKey peerKey)
    {
        // Setting
        ClientToServerKey = clientToServerKey;
        ServerToClientKey = serverToClientKey;
        ClientToServerIv = clientToServerIv;
        ServerToClientIv = serverToClientIv;
        PeerKey = peerKey;
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

    /// <summary>
    /// Method used to erase the secrets
    /// </summary>
    public void Dispose()
    {
        // Erasing every key and IV
        CryptographicOperations.ZeroMemory(ClientToServerKey);
        CryptographicOperations.ZeroMemory(ServerToClientKey);
        CryptographicOperations.ZeroMemory(ClientToServerIv);
        CryptographicOperations.ZeroMemory(ServerToClientIv);
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
