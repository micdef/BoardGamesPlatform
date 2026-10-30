/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Components/RazorLiteralTextTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that no .razor file of the website holds a visible literal text
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
/// Tests used to check that the markup of the website holds no text written in the code
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the rule of the project is that no text shown to a user is written in the code; this guards it for the markup.</para>
/// <para><b>Behavior:</b> a heuristic, not a Razor parser. It removes the Razor comments, the <c>@code</c> blocks, the script and style
/// elements and the Razor expressions, then looks for text between two tags and for literal <c>title</c>, <c>aria-label</c>, <c>alt</c>
/// and <c>placeholder</c> attributes. Text with a brace or a semicolon is taken for code (the cases of a <c>switch</c>). Symbols without a letter (such as the cross of the error banner) are allowed. It can miss a text
/// written in an unusual way, and it is not a proof.</para>
/// </remarks>
public sealed class RazorLiteralTextTests
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
    /// Method used to find the visible literal texts of a .razor file
    /// </summary>
    /// <param name="source">[Cannot be null] Content of the file</param>
    /// <returns>
    /// The literal texts found, empty when there is none
    /// </returns>
    private static List<string> FindLiteralTexts(string source)
    {
        // Removing what is not markup: Razor comments, the directives, the code block, the script and style elements, the Razor expressions
        string markup = Regex.Replace(source, @"@\*.*?\*@", string.Empty, RegexOptions.Singleline);
        markup = Regex.Replace(markup, @"^@(?:inject|using|page|layout|inherits|implements|attribute|rendermode|typeparam|namespace)\b[^\r\n]*", string.Empty, RegexOptions.Multiline);
        int codeIndex = markup.IndexOf("@code", StringComparison.Ordinal);
        if (codeIndex >= 0)
            markup = markup.Substring(0, codeIndex);
        markup = Regex.Replace(markup, @"<(script|style)\b.*?</\1>", string.Empty, RegexOptions.Singleline | RegexOptions.IgnoreCase);
        markup = Regex.Replace(markup, @"@(?:\w+(?:\.\w+)*)?(?:\([^()]*\)|\[[^\]]*\])*", string.Empty);

        // Looking for text between two tags, and for literal attributes
        List<string> found = new List<string>();
        foreach (Match match in Regex.Matches(markup, @">([^<>{};]*)<"))
        {
            if (Regex.IsMatch(match.Groups[1].Value, @"\p{L}"))
                found.Add(match.Groups[1].Value.Trim());
        }
        foreach (Match match in Regex.Matches(markup, @"\s(?:title|aria-label|alt|placeholder)=""([^""]*)"""))
        {
            if (Regex.IsMatch(match.Groups[1].Value, @"\p{L}"))
                found.Add(match.Groups[1].Value.Trim());
        }

        // Function's return
        return found;
    }

    #endregion

    #region Publics

    /// <summary>
    /// No .razor file holds a visible literal text
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void NoRazorFileHoldsAVisibleLiteralText()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks
        IReadOnlyList<string> files = WebSources.RazorFiles();
        files.Count.ShouldBeGreaterThan(0);
        foreach (string path in files)
            FindLiteralTexts(File.ReadAllText(path)).ShouldBeEmpty($"literal text in {Path.GetFileName(path)}");
    }

    /// <summary>
    /// The heuristic does catch a literal text and a literal attribute, and lets a Razor expression pass
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheHeuristicFindsLiteralTexts()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks
        FindLiteralTexts("<p>Hello</p>").ShouldHaveSingleItem().ShouldBe("Hello");
        FindLiteralTexts("<img alt=\"A cat\" />").ShouldHaveSingleItem().ShouldBe("A cat");
        FindLiteralTexts("<p>@Texts[\"Key\"]</p><span>🗙</span>").ShouldBeEmpty();
        FindLiteralTexts("<p>@Texts[\"Key\"] extra</p>").ShouldHaveSingleItem().ShouldBe("extra");
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
