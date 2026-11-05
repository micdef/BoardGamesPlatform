/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/ResponseError.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	JSON error part of a response envelope: the code that replaces the body
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
/// Error part of a response envelope
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> carries why a request has no answer, as a code only (see <see cref="Errors.ErrorCodes"/>), never as a text.</para>
/// <para><b>Behavior:</b> written as <c>{"code":"…"}</c>; the code is required when it is read, and <see cref="MessageSerializer"/> checks
/// its shape.</para>
/// </remarks>
internal sealed class ResponseError
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
    /// Error code, for example "request.unknownOperation"
    /// </summary>
    public string Code { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, which then requires the code
    /// </summary>
    /// <param name="code">[Cannot be null] Error code</param>
    [JsonConstructor]
    public ResponseError(string code)
    {
        // Setting
        Code = code;
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
