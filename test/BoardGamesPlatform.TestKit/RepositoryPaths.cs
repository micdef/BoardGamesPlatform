/*
 *	@file	test/BoardGamesPlatform.TestKit/RepositoryPaths.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Locates the repository from a running test, wherever its output folder is
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Helper used to locate the folders of the repository from a running test
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test read the files of the repository (project files, scripts…), on a developer machine as in the CI,
/// without any hard-coded path.</para>
/// <para><b>Behavior:</b> walks up from the output folder of the test until it finds the folder holding the solution file.</para>
/// </remarks>
public static class RepositoryPaths
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the solution file that marks the root of the repository
    /// </summary>
    private const string SolutionFileName = "BoardGamesPlatform.slnx";

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
    /// Method used to find the root folder of the repository
    /// </summary>
    /// <returns>
    /// The full path of the folder holding the solution file
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">If no parent folder of the test output holds the solution file</exception>
    public static string GetRoot()
    {
        // Walking up from the output folder of the test
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, SolutionFileName)))
            folder = folder.Parent;

        // Check
        if (folder is null)
            throw new DirectoryNotFoundException($"No parent folder of {AppContext.BaseDirectory} holds {SolutionFileName}");

        // Function's return
        return folder.FullName;
    }

    /// <summary>
    /// Method used to find the folder of the source projects
    /// </summary>
    /// <returns>
    /// The full path of the src folder of the repository
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">If no parent folder of the test output holds the solution file</exception>
    public static string GetSourceFolder()
    {
        // Function's return
        return Path.Combine(GetRoot(), "src");
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
