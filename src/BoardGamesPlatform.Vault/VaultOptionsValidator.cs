/*
 *	@file	src/BoardGamesPlatform.Vault/VaultOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks at startup that, outside Development, every secret of the tier comes from its vault
 */

// Using
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Validator used to refuse a secret that comes from another source than the vault, outside the Development environment
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> rule 1 of the vault (doc/dev/database.md, The vault): a secret left in an environment variable of any prefix (the
/// connection-string prefixes such as SQLCONNSTR_ included), in a JSON file edited on the server, or on the command line, is refused, so that
/// the vault is the only place of the secrets of a server.</para>
/// <para><b>Behavior:</b> asks every provider an administrator can fill on a server (the file providers, the environment variables, the command
/// line) for each secret of the tier (<see cref="VaultSecretKeys"/>), under the key as the provider maps it, and fails on a value that is not
/// empty. Only the secrets of the tier are looked at: the hosts of .NET read every environment variable of the machine, whose names have nothing
/// to do with the platform. The message names the key and the provider, never the value. The in-memory providers (code of the tier or of its
/// tests) are not read. In Development, nothing is refused: the user secrets are the vault of a developer machine. The folder of the vault must be
/// an absolute path.</para>
/// </remarks>
public sealed class VaultOptionsValidator : IValidateOptions<VaultOptions>
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

    /// <summary>
    /// Configuration of the tier, with all its providers
    /// </summary>
    private readonly IConfiguration _svcConfiguration;

    /// <summary>
    /// Environment of the tier
    /// </summary>
    private readonly IHostEnvironment _svcEnvironment;

    /// <summary>
    /// Secrets of the tier
    /// </summary>
    private readonly VaultSecretKeys _svcSecretKeys;

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
    /// Constructor used to check the configuration of a tier
    /// </summary>
    /// <param name="svcConfiguration">[Cannot be null] Configuration of the tier</param>
    /// <param name="svcEnvironment">[Cannot be null] Environment of the tier</param>
    /// <param name="svcSecretKeys">[Cannot be null] Secrets of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public VaultOptionsValidator(IConfiguration svcConfiguration, IHostEnvironment svcEnvironment, VaultSecretKeys svcSecretKeys)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(svcConfiguration);
        ArgumentNullException.ThrowIfNull(svcEnvironment);
        ArgumentNullException.ThrowIfNull(svcSecretKeys);

        // Setting
        _svcConfiguration = svcConfiguration;
        _svcEnvironment = svcEnvironment;
        _svcSecretKeys = svcSecretKeys;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to list the providers an administrator can fill on a server, through the chained configurations too
    /// </summary>
    /// <param name="root">[Cannot be null] Root of a configuration</param>
    /// <returns>
    /// The file, environment and command-line providers
    /// </returns>
    private static IEnumerable<IConfigurationProvider> EnumerateServerProviders(IConfigurationRoot root)
    {
        // Reading every provider, and the providers of a chained configuration
        foreach (IConfigurationProvider provider in root.Providers)
        {
            if (provider is ChainedConfigurationProvider { Configuration: IConfigurationRoot chained })
            {
                foreach (IConfigurationProvider inner in EnumerateServerProviders(chained))
                    yield return inner;
            }
            else if (provider is FileConfigurationProvider or EnvironmentVariablesConfigurationProvider or CommandLineConfigurationProvider)
            {
                yield return provider;
            }
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to check the settings of the vault and the sources of the secrets
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings of the vault</param>
    /// <returns>
    /// Success, or a failure naming each wrong setting and each secret found outside the vault, never a value
    /// </returns>
    public ValidateOptionsResult Validate(string? name, VaultOptions options)
    {
        // Checking the folder of the vault
        List<string> failures = new List<string>();
        if (!string.IsNullOrWhiteSpace(options.Path) && !System.IO.Path.IsPathFullyQualified(options.Path))
            failures.Add($"{VaultOptions.SectionName}:{nameof(VaultOptions.Path)} must be an absolute path");

        // Development: the user secrets are the vault of a developer machine
        if (_svcEnvironment.IsDevelopment())
            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);

        // Checking where the secrets come from (fail closed when the providers cannot be read)
        if (_svcConfiguration is not IConfigurationRoot root)
        {
            failures.Add("The configuration of the tier cannot be inspected: the sources of its secrets cannot be checked");
            return ValidateOptionsResult.Fail(failures);
        }
        foreach (IConfigurationProvider provider in EnumerateServerProviders(root))
        {
            foreach (string key in _svcSecretKeys.Keys)
            {
                if (provider.TryGet(key, out string? value) && !string.IsNullOrEmpty(value))
                    failures.Add($"The secret {key} is set by {provider}: outside Development, a secret comes from the vault only (see doc/adm/configuration.md)");
            }
        }

        // Function's return
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
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
