/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HeartbeatMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Control messages of the heartbeat: the ping of the client and the pong of the server
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Ping of the client or pong of the server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves to both ends that the other one is still there when no request flows, so that a dead peer or a broken network
/// path is found within a bounded time instead of leaving a connection open for nothing.</para>
/// <para><b>Behavior:</b> written as <c>{"control":"ping"}</c> or <c>{"control":"pong"}</c>, without any other property. Only the client
/// sends pings; the server answers each one with a pong.</para>
/// </remarks>
public sealed class HeartbeatMessage : ControlMessage
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Ping of the client
    /// </summary>
    public static readonly HeartbeatMessage Ping = new HeartbeatMessage(ControlKinds.Ping);

    /// <summary>
    /// Pong of the server
    /// </summary>
    public static readonly HeartbeatMessage Pong = new HeartbeatMessage(ControlKinds.Pong);

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
    /// Constructor, also used by the JSON reader
    /// </summary>
    /// <param name="control">[Cannot be null] Kind of the message, ping or pong; checked by the serializer</param>
    [JsonConstructor]
    public HeartbeatMessage(string control)
        : base(control)
    {
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
