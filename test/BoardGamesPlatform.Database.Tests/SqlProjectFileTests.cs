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
    /// Method used to read the files declared in the project file: the objects (Build) and the scripts run before or after the publish (PreDeploy, PostDeploy)
    /// </summary>
    /// <returns>
    /// The Include values of those items, as written in the project file
    /// </returns>
    private static List<string> ReadBuildItems()
    {
        // Reading the project file (MSBuild namespace of the original format)
        XDocument project = XDocument.Load(Path.Combine(RepositoryPaths.GetRoot(), ProjectFolder, "BoardGamesPlatform.Database.sqlproj"));
        XNamespace msbuild = "http://schemas.microsoft.com/developer/msbuild/2003";

        // Function's return: the objects (Build) and the scripts run before or after the publish (PreDeploy, PostDeploy)
        return project.Descendants()
            .Where(item => item.Name == msbuild + "Build" || item.Name == msbuild + "PreDeploy" || item.Name == msbuild + "PostDeploy")
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
    /// Every stored procedure documents its contract, lives in StoredProcedures\&lt;Schema&gt;\&lt;Table&gt;\, and is named SP_&lt;Schema&gt;_…
    /// after its file, declared in its own module (internal procedures, ServerCore) or in StorProc (public procedures)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> as in the reference project of the author, a public procedure is named after the table it writes
    /// ([StorProc].[SP_Platform_Settings_Insert] in StoredProcedures\Platform\Settings), not after the schema StorProc.</para>
    /// </remarks>
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
            // Cut at "\", the separator of the project (SSDT), on every system
            string[] parts = file.Split('\\');
            string folderSchema = parts[1];
            string fileName = Path.GetFileNameWithoutExtension(parts[^1]);
            if (!declaration.Success)
            {
                problems.Add($"{file}: no CREATE PROCEDURE [Schema].[Name]");
            }
            else if ((declaration.Groups["schema"].Value != folderSchema && declaration.Groups["schema"].Value != "StorProc")
                || declaration.Groups["name"].Value != fileName
                || !fileName.StartsWith($"SP_{folderSchema}_", StringComparison.Ordinal))
            {
                problems.Add($"{file}: declared as [{declaration.Groups["schema"].Value}].[{declaration.Groups["name"].Value}], expected [{folderSchema}] or [StorProc], and [SP_{folderSchema}_…] named after the file");
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
    /// Every group of procedures (StoredProcedures\&lt;Schema&gt;\&lt;Table&gt;) has its test class, &lt;Schema&gt;\&lt;Table&gt;ProceduresTests.cs, and
    /// every procedure of the group is named in that class
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the test strategy of version 0.7.0: one test class per group of procedures, and a new procedure without a test fails.
    /// A procedure added to an existing group is caught too: its name must appear, as a whole word, in the class that tests it.</para>
    /// </remarks>
    [Fact]
    public void EveryGroupOfProceduresHasItsTestClass()
    {
        // Listing the procedures by group, then the missing test classes and the procedures their class never names
        string testFolder = Path.Combine(RepositoryPaths.GetRoot(), "test", "BoardGamesPlatform.Database.Tests");
        List<string[]> procedures = FindSqlFiles()
            .Where(file => file.StartsWith("StoredProcedures\\", StringComparison.OrdinalIgnoreCase))
            .Select(file => file.Split('\\'))
            .ToList();
        List<string> problems = new List<string>();
        foreach (IGrouping<string, string[]> group in procedures.GroupBy(parts => $"{parts[1]}\\{parts[2]}", StringComparer.Ordinal))
        {
            string[] first = group.First();
            string testClass = Path.Combine(testFolder, first[1], $"{first[2]}ProceduresTests.cs");
            if (!File.Exists(testClass))
            {
                problems.Add($"{group.Key}: no test class test/BoardGamesPlatform.Database.Tests/{first[1]}/{first[2]}ProceduresTests.cs");
                continue;
            }

            string source = File.ReadAllText(testClass);
            problems.AddRange(group
                .Select(parts => Path.GetFileNameWithoutExtension(parts[^1]))
                .Where(name => !Regex.IsMatch(source, $@"\b{Regex.Escape(name)}\b", RegexOptions.CultureInvariant))
                .Select(name => $"{group.Key}: {name} is never named in {first[2]}ProceduresTests.cs"));
        }

        // Checks
        procedures.ShouldNotBeEmpty("No procedure was found: the check reads nothing");
        problems.ShouldBeEmpty("These procedures have no test class, or are not tested by the class of their group");
    }

    /// <summary>
    /// Every table-valued type, view, trigger and function lives in its folder and is named after its file, with the prefix and the schema of
    /// the SQL conventions
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the conventions of doc/dev/database.md name every object after its schema and table, so that a name says where the
    /// object lives and what it serves: TVP_&lt;Schema&gt;_&lt;Table&gt;_… in Types, V_&lt;Schema&gt;_&lt;Table&gt; in Views,
    /// TR_&lt;Schema&gt;_&lt;Table&gt;_… in the schema of its table, a function in Functions.</para>
    /// </remarks>
    [Fact]
    public void EveryTypeViewTriggerAndFunctionFollowsTheNaming()
    {
        // Checking every file of the four folders against its declaration
        List<string> problems = new List<string>();
        List<string> files = FindSqlFiles()
            .Where(file => file.StartsWith("Types\\", StringComparison.OrdinalIgnoreCase) || file.StartsWith("Views\\", StringComparison.OrdinalIgnoreCase)
                || file.StartsWith("Triggers\\", StringComparison.OrdinalIgnoreCase) || file.StartsWith("Functions\\", StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (string file in files)
        {
            // Cut at "\", the separator of the project (SSDT), on every system
            string[] parts = file.Split('\\');
            string name = Path.GetFileNameWithoutExtension(parts[^1]);
            (string Keyword, string Schema, string Prefix, int Depth) expected = parts[0] switch
            {
                "Types" => ("TYPE", "Types", $"TVP_{parts[1]}_{(parts.Length > 3 ? parts[2] : string.Empty)}_", 4),
                "Views" => ("VIEW", "Views", $"V_{parts[1]}_", 3),
                "Triggers" => ("TRIGGER", parts[1], $"TR_{parts[1]}_{(parts.Length > 3 ? parts[2] : string.Empty)}_", 4),
                _ => ("FUNCTION", "Functions", string.Empty, 2),
            };
            Match declaration = Regex.Match(ReadSqlFile(file), $@"^CREATE\s+{expected.Keyword}\s+\[(?<schema>[^\]]+)\]\.\[(?<name>[^\]]+)\]",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline);
            if (parts.Length != expected.Depth)
                problems.Add($"{file}: expected at the depth {parts[0]}{(expected.Depth > 2 ? "\\<Schema>" : string.Empty)}{(expected.Depth > 3 ? "\\<Table>" : string.Empty)}\\<Name>.sql");
            else if (!declaration.Success)
                problems.Add($"{file}: no CREATE {expected.Keyword} [Schema].[Name]");
            else if (declaration.Groups["schema"].Value != expected.Schema || declaration.Groups["name"].Value != name || !name.StartsWith(expected.Prefix, StringComparison.Ordinal))
                problems.Add($"{file}: declared as [{declaration.Groups["schema"].Value}].[{declaration.Groups["name"].Value}], expected [{expected.Schema}].[{expected.Prefix}…] named after the file");
        }

        // Checks
        files.ShouldNotBeEmpty("No type, view, trigger or function was found: the check reads nothing");
        problems.ShouldBeEmpty("These objects break the naming of the SQL conventions (doc/dev/database.md)");
    }

    /// <summary>
    /// Every table of the modules is stored on the filegroup Core, and every temporal table keeps its history in the schema History
    /// </summary>
    [Fact]
    public void EveryTableIsOnCoreAndKeepsItsHistoryInHistory()
    {
        // Checking every file of the Tables folder
        List<string> problems = new List<string>();
        List<string> tables = FindSqlFiles().Where(file => file.StartsWith("Tables\\", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (string file in tables)
        {
            string text = ReadSqlFile(file);
            if (!text.Contains(") ON [Core]", StringComparison.Ordinal))
                problems.Add($"{file}: not stored ON [Core]");
            string[] parts = file.Split('\\');
            string expectedHistory = $"HISTORY_TABLE = [History].[{parts[1]}_{Path.GetFileNameWithoutExtension(parts[^1])}]";
            if (text.Contains("SYSTEM_VERSIONING = ON", StringComparison.Ordinal) && !text.Contains(expectedHistory, StringComparison.Ordinal))
                problems.Add($"{file}: temporal, but its history is not {expectedHistory}");
        }

        // Checks
        tables.ShouldNotBeEmpty("No table was found: the check reads nothing");
        problems.ShouldBeEmpty("These tables break the storage rules of doc/dev/database.md");
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
