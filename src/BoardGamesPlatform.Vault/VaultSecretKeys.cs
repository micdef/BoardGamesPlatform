/*
 *	@file	src/BoardGamesPlatform.Vault/VaultSecretKeys.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	The secrets a tier reads, which must come from its vault on a server
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
/// List of the configuration keys of the secrets of a tier
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the check of the sources of the secrets looks at the secrets the tier reads, and only at them: the hosts of .NET read every
/// environment variable, without prefix, so a rule on the names of the keys alone would refuse a variable of the machine that has nothing to do
/// with the platform (a GITHUB_TOKEN, a token of a tool).</para>
/// <para><b>Behavior:</b> registered by <see cref="VaultExtensions.AddPlatformVault"/> with the keys the tier gives, read by
/// <see cref="VaultOptionsValidator"/>.</para>
/// </remarks>
public sealed class VaultSecretKeys
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
    /// Gets the configuration keys of the secrets of the tier ("Tls:CertificatePassword"...)
    /// </summary>
    public IReadOnlyList<string> Keys { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to list the secrets of a tier
    /// </summary>
    /// <param name="keys">[Cannot be null] Configuration keys of the secrets</param>
    /// <exception cref="ArgumentNullException">If the keys are null</exception>
    public VaultSecretKeys(IEnumerable<string> keys)
    {
        // Check
        ArgumentNullException.ThrowIfNull(keys);

        // Setting
        Keys = keys.ToArray();
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
