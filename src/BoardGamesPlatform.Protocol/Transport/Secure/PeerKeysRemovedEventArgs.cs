/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/PeerKeysRemovedEventArgs.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Keys of the peer removed from the list by a reload, whose connections must be closed
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Data of the event <see cref="SigningKeys.PeerKeysRemoved"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> removing a key revokes it: the connections opened with it are closed, established ones included.</para>
/// </remarks>
public sealed class PeerKeysRemovedEventArgs : EventArgs
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
    /// Keys removed
    /// </summary>
    public IReadOnlyList<SigningPublicKey> RemovedKeys { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="removedKeys">[Cannot be null] Keys removed</param>
    public PeerKeysRemovedEventArgs(IReadOnlyList<SigningPublicKey> removedKeys)
    {
        // Setting
        RemovedKeys = removedKeys;
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
