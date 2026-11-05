/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/CancelMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Control message sent by the client when the caller of a request gives up: the server stops its work
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
/// Cancellation of a request
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a request abandoned by its caller (a player who closes the tab, a timeout) must stop the work it started in the next
/// tiers, instead of running to its end for nobody.</para>
/// <para><b>Behavior:</b> written as <c>{"control":"cancel","correlationId":42}</c>; the identifier is required when it is read. The server
/// cancels the token given to the handler of that request and answers nothing; a cancellation for a request that already ended, or that
/// never existed, is ignored (it crossed the response on the wire).</para>
/// </remarks>
public sealed class CancelMessage : ControlMessage
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
    /// Correlation identifier of the request to cancel
    /// </summary>
    public ulong CorrelationId { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="correlationId">Correlation identifier of the request to cancel</param>
    public CancelMessage(ulong correlationId)
        : this(ControlKinds.Cancel, correlationId)
    {
    }

    /// <summary>
    /// Constructor used by the JSON reader, which then requires every property
    /// </summary>
    /// <param name="control">[Cannot be null] Kind written in the message; checked by the serializer</param>
    /// <param name="correlationId">Correlation identifier of the request to cancel</param>
    [JsonConstructor]
    public CancelMessage(string control, ulong correlationId)
        : base(control)
    {
        // Setting
        CorrelationId = correlationId;
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
