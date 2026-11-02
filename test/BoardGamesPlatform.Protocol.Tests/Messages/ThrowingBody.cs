/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/ThrowingBody.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Message body of the tests that cannot be written: reading its only property fails
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Body whose writing fails
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> plays a response that the serializer cannot write, to prove that this failure is answered with a code.</para>
/// </remarks>
public sealed class ThrowingBody
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Message of the failure
    /// </summary>
    private readonly string _reason = "This body cannot be written";

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
    /// Value whose reading always fails
    /// </summary>
    /// <exception cref="InvalidOperationException">Always</exception>
    public int Value => throw new InvalidOperationException(_reason);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    // #N/A

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
