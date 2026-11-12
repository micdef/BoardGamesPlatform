/*
 *	@file	src/BoardGamesPlatform.Vault/VaultConfigurationProvider.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Configuration provider of the vault: reads the store once and gives its secrets under their configuration keys
 */

// Using
using Microsoft.Extensions.Configuration;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Configuration provider used to give the secrets of the vault to a tier
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the only source from which a secret may come outside the Development environment (checked by
/// <see cref="VaultOptionsValidator"/>, which recognizes this provider by its type).</para>
/// <para><b>Behavior:</b> loads once, when the configuration is built: a vault that cannot be read stops the startup with a
/// <see cref="VaultException"/>. The names of the vault ("Tls__CertificatePassword") become configuration keys ("Tls:CertificatePassword").
/// A secret is never reloaded: a change of the vault takes effect at the next start of the service.</para>
/// </remarks>
public sealed class VaultConfigurationProvider : ConfigurationProvider
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Store of the vault of the operating system
    /// </summary>
    private readonly IVaultStore _store;

    /// <summary>
    /// Names in the vault of the secrets the tier knows
    /// </summary>
    private readonly HashSet<string> _knownNames;

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
    /// Gets where the vault reads its secrets, for the messages of the tier
    /// </summary>
    public string Location => _store.Location;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to give the secrets of a store
    /// </summary>
    /// <param name="store">[Cannot be null] Store of the vault</param>
    /// <param name="secretKeys">[Cannot be null] Configuration keys of the secrets the tier knows</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public VaultConfigurationProvider(IVaultStore store, IEnumerable<string> secretKeys)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secretKeys);

        // Setting
        _store = store;
        _knownNames = new HashSet<string>(secretKeys.Select(SecretKeys.ToVaultName), StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// Method used to read the vault and give its secrets under their configuration keys
    /// </summary>
    /// <exception cref="VaultException">If the vault cannot be read safely</exception>
    public override void Load()
    {
        // Reading the store, then naming each secret by its configuration key
        Dictionary<string, string?> data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> secret in _store.ReadAll(_knownNames))
            data[SecretKeys.ToKey(secret.Key)] = secret.Value;

        // Setting
        Data = data;
    }

    /// <summary>
    /// Method used to describe the provider without any value
    /// </summary>
    /// <returns>
    /// The name of the provider and the folder of its vault
    /// </returns>
    public override string ToString()
    {
        // Function's return
        return $"{nameof(VaultConfigurationProvider)} ({_store.Location})";
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
