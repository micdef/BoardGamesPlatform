/*
 *	@file	src/BoardGamesPlatform.Vault/VaultConfigurationSource.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Configuration source of the vault: the secrets of the tier, under their usual configuration keys
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
/// Configuration source used to add the secrets of the vault to the configuration of a tier
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the code that reads a secret (the options of TLS, of the handshake, of the connection string) and its validation at
/// startup do not change: the vault gives the same keys as the user secrets or the environment variables did.</para>
/// <para><b>Behavior:</b> builds a <see cref="VaultConfigurationProvider"/> over its store, which reads the vault once, when the configuration
/// is loaded.</para>
/// </remarks>
public sealed class VaultConfigurationSource : IConfigurationSource
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
    /// Configuration keys of the secrets the tier knows
    /// </summary>
    private readonly string[] _secretKeys;

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
    /// Constructor used to read the secrets of a tier from a store
    /// </summary>
    /// <param name="store">[Cannot be null] Store of the vault</param>
    /// <param name="secretKeys">[Cannot be null] Configuration keys of the secrets the tier knows ("Tls:CertificatePassword"...)</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public VaultConfigurationSource(IVaultStore store, IEnumerable<string> secretKeys)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(secretKeys);

        // Setting
        _store = store;
        _secretKeys = secretKeys.ToArray();
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
    /// Method used to build the provider of the vault
    /// </summary>
    /// <param name="builder">[Cannot be null] Builder of the configuration (unused: the vault depends on nothing else)</param>
    /// <returns>
    /// The provider
    /// </returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        // Function's return
        return new VaultConfigurationProvider(_store, _secretKeys);
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
