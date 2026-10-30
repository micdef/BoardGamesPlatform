/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/WebSources.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Finds the source files of the website, to check them as text
 */

// Using
using System.Xml.Linq;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// Reads the source files of the website (resources and components) from the repository
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the tests check the files themselves (keys of the resources, literal texts in the markup) and not only what is rendered.</para>
/// <para><b>Behavior:</b> walks up from the folder of the test assembly until it finds <c>BoardGamesPlatform.slnx</c>, then reads under <c>src/BoardGamesPlatform.Web</c>.</para>
/// </remarks>
internal static class WebSources
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

    /// <summary>
    /// Method used to find the folder of the website project
    /// </summary>
    /// <returns>
    /// The full path of <c>src/BoardGamesPlatform.Web</c>
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">If the root of the repository is not above the test assembly</exception>
    private static string FindWebFolder()
    {
        // Walking up until the solution file is found
        DirectoryInfo? folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, SolutionFileName)))
            folder = folder.Parent;
        if (folder is null)
            throw new DirectoryNotFoundException($"{SolutionFileName} was not found above {AppContext.BaseDirectory}");

        // Function's return
        return Path.Combine(folder.FullName, "src", "BoardGamesPlatform.Web");
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to list the .razor files of the website
    /// </summary>
    /// <returns>
    /// The full paths of every .razor file under <c>Components</c>
    /// </returns>
    public static IReadOnlyList<string> RazorFiles()
    {
        // Function's return
        return Directory.GetFiles(Path.Combine(FindWebFolder(), "Components"), "*.razor", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Method used to list the source files that may ask for a text by its key (.razor and .cs of the website)
    /// </summary>
    /// <returns>
    /// The full paths of the files, without the build folders
    /// </returns>
    public static IReadOnlyList<string> KeyUserFiles()
    {
        // Function's return
        string webFolder = FindWebFolder();
        string objFolder = Path.Combine(webFolder, "obj") + Path.DirectorySeparatorChar;
        string binFolder = Path.Combine(webFolder, "bin") + Path.DirectorySeparatorChar;
        return Directory.GetFiles(webFolder, "*.*", SearchOption.AllDirectories)
            .Where(path => (path.EndsWith(".razor", StringComparison.Ordinal) || path.EndsWith(".cs", StringComparison.Ordinal))
                && !path.StartsWith(objFolder, StringComparison.Ordinal)
                && !path.StartsWith(binFolder, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Method used to read the texts of a resource file of the website
    /// </summary>
    /// <param name="fileName">[Cannot be null] Name of the file in <c>Resources</c>, for example <c>WebTexts.fr.resx</c></param>
    /// <returns>
    /// The texts, by key
    /// </returns>
    public static IReadOnlyDictionary<string, string> ReadResources(string fileName)
    {
        // Reading every data element of the file
        XDocument document = XDocument.Load(Path.Combine(FindWebFolder(), "Resources", fileName));

        // Function's return
        return document.Root!.Elements("data").ToDictionary(
            data => (string)data.Attribute("name")!,
            data => (string?)data.Element("value") ?? string.Empty);
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
