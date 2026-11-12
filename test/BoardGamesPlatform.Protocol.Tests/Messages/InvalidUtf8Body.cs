/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/InvalidUtf8Body.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Message body whose text is written as invalid UTF-8 by its converter, used by the tests of the message writer
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Body holding one text, written by <see cref="InvalidUtf8Converter"/> as a JSON string of invalid UTF-8
/// </summary>
public sealed class InvalidUtf8Body
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
    /// Text carried by the body, written as invalid UTF-8 whatever its value
    /// </summary>
    [JsonConverter(typeof(InvalidUtf8Converter))]
    public string Text { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="text">[Cannot be null] Text carried by the body</param>
    public InvalidUtf8Body(string text)
    {
        // Setting
        Text = text;
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
