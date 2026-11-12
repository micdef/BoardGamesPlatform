/*
 *	@file	src/BoardGamesPlatform.Vault/IVaultStore.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Common entry point of the vaults of the operating systems: reads the secrets a tier knows
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
/// Interface used to read the secrets of a tier from the vault of its operating system
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the vault is native to the operating system (decided by the project lead in 0.7.0): the credentials of systemd on
/// Fedora (<see cref="SystemdCredentialsStore"/>), files encrypted with DPAPI on Windows (<see cref="DpapiVaultStore"/>), behind this one
/// entry point, so that the configuration source does not depend on the system.</para>
/// <para><b>Behavior:</b> a store reads every secret it holds, refuses one whose name the tier does not know (an unknown secret is a mistake of
/// the installation, never ignored), and refuses a value it cannot read safely, each with a <see cref="VaultException"/> that never quotes a
/// value.</para>
/// </remarks>
public interface IVaultStore
{
    #region Enumerations

    // #N/A

    #endregion

    #region Accessors/Properties

    /// <summary>
    /// Gets where the store reads its secrets (a folder), for the messages
    /// </summary>
    public string Location { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Method used to read every secret of the store
    /// </summary>
    /// <param name="knownNames">[Cannot be null] Names in the vault of the secrets the tier knows ("Tls__CertificatePassword"...), ignoring the case</param>
    /// <returns>
    /// The secrets, by name in the vault; a known secret that the store does not hold is simply absent (the validation of the settings of the
    /// tier refuses a missing secret)
    /// </returns>
    /// <exception cref="VaultException">If the store cannot be read, holds an unknown secret, or a secret it cannot read safely</exception>
    public IReadOnlyDictionary<string, string> ReadAll(IReadOnlySet<string> knownNames);

    #endregion
}
