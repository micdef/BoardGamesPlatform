/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Components/WebTextsResourcesTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the resource files of the website are complete in both languages
 */

// Using
using System.Text.RegularExpressions;
using BoardGamesPlatform.Web.Tests.Support;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Components;

/// <summary>
/// Tests used to check the resource files of the website
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a missing French key would show the English text (or the key) to a French player, and a key asked by a component
/// but absent from the files would show the key itself.</para>
/// <para><b>Behavior:</b> the tests read <c>WebTexts.resx</c> and <c>WebTexts.fr.resx</c> as XML, and scan the .razor and .cs files of
/// the website for the keys they ask.</para>
/// </remarks>
public sealed class WebTextsResourcesTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 30000;

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
    /// Method used to find the keys asked in the source: <c>Texts["Key"]</c> and <c>Text("Key")</c>
    /// </summary>
    /// <returns>
    /// The distinct keys
    /// </returns>
    private static List<string> KeysAskedInTheSource()
    {
        // Scanning every file
        Regex asked = new Regex("(?:Texts\\[\"|Text\\(\")(?<key>[A-Za-z0-9_]+)\"(?:\\]|\\))");
        return WebSources.KeyUserFiles()
            .SelectMany(path => asked.Matches(File.ReadAllText(path)).Select(match => match.Groups["key"].Value))
            .Distinct()
            .ToList();
    }

    #endregion

    #region Publics

    /// <summary>
    /// Both languages hold exactly the same keys
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void EveryKeyExistsInBothLanguages()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Reading both files
        IReadOnlyDictionary<string, string> english = WebSources.ReadResources("WebTexts.resx");
        IReadOnlyDictionary<string, string> french = WebSources.ReadResources("WebTexts.fr.resx");

        // Checks
        english.Count.ShouldBeGreaterThan(0);
        french.Keys.Order().ShouldBe(english.Keys.Order());
    }

    /// <summary>
    /// No text is empty, in any language
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("WebTexts.resx")]
    [InlineData("WebTexts.fr.resx")]
    public void NoTextIsEmpty(string fileName)
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks
        foreach (KeyValuePair<string, string> text in WebSources.ReadResources(fileName))
            string.IsNullOrWhiteSpace(text.Value).ShouldBeFalse($"{text.Key} is empty in {fileName}");
    }

    /// <summary>
    /// Every key asked by a component or a class exists in the default resources
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void EveryKeyAskedInTheSourceExists()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks
        List<string> asked = KeysAskedInTheSource();
        asked.Count.ShouldBeGreaterThan(0);
        IReadOnlyDictionary<string, string> english = WebSources.ReadResources("WebTexts.resx");
        foreach (string key in asked)
            english.ContainsKey(key).ShouldBeTrue($"the key {key} is asked in the source but absent from WebTexts.resx");
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
