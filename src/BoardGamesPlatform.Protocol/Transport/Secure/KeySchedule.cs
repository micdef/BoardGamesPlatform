/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/KeySchedule.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Derivation of the keys and IVs of the records from the ECDH secret, with HKDF-SHA256
 */

// Using
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Key schedule of the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the raw ECDH secret is never a key: HKDF turns it into four independent values, one key and one IV per direction,
/// bound to the whole transcript of the handshake.</para>
/// <para><b>Behavior:</b> PRK = HKDF-Extract(salt = nonceC ‖ nonceS, input = Z); each value = HKDF-Expand(PRK, info, length), where info is
/// the length (2 bytes, big-endian) ‖ the length of the label (1 byte) ‖ the label ‖ the length of the transcript hash (1 byte) ‖ the hash,
/// as the HkdfLabel of RFC 8446 (section 7.1). The PRK is erased before the method returns.</para>
/// </remarks>
internal static class KeySchedule
{
    #region Members

    #region Constants

    /// <summary>
    /// Label of the key of the records client → server
    /// </summary>
    public const string ClientToServerKeyLabel = "BGPS v1 back-veryback c2s key";

    /// <summary>
    /// Label of the key of the records server → client
    /// </summary>
    public const string ServerToClientKeyLabel = "BGPS v1 back-veryback s2c key";

    /// <summary>
    /// Label of the IV of the records client → server
    /// </summary>
    public const string ClientToServerIvLabel = "BGPS v1 back-veryback c2s iv";

    /// <summary>
    /// Label of the IV of the records server → client
    /// </summary>
    public const string ServerToClientIvLabel = "BGPS v1 back-veryback s2c iv";

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
    /// Method used to build the info of one value
    /// </summary>
    /// <param name="length">Length of the value</param>
    /// <param name="label">[Cannot be null] Label of the value, ASCII, below 256 bytes</param>
    /// <param name="transcriptHash">Hash of the transcript, below 256 bytes</param>
    /// <returns>
    /// The info given to HKDF-Expand
    /// </returns>
    internal static byte[] BuildInfo(int length, string label, ReadOnlySpan<byte> transcriptHash)
    {
        // Writing each part after its length, so that two different inputs never give the same info
        byte[] labelBytes = Encoding.ASCII.GetBytes(label);
        byte[] info = new byte[2 + 1 + labelBytes.Length + 1 + transcriptHash.Length];
        BinaryPrimitives.WriteUInt16BigEndian(info, checked((ushort)length));
        info[2] = checked((byte)labelBytes.Length);
        labelBytes.CopyTo(info, 3);
        info[3 + labelBytes.Length] = checked((byte)transcriptHash.Length);
        transcriptHash.CopyTo(info.AsSpan(4 + labelBytes.Length));

        // Function's return
        return info;
    }

    /// <summary>
    /// Method used to expand one value from the PRK
    /// </summary>
    /// <param name="prk">Pseudo-random key</param>
    /// <param name="label">[Cannot be null] Label of the value</param>
    /// <param name="transcriptHash">Hash of the transcript</param>
    /// <param name="length">Length of the value</param>
    /// <returns>
    /// The value
    /// </returns>
    private static byte[] Expand(ReadOnlySpan<byte> prk, string label, ReadOnlySpan<byte> transcriptHash, int length)
    {
        // Expanding with the info of the value
        byte[] value = new byte[length];
        HKDF.Expand(HashAlgorithmName.SHA256, prk, value, BuildInfo(length, label, transcriptHash));

        // Function's return
        return value;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to derive the secrets of the records
    /// </summary>
    /// <param name="sharedSecret">Raw ECDH secret of the two ephemeral keys</param>
    /// <param name="clientNonce">Nonce of the client</param>
    /// <param name="serverNonce">Nonce of the server</param>
    /// <param name="transcriptHash">SHA-256 of the transcript of the handshake</param>
    /// <param name="peerKey">[Cannot be null] Pinned key of the peer</param>
    /// <returns>
    /// The keys and IVs of both directions
    /// </returns>
    public static SessionSecrets Derive(ReadOnlySpan<byte> sharedSecret, ReadOnlySpan<byte> clientNonce, ReadOnlySpan<byte> serverNonce, ReadOnlySpan<byte> transcriptHash,
        SigningPublicKey peerKey)
    {
        // Extracting the PRK with both nonces as the salt
        byte[] salt = new byte[clientNonce.Length + serverNonce.Length];
        clientNonce.CopyTo(salt);
        serverNonce.CopyTo(salt.AsSpan(clientNonce.Length));
        byte[] prk = new byte[SHA256.HashSizeInBytes];
        try
        {
            HKDF.Extract(HashAlgorithmName.SHA256, sharedSecret, salt, prk);

            // Function's return: the four values, then the PRK erased
            return new SessionSecrets(
                Expand(prk, ClientToServerKeyLabel, transcriptHash, SessionSecrets.KeyLength),
                Expand(prk, ServerToClientKeyLabel, transcriptHash, SessionSecrets.KeyLength),
                Expand(prk, ClientToServerIvLabel, transcriptHash, SessionSecrets.IvLength),
                Expand(prk, ServerToClientIvLabel, transcriptHash, SessionSecrets.IvLength),
                peerKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(prk);
        }
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
