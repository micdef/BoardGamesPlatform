/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/TestEnvironment.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Environment of a host, of which the tests of the vault only set the name
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
/// Fake of <see cref="IHostEnvironment"/>, with a name chosen by the test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the check of the sources of the secrets depends on the environment only (Development or not); a fake of the tests avoids
/// the HostingEnvironment of the namespace Microsoft.Extensions.Hosting.Internal, which Microsoft marks as internal.</para>
/// </remarks>
internal sealed class TestEnvironment : IHostEnvironment
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
    /// Gets or sets the name of the environment
    /// </summary>
    public string EnvironmentName { get; set; }

    /// <summary>
    /// Gets or sets the name of the application
    /// </summary>
    public string ApplicationName { get; set; } = "BoardGamesPlatform.Vault.Tests";

    /// <summary>
    /// Gets or sets the content root (the folder of the tests)
    /// </summary>
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    /// <summary>
    /// Gets or sets the files of the content root (none: the code under test reads no file of it)
    /// </summary>
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to name the environment
    /// </summary>
    /// <param name="environmentName">[Cannot be null] Name of the environment (Production, Development...)</param>
    /// <exception cref="ArgumentNullException">If the name is null</exception>
    public TestEnvironment(string environmentName)
    {
        // Check
        ArgumentNullException.ThrowIfNull(environmentName);

        // Setting
        EnvironmentName = environmentName;
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
