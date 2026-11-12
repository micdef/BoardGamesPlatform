/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/FakeStore.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Store of a vault held in memory, for the tests of the configuration source
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Fake of <see cref="IVaultStore"/>, whose secrets are given by the test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> tests the configuration source and the check of the sources of the secrets without the file system of a system.</para>
/// </remarks>
internal sealed class FakeStore : IVaultStore
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Secrets of the store, by name in the vault
    /// </summary>
    private readonly IReadOnlyDictionary<string, string> _secrets;

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
    /// Gets where the store reads its secrets
    /// </summary>
    public string Location => "memory";

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to give the secrets of the store
    /// </summary>
    /// <param name="secrets">[Cannot be null] Secrets, by name in the vault</param>
    /// <exception cref="ArgumentNullException">If the secrets are null</exception>
    public FakeStore(IReadOnlyDictionary<string, string> secrets)
    {
        // Check
        ArgumentNullException.ThrowIfNull(secrets);

        // Setting
        _secrets = secrets;
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
    /// Method used to read every secret of the store
    /// </summary>
    /// <param name="knownNames">[Cannot be null] Names of the secrets the tier knows (unused: the fake holds known names only)</param>
    /// <returns>
    /// The secrets
    /// </returns>
    public IReadOnlyDictionary<string, string> ReadAll(IReadOnlySet<string> knownNames)
    {
        // Function's return
        return _secrets;
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
