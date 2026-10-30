/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Components/TemplatePagesTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the texts of the pages and layouts that come from the Blazor template, in English and in French
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Web.Components.Layout;
using BoardGamesPlatform.Web.Components.Pages;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Components;

/// <summary>
/// Tests used to check the not found page, the error banner of the layout, the error page and the reconnection dialog
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> these texts come from the resources and follow the language of the player, and the error page never shows an exception.</para>
/// <para><b>Behavior:</b> each test renders the component in memory with bUnit, for a culture, and reads its markup.</para>
/// </remarks>
public sealed class TemplatePagesTests
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
    /// Method used to prepare a context for a player of the given culture, with the resources folder of the website
    /// </summary>
    /// <param name="context">[Cannot be null] Context to prepare</param>
    /// <param name="cultureName">[Cannot be null] Name of the culture of the player</param>
    private static void Prepare(BunitContext context, string cultureName)
    {
        // Giving the player's culture to the texts, as the localization middleware does
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Registering the localization
        context.Services.AddLocalization(options => options.ResourcesPath = "Resources");
    }

    #endregion

    #region Publics

    /// <summary>
    /// The not found page is written in the language of the player
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("en-US", "Not found", "Sorry, the content you are looking for does not exist.")]
    [InlineData("fr-FR", "Introuvable", "Désolé, le contenu que vous cherchez n'existe pas.")]
    public async Task NotFoundIsLocalizedAsync(string cultureName, string title, string message)
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering
        await using BunitContext context = new BunitContext();
        Prepare(context, cultureName);
        IRenderedComponent<NotFound> page = context.Render<NotFound>();

        // Checks
        page.Find("h1").TextContent.ShouldBe(title);
        page.Find("p").TextContent.ShouldBe(message);
    }

    /// <summary>
    /// The error banner of the layout is written in the language of the player, around the page
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("en-US", "An unhandled error has occurred.", "Reload")]
    [InlineData("fr-FR", "Une erreur inattendue s'est produite.", "Recharger")]
    public async Task TheErrorBannerIsLocalizedAsync(string cultureName, string banner, string reload)
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the layout around a page
        await using BunitContext context = new BunitContext();
        Prepare(context, cultureName);
        IRenderedComponent<MainLayout> layout = context.Render<MainLayout>(parameters => parameters.Add(component => component.Body, "<p id=\"body\">page</p>"));

        // Checks
        layout.Find("#body").TextContent.ShouldBe("page");
        layout.Find("#blazor-error-ui").TextContent.ShouldContain(banner);
        layout.Find("#blazor-error-ui a.reload").TextContent.ShouldBe(reload);
    }

    /// <summary>
    /// The error page is written in the language of the player, shows the request identifier and nothing of an exception
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("en-US", "Error.", "Request ID:")]
    [InlineData("fr-FR", "Erreur.", "Identifiant de la demande :")]
    public async Task TheErrorPageIsLocalizedAsync(string cultureName, string heading, string requestIdLabel)
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page while an activity gives it a request identifier
        using System.Diagnostics.Activity activity = new System.Diagnostics.Activity("test-request");
        activity.Start();
        await using BunitContext context = new BunitContext();
        Prepare(context, cultureName);
        IRenderedComponent<Error> page = context.Render<Error>();

        // Checks
        page.Find("h1").TextContent.ShouldBe(heading);
        page.Find("strong").TextContent.ShouldBe(requestIdLabel);
        page.Find("code").TextContent.ShouldBe(activity.Id);
        page.Markup.ShouldNotContain("Exception");
    }

    /// <summary>
    /// The reconnection dialog is written in the language of the player, and its script holds no text
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("en-US", "Rejoining the server...", "Retry", "Resume")]
    [InlineData("fr-FR", "Reconnexion au serveur...", "Réessayer", "Reprendre")]
    public async Task TheReconnectDialogIsLocalizedAsync(string cultureName, string firstAttempt, string retry, string resume)
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering
        await using BunitContext context = new BunitContext();
        Prepare(context, cultureName);
        IRenderedComponent<ReconnectModal> dialog = context.Render<ReconnectModal>();

        // Checks: the texts, and the countdown element that the Blazor script fills
        dialog.Find(".components-reconnect-first-attempt-visible").TextContent.Trim().ShouldBe(firstAttempt);
        dialog.Find("#components-reconnect-button").TextContent.Trim().ShouldBe(retry);
        dialog.Find("#components-resume-button").TextContent.Trim().ShouldBe(resume);
        dialog.Find(".components-reconnect-repeated-attempt-visible #components-seconds-to-next-attempt").TextContent.ShouldBeEmpty();
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
