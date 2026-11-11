/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/ExtensionBody.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Message body with extension data, whose keys may repeat the name of a property, used by the tests of the request writer
 */

// Using
using System.Text.Json;
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Body holding one number and extension data: an extension key equal to the name of the number writes that property twice
/// </summary>
public sealed class ExtensionBody
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
    /// Number carried by the body, written as "a"
    /// </summary>
    public int A { get; }

    /// <summary>
    /// Extension data, written as properties of the body next to "a"
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; } = new Dictionary<string, JsonElement>();

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; the extension data is filled afterwards, since a parameter of the constructor is required by the strict options, and
    /// extension data cannot be required
    /// </summary>
    /// <param name="a">Number carried by the body</param>
    public ExtensionBody(int a)
    {
        // Setting
        A = a;
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
