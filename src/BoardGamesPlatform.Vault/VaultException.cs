/*
 *	@file	src/BoardGamesPlatform.Vault/VaultException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Error of a vault that is configured but cannot be used: it names the vault and the secret, never a value
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Exception used when a configured vault cannot be read safely
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a vault that is configured but unreadable stops the startup of the tier (fail closed): a missing folder, an unknown
/// secret, a secret that cannot be decrypted, a value with a control character, a file whose access list is too wide.</para>
/// <para><b>Behavior:</b> the message names the folder and the secret, never its value nor the bytes read; the cryptographic error of DPAPI, which
/// says nothing of the value, may be kept as inner exception.</para>
/// </remarks>
public sealed class VaultException : Exception
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
    /// Constructor required by the exception conventions (RCS1194)
    /// </summary>
    public VaultException()
        : base("The vault cannot be read")
    {
    }

    /// <summary>
    /// Constructor used to report why the vault cannot be read
    /// </summary>
    /// <param name="message">[Can be null] Reason, naming the vault and the secret, never a value</param>
    public VaultException(string? message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructor used to report why the vault cannot be read, with the error of the system
    /// </summary>
    /// <param name="message">[Can be null] Reason, naming the vault and the secret, never a value</param>
    /// <param name="innerException">[Can be null] Error of the system (DPAPI, the file system), which never holds the value</param>
    public VaultException(string? message, Exception? innerException)
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
