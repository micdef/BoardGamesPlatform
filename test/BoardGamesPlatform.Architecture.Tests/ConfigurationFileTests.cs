/*
 *	@file	test/BoardGamesPlatform.Architecture.Tests/ConfigurationFileTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the committed settings files hold no secret and that only the Very Back declares a database connection
 */

// Using
using System.Text.Json;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Architecture.Tests;

/// <summary>
/// Tests used to check the committed settings files of the source projects
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> makes two rules of the configuration conventions (doc/adm/configuration.md) enforced by the CI: a secret is never
/// written in a file of the repository, and no tier other than the Very Back can even be configured to reach the database.</para>
/// <para><b>Behavior:</b> reads the appsettings*.json files of each project of src/, and its Properties/launchSettings.json (whose environment
/// variables can hold settings), and flattens them into configuration keys ("Section:Key"), as .NET does. Configuration keys are case-insensitive,
/// so are these checks. A key is a secret when one of its segments, or that segment without a final "s", ends with one of the secret names below
/// (for example "Smtp:Password", "Smtp:Passwords:0", "ConnectionStrings:Main"); its committed text value must be empty.</para>
/// </remarks>
public sealed class ConfigurationFileTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding the database connection strings
    /// </summary>
    private const string ConnectionStringsSection = "ConnectionStrings";

    /// <summary>
    /// Name of the only source project allowed to declare a database connection (the Very Back)
    /// </summary>
    private const string VeryBackProject = "BoardGamesPlatform.VeryBack";

    #endregion

    #region Variables

    /// <summary>
    /// Endings of a key segment that mark its value as a secret (for example "SmtpPassword", "ApiKey", "SessionToken", "ConnectionStrings")
    /// </summary>
    private static readonly string[] _secretKeyEndings = new string[] { "Password", "Pwd", "Secret", "ApiKey", "Token", "PrivateKey", "ConnectionString" };

    /// <summary>
    /// Reading options of the settings files, the same as the JSON configuration provider of .NET: comments and trailing commas are accepted
    /// </summary>
    private static readonly JsonDocumentOptions _jsonOptions = new JsonDocumentOptions
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
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
    /// Method used to find the appsettings*.json files of every source project
    /// </summary>
    /// <returns>
    /// The full paths of the appsettings*.json files found one level below src/ (one folder per project; bin and obj are never searched)
    /// </returns>
    private static List<string> FindAppSettingsFiles()
    {
        // Function's return
        return Directory.GetDirectories(RepositoryPaths.GetSourceFolder())
            .SelectMany(projectFolder => Directory.GetFiles(projectFolder, "appsettings*.json", SearchOption.TopDirectoryOnly))
            .ToList();
    }

    /// <summary>
    /// Method used to find every committed file that can hold settings: the appsettings*.json files and the launch settings of each project
    /// </summary>
    /// <returns>
    /// The full paths of the appsettings*.json files and of the Properties/launchSettings.json files of src/
    /// </returns>
    private static List<string> FindAllSettingsFiles()
    {
        // Setting
        List<string> settingsFiles = FindAppSettingsFiles();

        // Adding the launch settings, whose "environmentVariables" become settings of the executable when it is started from the IDE
        settingsFiles.AddRange(Directory.GetDirectories(RepositoryPaths.GetSourceFolder())
            .Select(projectFolder => Path.Combine(projectFolder, "Properties", "launchSettings.json"))
            .Where(File.Exists));

        // Function's return
        return settingsFiles;
    }

    /// <summary>
    /// Method used to read a settings file as the flat list of configuration keys .NET builds from it
    /// </summary>
    /// <param name="settingsFile">[Cannot be null] Full path of the settings file</param>
    /// <returns>
    /// Each configuration key ("Section:Key") with its JSON element
    /// </returns>
    /// <exception cref="IOException">If the file cannot be read</exception>
    /// <exception cref="InvalidDataException">If the file is not valid JSON; the message names the file</exception>
    private static List<KeyValuePair<string, JsonElement>> ReadSettings(string settingsFile)
    {
        // Setting
        List<KeyValuePair<string, JsonElement>> settings = new List<KeyValuePair<string, JsonElement>>();

        // Transforming data (the elements are cloned, so that they outlive the document)
        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsFile), _jsonOptions);
            FlattenSettings(document.RootElement.Clone(), string.Empty, settings);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{ToRepositoryPath(settingsFile)} is not valid JSON: {exception.Message}", exception);
        }

        // Function's return
        return settings;
    }

    /// <summary>
    /// Method used to turn a JSON element into configuration keys, the way the JSON configuration provider of .NET does
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> objects and arrays add a segment to the key ("Section:Key", "Section:0"); any other element is a leaf. Like the
    /// provider, an empty object or an empty array below the root is kept as a key of its own (for example "ConnectionStrings": {}).</para>
    /// </remarks>
    /// <param name="element">JSON element to flatten</param>
    /// <param name="path">[Cannot be null] Configuration key of the element; empty for the root</param>
    /// <param name="settings">[Cannot be null] List receiving the leaves</param>
    private static void FlattenSettings(JsonElement element, string path, List<KeyValuePair<string, JsonElement>> settings)
    {
        // Empty objects and arrays below the root: a key without value
        bool isContainer = element.ValueKind == JsonValueKind.Object || element.ValueKind == JsonValueKind.Array;
        bool isEmpty = element.ValueKind == JsonValueKind.Object ? !element.EnumerateObject().Any()
            : element.ValueKind == JsonValueKind.Array && element.GetArrayLength() == 0;
        if (isContainer && isEmpty && path.Length > 0)
        {
            settings.Add(new KeyValuePair<string, JsonElement>(path, element));
        }

        // Objects: one segment per property
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
                FlattenSettings(property.Value, path.Length == 0 ? property.Name : $"{path}:{property.Name}", settings);
        }

        // Arrays: one segment per index
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                FlattenSettings(item, $"{path}:{index}", settings);
                index++;
            }
        }

        // Leaves: kept with their key
        else
        {
            settings.Add(new KeyValuePair<string, JsonElement>(path, element));
        }
    }

    /// <summary>
    /// Method used to split a configuration key into its segments
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> ":" separates the segments; "__" too, the form used by environment variables (for example in launch settings).</para>
    /// </remarks>
    /// <param name="key">[Cannot be null] Configuration key</param>
    /// <returns>
    /// The segments of the key, in order
    /// </returns>
    private static string[] SplitKey(string key)
    {
        // Function's return
        return key.Replace("__", ":", StringComparison.Ordinal).Split(':');
    }

    /// <summary>
    /// Method used to tell whether a configuration key holds a secret
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> every segment is tested, not only the last one, so that a secret held in an array ("Passwords:0") or in an object
    /// ("Password:Value") is found; the indexes of arrays are skipped. A final "s" is ignored ("Tokens", "ConnectionStrings").</para>
    /// </remarks>
    /// <param name="key">[Cannot be null] Configuration key ("Section:Key")</param>
    /// <returns>
    /// True  ==> One segment of the key ends with a secret name<br/>
    /// False ==> The key is an ordinary setting
    /// </returns>
    private static bool IsSecretKey(string key)
    {
        // Function's return
        return SplitKey(key)
            .Where(segment => !segment.All(char.IsAsciiDigit))
            .Select(segment => segment.EndsWith('s') || segment.EndsWith('S') ? segment.Substring(0, segment.Length - 1) : segment)
            .Any(segment => _secretKeyEndings.Any(ending => segment.EndsWith(ending, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Method used to tell whether a JSON leaf holds a value that could be a secret
    /// </summary>
    /// <param name="value">JSON leaf</param>
    /// <returns>
    /// True  ==> The leaf is a non-empty text<br/>
    /// False ==> The leaf is empty, null, a number or a boolean (for example "RequirePassword": true)
    /// </returns>
    private static bool HasTextValue(JsonElement value)
    {
        // Function's return
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(value.GetString());
    }

    /// <summary>
    /// Method used to name a settings file in a failure message, relative to the repository
    /// </summary>
    /// <param name="settingsFile">[Cannot be null] Full path of the settings file</param>
    /// <returns>
    /// The path of the file from the root of the repository, with "/" separators
    /// </returns>
    private static string ToRepositoryPath(string settingsFile)
    {
        // Function's return
        return Path.GetRelativePath(RepositoryPaths.GetRoot(), settingsFile).Replace('\\', '/');
    }

    #endregion

    #region Publics

    /// <summary>
    /// The settings files of the source projects are found
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> positive control: if the search found no file (a moved folder, a renamed file), the other tests of this class
    /// would pass without checking anything.</para>
    /// </remarks>
    [Fact]
    public void SettingsFilesAreFound()
    {
        // Listing the settings files
        List<string> settingsFiles = FindAppSettingsFiles();

        // Check
        settingsFiles.ShouldNotBeEmpty("No appsettings*.json file was found below src/: the configuration rules check nothing");
    }

    /// <summary>
    /// No committed settings file holds a secret value
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a secret written in the repository is public forever, even after it is removed (the history keeps it). A secret key
    /// may appear in a committed file to document it, but its value stays empty: it is set by user secrets in development and by the
    /// environment of the service elsewhere (doc/adm/configuration.md).</para>
    /// </remarks>
    [Fact]
    public void CommittedSettingsHoldNoSecretValue()
    {
        // Listing the secret keys with a value
        List<string> secretsWithValue = FindAllSettingsFiles()
            .SelectMany(settingsFile => ReadSettings(settingsFile)
                .Where(setting => IsSecretKey(setting.Key) && HasTextValue(setting.Value))
                .Select(setting => $"{ToRepositoryPath(settingsFile)}: {setting.Key}"))
            .ToList();

        // Check (the message names the keys, never their values)
        secretsWithValue.ShouldBeEmpty("These secret keys have a value in a committed file; leave the value empty and set it outside the repository");
    }

    /// <summary>
    /// Only the Very Back declares database connection strings
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the Very Back is the only access to the database; no other tier can even be configured to open a connection.</para>
    /// <para><b>Behavior:</b> looks for the root "ConnectionStrings" section, the one read by the connection string helper of .NET.</para>
    /// </remarks>
    [Fact]
    public void OnlyTheVeryBackDeclaresConnectionStrings()
    {
        // Listing the files of the other projects that declare a connection string
        List<string> forbiddenFiles = FindAppSettingsFiles()
            .Where(settingsFile => Path.GetFileName(Path.GetDirectoryName(settingsFile)) != VeryBackProject)
            .Where(settingsFile => ReadSettings(settingsFile)
                .Any(setting => SplitKey(setting.Key)[0].Equals(ConnectionStringsSection, StringComparison.OrdinalIgnoreCase)))
            .Select(ToRepositoryPath)
            .ToList();

        // Check
        forbiddenFiles.ShouldBeEmpty("Only the Very Back reaches the database; remove the ConnectionStrings section from these files");
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
