/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureChannelFailure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Closed list of the reasons why the secure channel closes a connection
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
/// Reason why the secure channel refuses a handshake or closes a connection, logged by the tier and never sent to the peer
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a closed list, so that a log never carries a text the peer chose, and so that the tests can name the check that
/// failed.</para>
/// </remarks>
public enum SecureChannelFailure
{
    /// <summary>
    /// A message of the handshake has a wrong magic, size, point or signature value
    /// </summary>
    Malformed,

    /// <summary>
    /// Another version or suite
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// A key id that is not pinned
    /// </summary>
    UnknownKey,

    /// <summary>
    /// The pinned key named by the peer is the key of the TLS certificate it presented
    /// </summary>
    PeerKeyIsCertificateKey,

    /// <summary>
    /// A signature that does not verify
    /// </summary>
    BadSignature,

    /// <summary>
    /// The first record received does not decrypt
    /// </summary>
    KeyConfirmationFailed,

    /// <summary>
    /// A later record whose tag fails, a truncation, a byte after the close record
    /// </summary>
    RecordAuthenticationFailed,

    /// <summary>
    /// The hard limit of records of a direction
    /// </summary>
    RecordLimitReached,

    /// <summary>
    /// The key of the peer was removed from the list of the pinned keys
    /// </summary>
    PeerKeyRemoved,

    /// <summary>
    /// The TLS session gives no certificate for one of the ends
    /// </summary>
    NoCertificate,

    /// <summary>
    /// The peer closed the connection during the handshake
    /// </summary>
    PeerClosed
}
