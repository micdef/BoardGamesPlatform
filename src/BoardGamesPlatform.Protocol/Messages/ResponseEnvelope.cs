/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/ResponseEnvelope.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	JSON envelope of a response: its body, or the error code that replaces it
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
/// <para><b>Purpose:</b> lets a response carry either the answer of the operation or the reason why there is none, without changing the
/// body types.</para>
/// <para><b>Behavior:</b> written by <see cref="MessageSerializer"/> as <c>{"body":{…}}</c> or <c>{"error":{"code":"…"}}</c>, the absent part
/// being left out. Both parts are optional for the JSON reader; the serializer then requires exactly one of them.</para>
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
    /// Body of the response; null when the response is an error
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TBody? Body { get; }

    /// <summary>
    /// Error that replaces the body; null when the response has a body
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ResponseError? Error { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, for which both parts are optional
    /// </summary>
    /// <param name="body">[Can be null] Body of the response</param>
    /// <param name="error">[Can be null] Error that replaces the body</param>
    [JsonConstructor]
    public ResponseEnvelope(TBody? body = default, ResponseError? error = null)
    {
        // Setting
        Body = body;
        Error = error;
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
