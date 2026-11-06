/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SigningKeysUnusableException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Keys of the secure channel of this tier that cannot be used for a new connection now
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
/// Failure of the keys of this tier for a new connection: the signing key is the key of the TLS certificate, a certificate reloaded with it
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a type of its own, so that the connector tells it apart from any other failure: it refuses the link for the caller
/// (<c>link.unavailable</c>) with a message about the keys, while another error keeps its own message.</para>
/// </remarks>
public sealed class SigningKeysUnusableException : InvalidOperationException
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

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public SigningKeysUnusableException()
        : base("The signing key of the secure channel is the key of the TLS certificate of the tier: they must be distinct")
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194)
    /// </summary>
    /// <param name="message">Message of the failure</param>
    public SigningKeysUnusableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194)
    /// </summary>
    /// <param name="message">Message of the failure</param>
    /// <param name="innerException">Cause of the failure</param>
    public SigningKeysUnusableException(string message, Exception? innerException)
        : base(message, innerException)
    {
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
