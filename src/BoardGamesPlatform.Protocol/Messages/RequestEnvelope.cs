/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/RequestEnvelope.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	JSON envelope of a request: the name of the operation and its body
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
/// Envelope of a request sent on a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> tells the receiver which operation is asked, so that one connection can carry every operation of the link.</para>
/// <para><b>Behavior:</b> written as <c>{"operation":"&lt;name&gt;","body":{…}}</c> by <see cref="MessageSerializer"/>; both parts are
/// required when it is read.</para>
/// </remarks>
/// <typeparam name="TBody">Type of the body</typeparam>
internal sealed class RequestEnvelope<TBody>
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
    /// Name of the operation asked, for example "Ping"
    /// </summary>
    public string Operation { get; }

    /// <summary>
    /// Body of the request, read with the type of the operation
    /// </summary>
    public TBody Body { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, which then requires both parts
    /// </summary>
    /// <param name="operation">[Cannot be null] Name of the operation</param>
    /// <param name="body">Body of the request</param>
    [JsonConstructor]
    public RequestEnvelope(string operation, TBody body)
    {
        // Setting
        Operation = operation;
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
