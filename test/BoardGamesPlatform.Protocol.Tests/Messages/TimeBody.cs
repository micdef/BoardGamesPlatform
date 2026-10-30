/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/TimeBody.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Message body holding one required date, used by the tests of the envelopes
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
/// Body holding one required date, like the body of a Ping response
/// </summary>
public sealed class TimeBody
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
    /// Date carried by the body
    /// </summary>
    public DateTimeOffset Time { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, which then requires the date
    /// </summary>
    /// <param name="time">Date carried by the body</param>
    [JsonConstructor]
    public TimeBody(DateTimeOffset time)
    {
        // Setting
        Time = time;
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
