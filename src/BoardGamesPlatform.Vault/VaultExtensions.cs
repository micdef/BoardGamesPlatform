/*
 *	@file	src/BoardGamesPlatform.Vault/VaultExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Plugs the vault of the operating system into the configuration of a tier, and checks the sources of its secrets
 */

// Using
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Extensions used to give a tier the secrets of its vault
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one single place that decides where the secrets of every tier come from (doc/dev/database.md, The vault), so that the
/// code that reads them does not change.</para>
/// <para><b>Behavior:</b> chooses the store of the system: the credentials of systemd when the variable CREDENTIALS_DIRECTORY is set (Linux),
/// the DPAPI folder of the setting Vault:Path (Windows), or none (a developer machine, the tests). The vault is added last, so that it wins over
/// every other source, and it is read at once: a vault that cannot be read stops the startup with a <see cref="VaultException"/>. The check of the
/// sources of the secrets (<see cref="VaultOptionsValidator"/>) runs when the host starts; in Development, a warning is logged instead.</para>
/// </remarks>
public static class VaultExtensions
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

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to choose the store of the vault of the system
    /// </summary>
    /// <param name="configuration">[Cannot be null] Configuration of the tier, before the vault</param>
    /// <returns>
    /// The store, or null when the tier has no vault
    /// </returns>
    /// <exception cref="VaultException">If the setting Vault:Path is given on another system than Windows</exception>
    internal static IVaultStore? ChooseStore(IConfiguration configuration)
    {
        // Reading where the vault is
        string? path = configuration[$"{VaultOptions.SectionName}:{nameof(VaultOptions.Path)}"];
        string? credentials = Environment.GetEnvironmentVariable(SystemdCredentialsStore.DirectoryVariable);

        // Windows: the DPAPI folder of the setting
        if (OperatingSystem.IsWindows())
            return string.IsNullOrWhiteSpace(path) ? null : new DpapiVaultStore(path);

        // Other systems: the credentials of systemd; the setting of Windows is refused rather than ignored
        if (!string.IsNullOrWhiteSpace(path))
            throw new VaultException($"{VaultOptions.SectionName}:{nameof(VaultOptions.Path)} is a setting of Windows: on Linux, the vault is the credentials of the systemd unit (LoadCredentialEncrypted=)");

        // Function's return
        return string.IsNullOrWhiteSpace(credentials) ? null : new SystemdCredentialsStore(credentials);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to add the vault of the system to the configuration of a tier, and to check the sources of its secrets at startup
    /// </summary>
    /// <param name="builder">[Cannot be null] Builder of the host of the tier</param>
    /// <param name="secretKeys">[Cannot be null] Configuration keys of the secrets of the tier ("Tls:CertificatePassword"...); a secret of the
    /// vault that is not in this list stops the startup</param>
    /// <returns>
    /// True  ==> a vault was found and read
    /// False ==> the tier has no vault (its secrets come from the user secrets in Development, or are missing, which its settings refuse)
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="VaultException">If the vault is configured but cannot be read safely</exception>
    public static bool AddPlatformVault(this IHostApplicationBuilder builder, params string[] secretKeys)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(secretKeys);

        // Adding the vault last, read at once
        IVaultStore? store = ChooseStore(builder.Configuration);
        if (store is not null)
            builder.Configuration.Add(new VaultConfigurationSource(store, secretKeys));

        // Checking the settings and the sources of the secrets when the host starts, or warning in Development
        builder.Services.AddSingleton(new VaultSecretKeys(secretKeys));
        builder.Services.AddOptions<VaultOptions>().Bind(builder.Configuration.GetSection(VaultOptions.SectionName)).ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<VaultOptions>, VaultOptionsValidator>();
        if (builder.Environment.IsDevelopment())
            builder.Services.AddHostedService<VaultDevelopmentWarningService>();
        else if (store is null)
            builder.Services.AddHostedService<VaultMissingWarningService>();

        // Function's return
        return store is not null;
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
