/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/Support.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fakes of the tests of the vault: an environment of a host, and a store held in memory
 */

// Using
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Environment of a host, of which the tests only set the name
/// </summary>
/// <param name="environmentName">[Cannot be null] Name of the environment</param>
internal sealed class TestEnvironment(string environmentName) : IHostEnvironment
{
    #region Accessors/Properties

    #region Members & Properties

    /// <summary>
    /// Gets or sets the name of the environment
    /// </summary>
    public string EnvironmentName { get; set; } = environmentName;

    /// <summary>
    /// Gets or sets the name of the application
    /// </summary>
    public string ApplicationName { get; set; } = "BoardGamesPlatform.Vault.Tests";

    /// <summary>
    /// Gets or sets the content root (the folder of the tests)
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Gets or sets the files of the content root (none: the validator reads no file)
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    #endregion

    #endregion
}

/// <summary>
/// Store of a vault held in memory
/// </summary>
/// <param name="secrets">[Cannot be null] Secrets of the store, by name in the vault</param>
internal sealed class FakeStore(IReadOnlyDictionary<string, string> secrets) : IVaultStore
{
    #region Accessors/Properties

    #region Members & Properties

    /// <summary>
    /// Gets where the store reads its secrets
    /// </summary>
    public string Location => "memory";

    #endregion

    #endregion

    #region Methods

    #region Publics

    /// <summary>
    /// Method used to read every secret of the store
    /// </summary>
    /// <param name="knownNames">[Cannot be null] Names of the secrets the tier knows (unused: the fake holds known names only)</param>
    /// <returns>
    /// The secrets
    /// </returns>
    public IReadOnlyDictionary<string, string> ReadAll(IReadOnlySet<string> knownNames)
    {
        // Function's return
        return secrets;
    }

    #endregion

    #endregion
}
