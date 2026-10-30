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
/// and <c>placeholder</c> attributes. Text with a brace or a semicolon is taken for code (the cases of a <c>switch</c>). Symbols without a
/// letter (such as the cross of the error banner) are allowed.</para>
/// <para><b>Blind spots (it is not a proof):</b> a text inside a Razor expression (<c>@("Hello")</c>), a text given to a component parameter
/// (<c>&lt;Card Title="Hello" /&gt;</c>) and a text written in the <c>@code</c> block are not seen. Two tests document this.</para>
/// <para><b>Opt-out:</b> a file that holds a legitimate text that is not translatable (a brand name, a technical identifier) carries the
/// Razor comment <c>@* i18n-ignore *@</c> and is then skipped entirely. Use it sparingly: it covers the whole file.</para>
/// </remarks>
public sealed class RazorLiteralTextTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 30000;

    /// <summary>
    /// Razor comment that excludes a whole file from the check
    /// </summary>
    private const string IgnoreMarker = "@* i18n-ignore *@";

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
        // Skipping a file that opted out
        if (source.Contains(IgnoreMarker, StringComparison.Ordinal))
            return new List<string>();

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
        {
            FindLiteralTexts(File.ReadAllText(path)).ShouldBeEmpty(
                $"{Path.GetFileName(path)} holds a literal text. Move it to Resources/WebTexts.resx and WebTexts.fr.resx and ask for it with Texts[\"Key\"], "
                + $"or, if it is not translatable, add the comment {IgnoreMarker} to the file");
        }
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

    /// <summary>
    /// The opt-out comment excludes a file
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheIgnoreMarkerExcludesTheFile()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks
        FindLiteralTexts(IgnoreMarker + "<p>Hello</p>").ShouldBeEmpty();
    }

    /// <summary>
    /// The blind spots of the heuristic: these texts are NOT caught, so a green result does not prove that no text is written in the markup
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheHeuristicDoesNotSeeTheseTexts()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Checks: a text inside a Razor expression, a text given to a component parameter, a text in the code block
        FindLiteralTexts("<p>@(\"Hello\")</p>").ShouldBeEmpty();
        FindLiteralTexts("<Card Title=\"Hello\" />").ShouldBeEmpty();
        FindLiteralTexts("<p>@Texts[\"Key\"]</p> @code { private string _text = \"Hello\"; }").ShouldBeEmpty();
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
