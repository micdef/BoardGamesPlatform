/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureChannelLog.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logs of the handshakes and the records of the secure channel, shared by its listener and its connector
 */

// Using
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Log messages of the secure channel, event ids 9051 to 9055
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> a reason comes from the closed list of <see cref="SecureChannelFailure"/>, and the only values of the peer logged are
/// the fixed-size ones of the protocol (a key id, in hexadecimal); nothing the peer chose as text is ever logged.</para>
/// </remarks>
internal static partial class SecureChannelLog
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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// Method used to log a handshake refused by the server
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="level">Level of the reason</param>
    /// <param name="exception">Failure, for the details of the tier</param>
    /// <param name="remoteEndPoint">Address and port of the client</param>
    /// <param name="reason">Reason, from the closed list, or "connection failed"</param>
    /// <param name="heldBack">Number of failed handshakes not logged since the last one logged</param>
    [LoggerMessage(EventId = 9051, Message = "Connection from {RemoteEndPoint} refused by the secure channel: {Reason} ({HeldBack} other failed handshakes not logged since the last one)")]
    public static partial void ServerRefused(ILogger logger, LogLevel level, Exception exception, string remoteEndPoint, string reason, int heldBack);

    /// <summary>
    /// Method used to log a handshake refused by the client
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="level">Level of the reason</param>
    /// <param name="exception">Failure, for the details of the tier</param>
    /// <param name="host">Host connected to</param>
    /// <param name="port">Port connected to</param>
    /// <param name="reason">Reason, from the closed list, or "connection failed"</param>
    [LoggerMessage(EventId = 9052, Message = "The server at {Host}:{Port} is refused by the secure channel: {Reason}")]
    public static partial void ClientRefused(ILogger logger, LogLevel level, Exception exception, string host, int port, string reason);

    /// <summary>
    /// Method used to log a secure channel opened
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="keyId">Key id of the peer</param>
    [LoggerMessage(EventId = 9053, Level = LogLevel.Information, Message = "Secure channel opened with {RemoteEndPoint}, signing key of the peer {KeyId}")]
    public static partial void Opened(ILogger logger, string remoteEndPoint, string keyId);

    /// <summary>
    /// Method used to log a record refused: a tag that fails, a truncation, a byte after the close
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="reason">Reason, from the closed list</param>
    [LoggerMessage(EventId = 9054, Level = LogLevel.Error, Message = "A record from {RemoteEndPoint} was refused: {Reason}; the connection is closed")]
    public static partial void RecordRefused(ILogger logger, string remoteEndPoint, SecureChannelFailure reason);

    /// <summary>
    /// Method used to log the hard limit of the records of a direction reached
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9055, Level = LogLevel.Warning, Message = "The connection with {RemoteEndPoint} reached the limit of records of its keys; it is closed, and a new one makes new keys")]
    public static partial void RecordLimitReached(ILogger logger, string remoteEndPoint);

    /// <summary>
    /// Method used to log a connection that could not be closed after a refused handshake
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="exception">Failure of the close</param>
    /// <param name="host">Host connected to</param>
    /// <param name="port">Port connected to</param>
    [LoggerMessage(EventId = 9065, Level = LogLevel.Warning, Message = "The connection to {Host}:{Port} could not be closed after its refused handshake")]
    public static partial void CloseFailed(ILogger logger, Exception exception, string host, int port);

    /// <summary>
    /// Method used to log a record refused, at the level of its reason
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="reason">Reason, from the closed list</param>
    public static void RecordFailed(ILogger logger, string remoteEndPoint, SecureChannelFailure reason)
    {
        // The limit is a normal end of the keys; any other refusal is a sign of tampering or a bug
        if (reason == SecureChannelFailure.RecordLimitReached)
            RecordLimitReached(logger, remoteEndPoint);
        else
            RecordRefused(logger, remoteEndPoint, reason);
    }

    /// <summary>
    /// Method used to tell the reason and the level of a failed handshake
    /// </summary>
    /// <param name="exception">[Cannot be null] Failure</param>
    /// <returns>
    /// The category of the failure, from a closed list (the key of the pace of its logs, so that a peer cannot make it grow), the reason to
    /// log (the category, with the unknown key id when there is one), and its level
    /// </returns>
    public static (string Category, string Reason, LogLevel Level) Describe(Exception exception)
    {
        // A failure outside the channel: the keys of this tier, or the connection itself
        if (exception is not SecureChannelException secure)
        {
            return exception is SigningKeysUnusableException
                ? ("keys", "the keys of this tier cannot be used", LogLevel.Error)
                : ("connection", "connection failed", LogLevel.Warning);
        }

        // Function's return: the reasons of the specification, with their levels; a sign of tampering or of a broken setup is an error
        LogLevel level = secure.Reason switch
        {
            SecureChannelFailure.PeerKeyIsCertificateKey => LogLevel.Error,
            SecureChannelFailure.KeyConfirmationFailed => LogLevel.Error,
            SecureChannelFailure.RecordAuthenticationFailed => LogLevel.Error,
            SecureChannelFailure.NoCertificate => LogLevel.Error,
            SecureChannelFailure.PeerKeyRemoved => LogLevel.Information,
            _ => LogLevel.Warning
        };
        string category = secure.Reason.ToString();
        return (category, secure.KeyIdHex is null ? category : $"{category} (key id {secure.KeyIdHex})", level);
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
