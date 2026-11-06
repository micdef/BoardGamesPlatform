/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureChannelException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Failure of the secure channel, with its reason from the closed list
 */

// Using
using System.Security.Authentication;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Failure of the handshake or of a record of the secure channel
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> an <see cref="AuthenticationException"/>, as a refusal of TLS, so that the link client and the link server treat it
/// as a peer that is not the one expected (<c>link.unavailable</c> for the caller). The message names the reason only, never a byte of the
/// peer.</para>
/// </remarks>
public sealed class SecureChannelException : AuthenticationException
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

    /// <summary>
    /// Reason of the failure
    /// </summary>
    public SecureChannelFailure Reason { get; }

    /// <summary>
    /// Key id the peer named, in upper-case hexadecimal, when it is not pinned (<see cref="SecureChannelFailure.UnknownKey"/>): a fixed-size
    /// value of the protocol, useful during a rotation; null otherwise
    /// </summary>
    public string? KeyIdHex { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194); the reason is then <see cref="SecureChannelFailure.Malformed"/>
    /// </summary>
    public SecureChannelException()
        : this(SecureChannelFailure.Malformed)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194); the reason is then <see cref="SecureChannelFailure.Malformed"/>
    /// </summary>
    /// <param name="message">Message of the failure</param>
    public SecureChannelException(string message)
        : base(message)
    {
        // Setting
        Reason = SecureChannelFailure.Malformed;
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194); the reason is then <see cref="SecureChannelFailure.Malformed"/>
    /// </summary>
    /// <param name="message">Message of the failure</param>
    /// <param name="innerException">Cause of the failure</param>
    public SecureChannelException(string message, Exception? innerException)
        : base(message, innerException)
    {
        // Setting
        Reason = SecureChannelFailure.Malformed;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reason">Reason of the failure</param>
    public SecureChannelException(SecureChannelFailure reason)
        : base($"The secure channel failed: {reason}")
    {
        // Setting
        Reason = reason;
    }

    /// <summary>
    /// Constructor of a key id that is not pinned
    /// </summary>
    /// <param name="keyId">Key id the peer named, 32 bytes</param>
    public SecureChannelException(ReadOnlySpan<byte> keyId)
        : this(SecureChannelFailure.UnknownKey, Convert.ToHexString(keyId))
    {
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reason">Reason of the failure</param>
    /// <param name="keyIdHex">Key id named by the peer, in hexadecimal</param>
    private SecureChannelException(SecureChannelFailure reason, string keyIdHex)
        : base($"The secure channel failed: {reason} (key id {keyIdHex})")
    {
        // Setting
        Reason = reason;
        KeyIdHex = keyIdHex;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="reason">Reason of the failure</param>
    /// <param name="innerException">Failure of the cryptography below, kept for the logs of the tier</param>
    public SecureChannelException(SecureChannelFailure reason, Exception? innerException)
        : base($"The secure channel failed: {reason}", innerException)
    {
        // Setting
        Reason = reason;
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

    // #N/A

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
