/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/ControlMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Base of the control messages, sent on the reserved correlation identifier and never answered as requests
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
/// Base of the control messages of a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the messages that manage the connection itself (the agreement on the version, later the heartbeat and the
/// cancellation of a request) travel on the same connection as the requests, without being taken for one.</para>
/// <para><b>Behavior:</b> a control message is the payload of a frame with the reserved correlation identifier
/// <see cref="Framing.Frame.NoCorrelation"/>: a JSON object whose first property, <c>control</c>, names its kind (see
/// <see cref="ControlKinds"/>). It is written and read strictly by <see cref="Messages.MessageSerializer"/>: an unknown kind, a missing or
/// unknown property is a protocol violation, which closes the connection.</para>
/// </remarks>
public abstract class ControlMessage
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
    /// Kind of the message (see <see cref="ControlKinds"/>), always written first
    /// </summary>
    [JsonPropertyOrder(-1)]
    public string Control { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="control">[Cannot be null] Kind of the message</param>
    protected ControlMessage(string control)
    {
        // Setting
        Control = control;
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
