/*
 *	@file	test/BoardGamesPlatform.Architecture.Tests/ProjectReferenceTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that every source project only references the projects the architecture allows
 */

// Using
using System.Xml.Linq;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Architecture.Tests;

/// <summary>
/// Tests used to check the project references of the solution against the validated architecture
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> makes the chain Fronts → Gateway → Back → Very Back enforced by the build, not only described: a forbidden
/// reference breaks the CI before it can be merged.</para>
/// <para><b>Behavior:</b> reads every project file of src/ and compares its project references with the list below, which is the
/// architecture validated by the project lead (see src/README.md). A new project, or a new reference, must be added to the list on purpose.
/// These checks read the project files, so they already work while the projects are still empty.</para>
/// </remarks>
public sealed class ProjectReferenceTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Prefix shared by the names of every project of the solution
    /// </summary>
    private const string ProjectPrefix = "BoardGamesPlatform.";

    #endregion

    #region Variables

    /// <summary>
    /// Projects each source project is allowed to reference (names without the "BoardGamesPlatform." prefix)
    /// </summary>
    private static readonly Dictionary<string, string[]> _allowedReferences = new Dictionary<string, string[]>
    {
        // Shared libraries: no dependency
        { "Codes", Array.Empty<string>() },
        { "Protocol", Array.Empty<string>() },
        { "Observability", Array.Empty<string>() },

        // Contracts: each one belongs to the tier that exposes it
        { "Gateway.Contracts", new string[] { "Codes" } },
        { "Back.Contracts", new string[] { "Codes" } },
        { "VeryBack.Contracts", Array.Empty<string>() },
        { "Games.Abstractions", new string[] { "Codes" } },

        // Back-end: Clean Architecture, the dependencies point to the domain
        { "Platform.Domain", new string[] { "Codes" } },
        { "Platform.Application", new string[] { "Platform.Domain", "Games.Abstractions" } },
        { "Platform.Infrastructure", new string[] { "Platform.Application", "VeryBack.Contracts", "Protocol" } },
        { "Back", new string[] { "Observability", "Platform.Application", "Platform.Infrastructure", "Back.Contracts", "Protocol" } },

        // Very Back: the only access to the database
        { "VeryBack", new string[] { "Observability", "VeryBack.Contracts", "Protocol" } },

        // Gateway: knows the fronts' contract and the Back's contract, no game
        { "Gateway", new string[] { "Observability", "Gateway.Contracts", "Back.Contracts", "Protocol" } },

        // Fronts: only the Gateway's contract
        { "Web", new string[] { "Observability", "Gateway.Contracts" } }
    };

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
    /// Method used to read the project references of every source project
    /// </summary>
    /// <returns>
    /// For each project of src/ (name without the prefix), the names of the projects it references (without the prefix)
    /// </returns>
    private static Dictionary<string, List<string>> ReadSourceProjectReferences()
    {
        // Reading every project file of src/ (one folder per project, so one level deep; bin and obj are never searched)
        Dictionary<string, List<string>> references = new Dictionary<string, List<string>>();
        IEnumerable<string> projectFiles = Directory.GetDirectories(RepositoryPaths.GetSourceFolder())
            .SelectMany(projectFolder => Directory.GetFiles(projectFolder, "*.csproj", SearchOption.TopDirectoryOnly));
        foreach (string projectFile in projectFiles)
        {
            // The paths of the project files use "\": it is turned into "/" so that the file name is also found on Linux
            List<string> referencedProjects = XDocument.Load(projectFile)
                .Descendants("ProjectReference")
                .Select(reference => ((string?)reference.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
                .Select(include => ToShortName(Path.GetFileNameWithoutExtension(include)))
                .ToList();
            references.Add(ToShortName(Path.GetFileNameWithoutExtension(projectFile)), referencedProjects);
        }

        // Function's return
        return references;
    }

    /// <summary>
    /// Method used to remove the solution prefix from a project name
    /// </summary>
    /// <param name="projectName">[Cannot be null] Full name of the project</param>
    /// <returns>
    /// The project name without "BoardGamesPlatform."
    /// </returns>
    private static string ToShortName(string projectName)
    {
        // Function's return
        return projectName.StartsWith(ProjectPrefix, StringComparison.Ordinal) ? projectName.Substring(ProjectPrefix.Length) : projectName;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Every source project has an entry in the list of allowed references
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a new project cannot escape the rules: its place in the architecture must be decided and written down.</para>
    /// </remarks>
    [Fact]
    public void EverySourceProjectHasAReferenceRule()
    {
        // Listing the projects without a rule
        List<string> projectsWithoutRule = ReadSourceProjectReferences().Keys
            .Where(project => !_allowedReferences.ContainsKey(project))
            .ToList();

        // Check
        projectsWithoutRule.ShouldBeEmpty("Each new project of src/ needs its allowed references in ProjectReferenceTests._allowedReferences");
    }

    /// <summary>
    /// Every entry of the list of allowed references is a real project of src/
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a renamed or removed project cannot leave a stale rule behind.</para>
    /// </remarks>
    [Fact]
    public void EveryReferenceRuleNamesARealProject()
    {
        // Listing the rules without a project
        Dictionary<string, List<string>> projects = ReadSourceProjectReferences();
        List<string> staleRules = _allowedReferences.Keys
            .Where(rule => !projects.ContainsKey(rule))
            .ToList();

        // Check
        staleRules.ShouldBeEmpty("These entries of ProjectReferenceTests._allowedReferences name no project of src/");
    }

    /// <summary>
    /// Every source project is analyzed by the layer rules
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the layer rules only see the assemblies copied next to the tests; a new project must also be referenced by this
    /// test project, otherwise its types would silently escape the rules.</para>
    /// </remarks>
    [Fact]
    public void EverySourceProjectIsAnalyzedByTheLayerRules()
    {
        // Listing the projects whose assembly is missing next to the tests
        List<string> projectsNotAnalyzed = ReadSourceProjectReferences().Keys
            .Where(project => !File.Exists(Path.Combine(AppContext.BaseDirectory, $"{ProjectPrefix}{project}.dll")))
            .ToList();

        // Check
        projectsNotAnalyzed.ShouldBeEmpty("Each project of src/ must be referenced by BoardGamesPlatform.Architecture.Tests.csproj");
    }

    /// <summary>
    /// Every source project only references the projects the architecture allows
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> each tier only sees its direct neighbour, and each module only depends towards its domain.</para>
    /// </remarks>
    [Fact]
    public void SourceProjectsOnlyReferenceAllowedProjects()
    {
        // Listing the forbidden references
        List<string> forbiddenReferences = new List<string>();
        foreach (KeyValuePair<string, List<string>> project in ReadSourceProjectReferences())
        {
            if (!_allowedReferences.TryGetValue(project.Key, out string[]? allowed))
                continue;
            forbiddenReferences.AddRange(project.Value
                .Where(reference => !allowed.Contains(reference))
                .Select(reference => $"{project.Key} → {reference}"));
        }

        // Check
        forbiddenReferences.ShouldBeEmpty("These references break the validated architecture (see src/README.md)");
    }

    /// <summary>
    /// A game never references another game
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> each game is an isolated module; adding or removing a game never touches another one.</para>
    /// <para><b>Behavior:</b> among the "Games.*" projects, a project named "Games.X…" may only reference the game contract and its own
    /// projects "Games.X…"; its other references are checked by the list of allowed references.</para>
    /// </remarks>
    [Fact]
    public void GamesNeverReferenceAnotherGame()
    {
        // Listing the references from a game to another game
        List<string> crossGameReferences = new List<string>();
        foreach (KeyValuePair<string, List<string>> project in ReadSourceProjectReferences())
        {
            if (!project.Key.StartsWith("Games.", StringComparison.Ordinal) || project.Key == "Games.Abstractions")
                continue;
            string game = project.Key.Split('.')[1];
            crossGameReferences.AddRange(project.Value
                .Where(reference => reference.StartsWith("Games.", StringComparison.Ordinal)
                    && reference != "Games.Abstractions"
                    && reference.Split('.')[1] != game)
                .Select(reference => $"{project.Key} → {reference}"));
        }

        // Check
        crossGameReferences.ShouldBeEmpty("A game may only reference the game contract and its own projects");
    }

    /// <summary>
    /// No project references the SQL project
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the database schema is reached only through the stored procedures called by the Very Back, by name.</para>
    /// </remarks>
    [Fact]
    public void NoProjectReferencesTheSqlProject()
    {
        // Listing the references to the SQL project
        List<string> sqlReferences = ReadSourceProjectReferences()
            .SelectMany(project => project.Value
                .Where(reference => reference == "Database")
                .Select(reference => $"{project.Key} → {reference}"))
            .ToList();

        // Check
        sqlReferences.ShouldBeEmpty("Only the Very Back reaches the database, through stored procedures");
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
