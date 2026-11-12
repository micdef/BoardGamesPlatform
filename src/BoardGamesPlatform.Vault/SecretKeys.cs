/*
 *	@file	src/BoardGamesPlatform.Vault/SecretKeys.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Tells which configuration keys hold a secret, and how a secret is named in the vault
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
/// Rules used to name a secret in the vault
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one naming for both systems: the name of a credential of systemd, the name of a file of the DPAPI vault.</para>
/// <para><b>Behavior:</b> a key "Section:Key" is named "Section__Key" in the vault, the form systemd and the file systems accept, which the
/// configuration maps back to the key.</para>
/// </remarks>
public static class SecretKeys
{
    #region Members

    #region Constants

    /// <summary>
    /// Separator of the segments of a configuration key
    /// </summary>
    private const string KeySeparator = ":";

    /// <summary>
    /// Separator of the segments in the name of a secret in the vault
    /// </summary>
    private const string NameSeparator = "__";

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

    /// <summary>
    /// Method used to give the name of a secret in the vault
    /// </summary>
    /// <param name="key">[Cannot be null] Configuration key ("Tls:CertificatePassword")</param>
    /// <returns>
    /// The name in the vault ("Tls__CertificatePassword")
    /// </returns>
    /// <exception cref="ArgumentNullException">If the key is null</exception>
    public static string ToVaultName(string key)
    {
        // Check
        ArgumentNullException.ThrowIfNull(key);

        // Function's return
        return key.Replace(KeySeparator, NameSeparator, StringComparison.Ordinal);
    }

    /// <summary>
    /// Method used to give the configuration key of a secret named in the vault
    /// </summary>
    /// <param name="name">[Cannot be null] Name in the vault ("Tls__CertificatePassword")</param>
    /// <returns>
    /// The configuration key ("Tls:CertificatePassword")
    /// </returns>
    /// <exception cref="ArgumentNullException">If the name is null</exception>
    public static string ToKey(string name)
    {
        // Check
        ArgumentNullException.ThrowIfNull(name);

        // Function's return
        return name.Replace(NameSeparator, KeySeparator, StringComparison.Ordinal);
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
