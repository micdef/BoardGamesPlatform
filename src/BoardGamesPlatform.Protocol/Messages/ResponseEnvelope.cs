/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/ResponseEnvelope.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	JSON envelope of a response: its body
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Messages;

/// <summary>
/// Envelope of a response sent on a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps responses in an envelope from the start, so that the error code and the version of 0.4.0 can be added next
/// to the body without changing the body itself.</para>
/// <para><b>Behavior:</b> written as <c>{"body":{…}}</c> by <see cref="MessageSerializer"/>; the body is required when it is read.</para>
/// </remarks>
/// <typeparam name="TBody">Type of the body</typeparam>
internal sealed class ResponseEnvelope<TBody>
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
    /// Body of the response
    /// </summary>
    public TBody Body { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, which then requires the body
    /// </summary>
    /// <param name="body">Body of the response</param>
    [JsonConstructor]
    public ResponseEnvelope(TBody body)
    {
        // Setting
        Body = body;
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
