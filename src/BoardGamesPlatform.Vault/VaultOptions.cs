/*
 *	@file	src/BoardGamesPlatform.Vault/VaultOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "Vault" configuration section: where a Windows service finds its vault
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
/// Settings of the vault of a tier
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> on Windows, the folder of the vault is a setting (it is not a secret: it names a folder whose access list protects the
/// secrets). On Fedora there is no setting: systemd names the folder of the credentials in the variable CREDENTIALS_DIRECTORY.</para>
/// <para><b>Behavior:</b> bound from the section "Vault"; validated at startup by <see cref="VaultOptionsValidator"/>, which also checks where the
/// secrets of the tier come from.</para>
/// </remarks>
public sealed class VaultOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section
    /// </summary>
    public const string SectionName = "Vault";

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
    /// Gets or sets the folder of the vault of a Windows service, an absolute path [Can be null]: empty means no vault (Development, tests)
    /// </summary>
    public string? Path { get; set; }

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
