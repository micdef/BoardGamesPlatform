/*
 *	@file	test/BoardGamesPlatform.Database.Tests/SqlProjectFileTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the files of the SQL project against its conventions, without any SQL Server
 */

// Using
using System.Text.RegularExpressions;
using System.Xml.Linq;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests;

/// <summary>
/// Tests used to check the files of the SQL project
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the SQL project is in the original format, where every file must be declared in the project file: a file forgotten
/// there is never deployed, silently. The headers are also the documentation of every object (coding conventions, SQL section).</para>
/// <para><b>Behavior:</b> reads the .sql files of src/BoardGamesPlatform.Database (bin and obj excluded) and the Build items of the project
/// file. These tests need no SQL Server, so they run in the CI too.</para>
/// </remarks>
public sealed partial class SqlProjectFileTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Folder of the SQL project, relative to the repository
    /// </summary>
    private const string ProjectFolder = "src/BoardGamesPlatform.Database";

    #endregion

    #region Variables

    /// <summary>
    /// Sections that every stored procedure header holds, in this order
    /// </summary>
    private static readonly string[] _procedureSections = new string[] { "Parameters :", "Errors :", "Response Structure :", "Response Examples :", "Behavior :" };

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
    /// Method used to find the .sql files of the SQL project
    /// </summary>
    /// <returns>
    /// The paths of the files, relative to the project folder, with "\" separators as in the project file
    /// </returns>
    private static List<string> FindSqlFiles()
    {
        // Setting
        string projectFolder = Path.Combine(RepositoryPaths.GetRoot(), ProjectFolder);

        // Function's return
        return Directory.GetFiles(projectFolder, "*.sql", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(projectFolder, file).Replace('/', '\\'))
            .Where(file => !file.StartsWith("bin\\", StringComparison.OrdinalIgnoreCase) && !file.StartsWith("obj\\", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Method used to read the files declared as Build items in the project file
    /// </summary>
    /// <returns>
    /// The Include values of the Build items, as written in the project file
    /// </returns>
    private static List<string> ReadBuildItems()
    {
        // Reading the project file (MSBuild namespace of the original format)
        XDocument project = XDocument.Load(Path.Combine(RepositoryPaths.GetRoot(), ProjectFolder, "BoardGamesPlatform.Database.sqlproj"));
        XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

        // Function's return
        return project.Descendants(msbuild + "Build")
            .Select(item => (string?)item.Attribute("Include") ?? string.Empty)
            .ToList();
    }

    /// <summary>
    /// Method used to read a file of the SQL project
    /// </summary>
    /// <param name="relativePath">[Cannot be null] Path relative to the project folder</param>
    /// <returns>
    /// The text of the file
    /// </returns>
    private static string ReadSqlFile(string relativePath)
    {
        // Function's return
        return File.ReadAllText(Path.Combine(RepositoryPaths.GetRoot(), ProjectFolder, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// Method used to get the pattern of a procedure declaration: CREATE PROCEDURE [Schema].[SP_Schema_…]
    /// </summary>
    /// <returns>
    /// The compiled pattern, capturing the schema and the procedure name
    /// </returns>
    [GeneratedRegex(@"CREATE\s+PROCEDURE\s+\[(?<schema>[^\]]+)\]\.\[(?<name>[^\]]+)\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcedureDeclarationPattern();

    /// <summary>
    /// Method used to get the pattern of the standard error frame: BEGIN TRY, then BEGIN CATCH whose last statement is THROW
    /// </summary>
    /// <returns>
    /// The compiled pattern
    /// </returns>
    [GeneratedRegex(@"BEGIN\s+TRY[\s\S]*BEGIN\s+CATCH[\s\S]*\bTHROW\b[\s;]*END\s+CATCH", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CatchEndingWithThrowPattern();

    #endregion

    #region Publics

    /// <summary>
    /// The SQL files of the project are found
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> positive control: if the search found nothing, the other tests of this class would pass without checking anything.</para>
    /// </remarks>
    [Fact]
    public void SqlFilesAreFound()
    {
        // Check
        FindSqlFiles().ShouldNotBeEmpty("No .sql file was found in the SQL project: the conventions check nothing");
    }

    /// <summary>
    /// Every .sql file of the folder is built by the project, and every Build item of the project exists
    /// </summary>
    [Fact]
    public void EverySqlFileIsBuiltByTheProject()
    {
        // Comparing the files and the Build items
        List<string> files = FindSqlFiles();
        List<string> buildItems = ReadBuildItems();
        List<string> notBuilt = files.Where(file => !buildItems.Contains(file, StringComparer.OrdinalIgnoreCase)).ToList();
        List<string> missing = buildItems.Where(item => !files.Contains(item, StringComparer.OrdinalIgnoreCase)).ToList();

        // Checks
        notBuilt.ShouldBeEmpty("These files are not declared as <Build Include=\"…\" /> in BoardGamesPlatform.Database.sqlproj: they would never be deployed");
        missing.ShouldBeEmpty("These Build items of BoardGamesPlatform.Database.sqlproj name no file");
    }

    /// <summary>
    /// Every .sql file starts with its documentation header, which names the file itself and its author
    /// </summary>
    [Fact]
    public void EverySqlFileStartsWithItsHeader()
    {
        // Listing the files whose header is missing or names another file
        List<string> wrongHeaders = FindSqlFiles()
            .Where(file =>
            {
                string text = ReadSqlFile(file);
                string expectedFile = $"@file @prj/{ProjectFolder}/{file.Replace('\\', '/')}";
                return !text.StartsWith("/**", StringComparison.Ordinal)
                    || !text.Contains(expectedFile, StringComparison.Ordinal)
                    || !text.Contains("@author Defraene Michaël (AKA Sonic)", StringComparison.Ordinal)
                    || !text.Contains("@brief ", StringComparison.Ordinal);
            })
            .ToList();

        // Check
        wrongHeaders.ShouldBeEmpty("These files do not start with a /** */ header holding @file (their own path), @author and @brief");
    }

    /// <summary>
    /// Every stored procedure documents its contract, and is declared in its schema with the name SP_&lt;Schema&gt;_…
    /// </summary>
    [Fact]
    public void EveryProcedureDocumentsItsContractAndFollowsTheNaming()
    {
        // Checking every file of the StoredProcedures folder
        List<string> problems = new List<string>();
        foreach (string file in FindSqlFiles().Where(file => file.StartsWith("StoredProcedures\\", StringComparison.OrdinalIgnoreCase)))
        {
            string text = ReadSqlFile(file);
            problems.AddRange(_procedureSections
                .Where(section => !text.Contains(section, StringComparison.Ordinal))
                .Select(section => $"{file}: no '{section}' section"));
            List<int> sectionPositions = _procedureSections.Select(section => text.IndexOf(section, StringComparison.Ordinal)).Where(position => position >= 0).ToList();
            if (!sectionPositions.SequenceEqual(sectionPositions.Order()))
                problems.Add($"{file}: the header sections are not in the order {string.Join(", ", _procedureSections)}");

            Match declaration = ProcedureDeclarationPattern().Match(text);
            string folderSchema = file.Split('\\')[1];
            string fileName = Path.GetFileNameWithoutExtension(file);
            if (!declaration.Success)
            {
                problems.Add($"{file}: no CREATE PROCEDURE [Schema].[Name]");
            }
            else if (declaration.Groups["schema"].Value != folderSchema
                || declaration.Groups["name"].Value != fileName
                || !fileName.StartsWith($"SP_{folderSchema}_", StringComparison.Ordinal))
            {
                problems.Add($"{file}: declared as [{declaration.Groups["schema"].Value}].[{declaration.Groups["name"].Value}], expected [{folderSchema}].[SP_{folderSchema}_…] named after the file");
            }
        }

        // Check
        problems.ShouldBeEmpty("These stored procedures break the SQL conventions (doc/dev/coding-conventions.md)");
    }

    /// <summary>
    /// Every stored procedure runs with SET NOCOUNT ON and SET XACT_ABORT ON, inside a TRY block whose CATCH block ends with THROW
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the suppression of SR0013 (StaticCodeAnalysis.SuppressMessages.xml) holds only while the CATCH block of every
    /// procedure ends with THROW: an error must reach the caller, never a missing result.</para>
    /// </remarks>
    [Fact]
    public void EveryProcedureRunsInTheStandardFrame()
    {
        // Checking every file of the StoredProcedures folder
        List<string> problems = new List<string>();
        foreach (string file in FindSqlFiles().Where(file => file.StartsWith("StoredProcedures\\", StringComparison.OrdinalIgnoreCase)))
        {
            string text = ReadSqlFile(file);
            if (!text.Contains("SET NOCOUNT ON;", StringComparison.Ordinal))
                problems.Add($"{file}: no SET NOCOUNT ON");
            if (!text.Contains("SET XACT_ABORT ON;", StringComparison.Ordinal))
                problems.Add($"{file}: no SET XACT_ABORT ON");
            if (!CatchEndingWithThrowPattern().IsMatch(text))
                problems.Add($"{file}: no BEGIN TRY … BEGIN CATCH … THROW … END CATCH");
        }

        // Check
        problems.ShouldBeEmpty("These stored procedures do not run in the standard frame of the SQL conventions");
    }

    /// <summary>
    /// Every file named in StaticCodeAnalysis.SuppressMessages.xml exists, with the path form that SSDT reads
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> SSDT silently ignores a suppression whose path is wrong (a typo, slashes instead of backslashes): the suppression
    /// would then do nothing, or survive the file it was written for.</para>
    /// </remarks>
    [Fact]
    public void EverySuppressedFileExists()
    {
        // Reading the suppressed files
        XDocument suppressions = XDocument.Load(Path.Combine(RepositoryPaths.GetRoot(), ProjectFolder, "StaticCodeAnalysis.SuppressMessages.xml"));
        XNamespace analysis = "urn:Microsoft.Data.Tools.Schema.StaticCodeAnalysis";
        List<string> paths = suppressions.Descendants(analysis + "SuppressedFile").Select(file => (string?)file.Attribute("FilePath") ?? string.Empty).ToList();
        List<string> sqlFiles = FindSqlFiles();

        // Checks
        paths.ShouldNotBeEmpty("StaticCodeAnalysis.SuppressMessages.xml names no file: the check reads nothing");
        paths.Where(path => path.Contains('/', StringComparison.Ordinal)).ShouldBeEmpty("These paths use '/': SSDT only reads '\\'");
        paths.Where(path => !sqlFiles.Contains(path, StringComparer.Ordinal)).ShouldBeEmpty("These suppressed files do not exist in the SQL project");
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
