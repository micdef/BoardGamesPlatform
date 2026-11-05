/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HelloRefusedMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Answer of the server to a refused hello: the code of the refusal, before the server closes the connection
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
/// Refusal of the hello by the server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> tells the client why the connection cannot go on (for example no common version), so that the operator reads a code
/// in the logs instead of a bare close.</para>
/// <para><b>Behavior:</b> written as <c>{"control":"helloRefused","code":"protocol.versionUnsupported"}</c>; the code is required when it is
/// read and must be well-formed (see <see cref="Errors.ErrorCodes"/>). The server closes the connection after it.</para>
/// </remarks>
public sealed class HelloRefusedMessage : ControlMessage
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
    /// Code of the refusal
    /// </summary>
    public string Code { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="code">[Cannot be null] Code of the refusal</param>
    public HelloRefusedMessage(string code)
        : this(ControlKinds.HelloRefused, code)
    {
    }

    /// <summary>
    /// Constructor used by the JSON reader, which then requires every property
    /// </summary>
    /// <param name="control">[Cannot be null] Kind written in the message; checked by the serializer</param>
    /// <param name="code">[Cannot be null] Code of the refusal</param>
    [JsonConstructor]
    public HelloRefusedMessage(string control, string code)
        : base(control)
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
