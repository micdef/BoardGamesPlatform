/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/ControlKinds.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Names of the kinds of control messages, as written in their "control" property
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Kinds of control messages
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one list of the names written on the wire, shared by the writer and the reader.</para>
/// </remarks>
public static class ControlKinds
{
    #region Members

    #region Constants

    /// <summary>
    /// First message of a connection, from the client: the versions and the limits it supports (<see cref="HelloMessage"/>)
    /// </summary>
    public const string Hello = "hello";

    /// <summary>
    /// Answer of the server to a hello it accepts: the version chosen and its limits (<see cref="HelloAckMessage"/>)
    /// </summary>
    public const string HelloAck = "helloAck";

    /// <summary>
    /// Answer of the server to a hello it refuses, with the code of the refusal; the server then closes (<see cref="HelloRefusedMessage"/>)
    /// </summary>
    public const string HelloRefused = "helloRefused";

    /// <summary>
    /// Cancellation of a request by the client, after the handshake (<see cref="CancelMessage"/>)
    /// </summary>
    public const string Cancel = "cancel";

    /// <summary>
    /// Heartbeat of the client, after the handshake (<see cref="HeartbeatMessage"/>)
    /// </summary>
    public const string Ping = "ping";

    /// <summary>
    /// Answer of the server to a ping (<see cref="HeartbeatMessage"/>)
    /// </summary>
    public const string Pong = "pong";

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
