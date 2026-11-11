/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/DeepBody.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Message body nested in itself as deep as asked, used by the tests of the depth limit of the request writer
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
/// Body holding the next body of a chain, so that a message reaches any depth
/// </summary>
public sealed class DeepBody
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
    /// Next body of the chain, written as "next"; null for the last one
    /// </summary>
    public DeepBody? Next { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="next">Next body of the chain; null for the last one</param>
    public DeepBody(DeepBody? next)
    {
        // Setting
        Next = next;
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

    /// <summary>
    /// Method used to build a chain of bodies
    /// </summary>
    /// <param name="depth">Bodies of the chain, at least 1</param>
    /// <returns>
    /// The first body of the chain
    /// </returns>
    public static DeepBody Chain(int depth)
    {
        // Building from the last body to the first one
        DeepBody body = new DeepBody(null);
        for (int level = 1; level < depth; level++)
            body = new DeepBody(body);

        // Function's return
        return body;
    }

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
