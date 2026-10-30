/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Components/PingPageTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the ping page rendered by bUnit with a fake Gateway client
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Web.Components.Pages;
using BoardGamesPlatform.Web.GatewayLink;
using BoardGamesPlatform.Web.Tests.Support;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Components;

/// <summary>
/// Tests used to check the ping page: the time shown, the progress, the generic error and the languages
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the page is the first thing a player sees of the chain of tiers: its texts must come from the resources, its
/// error must never reveal an internal message, and its time must follow the culture of the request.</para>
/// <para><b>Behavior:</b> each test renders the page in memory (no browser, no network) with a fake Gateway client, then waits for the
/// rendering events it expects: no fixed delay.</para>
/// </remarks>
public sealed class PingPageTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 30000;

    /// <summary>
    /// Longest wait for a rendering event, in seconds
    /// </summary>
    private const int EventTimeoutSeconds = 10;

    /// <summary>
    /// CSS selector of the button
    /// </summary>
    private const string ButtonSelector = "#ping-button";

    /// <summary>
    /// CSS selector of the status line
    /// </summary>
    private const string StatusSelector = "#ping-status";

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
    /// Method used to render the page for a player of the given culture
    /// </summary>
    /// <param name="context">[Cannot be null] Context that renders the page</param>
    /// <param name="client">[Cannot be null] Fake Gateway client</param>
    /// <param name="cultureName">[Cannot be null] Name of the culture of the player, for the texts and for the formats</param>
    /// <returns>
    /// The rendered page
    /// </returns>
    private static IRenderedComponent<PingPage> RenderPage(BunitContext context, IGatewayClient client, string cultureName)
    {
        // Giving the player's culture to the texts and to the formats, as the localization middleware does for a circuit
        CultureInfo culture = CultureInfo.GetCultureInfo(cultureName);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        // Registering what the page needs, with the resources folder of the website
        context.Services.AddSingleton(client);
        context.Services.AddLocalization(options => options.ResourcesPath = "Resources");

        // Function's return
        return context.Render<PingPage>();
    }

    #endregion

    #region Publics

    /// <summary>
    /// After the click, the time of SQL Server is shown with its offset, in the format of the culture
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheTimeIsShownAfterTheClickAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page for an English player, with a fake Gateway that answers
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));
        FakeGatewayClient client = new FakeGatewayClient(serverTime);
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, client, "en-US");
        page.FindAll(StatusSelector).ShouldBeEmpty();

        // Clicking
        page.Find(ButtonSelector).Click();

        // Checks: the time in the culture of the player, its offset, and one call
        string expectedTime = serverTime.ToString("G", CultureInfo.GetCultureInfo("en-US"));
        page.WaitForAssertion(
            () => page.Find(StatusSelector).TextContent.ShouldBe($"SQL Server answered: {expectedTime} (UTC+01:00)"),
            TimeSpan.FromSeconds(EventTimeoutSeconds));
        client.PingCalls.ShouldBe(1);
    }

    /// <summary>
    /// While the call runs, the button is disabled and an in-progress message is shown; the time replaces it afterwards
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheProgressIsShownWhileTheCallRunsAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page with a fake Gateway that holds its answer
        FakeGatewayClient client = new FakeGatewayClient(new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero), hold: true);
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, client, "en-US");

        // Clicking: the progress is shown and the button cannot be clicked again
        page.Find(ButtonSelector).Click();
        page.WaitForAssertion(
            () =>
            {
                page.Find(StatusSelector).TextContent.ShouldBe("Waiting for the answer of SQL Server...");
                page.Find(ButtonSelector).HasAttribute("disabled").ShouldBeTrue();
            },
            TimeSpan.FromSeconds(EventTimeoutSeconds));

        // Releasing the answer: the time replaces the progress and the button works again
        client.Release();
        page.WaitForAssertion(
            () =>
            {
                page.Find(StatusSelector).TextContent.ShouldStartWith("SQL Server answered:");
                page.Find(ButtonSelector).HasAttribute("disabled").ShouldBeFalse();
            },
            TimeSpan.FromSeconds(EventTimeoutSeconds));
    }

    /// <summary>
    /// When the call fails, a generic localized error is shown and the message of the exception is not
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailureShowsAGenericErrorWithoutTheExceptionMessageAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page with a fake Gateway that fails with a message to keep secret
        const string InternalMessage = "secret internal detail of the Gateway link";
        FakeGatewayClient client = new FakeGatewayClient(new InvalidOperationException(InternalMessage));
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, client, "en-US");

        // Clicking
        page.Find(ButtonSelector).Click();

        // Checks: the generic message in an alert, and nowhere the text of the exception
        page.WaitForAssertion(
            () => page.Find(StatusSelector).TextContent.ShouldBe("The platform did not answer. Please try again later."),
            TimeSpan.FromSeconds(EventTimeoutSeconds));
        page.Find(StatusSelector).GetAttribute("role").ShouldBe("alert");
        page.Markup.ShouldNotContain(InternalMessage);
        page.Find(ButtonSelector).HasAttribute("disabled").ShouldBeFalse();
    }

    /// <summary>
    /// An English player reads the texts of the default resources
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheTextsAreEnglishForAnEnglishPlayerAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page for an English player
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, new FakeGatewayClient(DateTimeOffset.UtcNow), "en-US");

        // Checks
        page.Find("h1").TextContent.ShouldBe("Platform ping");
        page.Find(ButtonSelector).TextContent.Trim().ShouldBe("Ping the platform");
    }

    /// <summary>
    /// A French player reads the French texts and the time in the French format, for the success and for the failure
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheTextsAndTheTimeAreFrenchForAFrenchPlayerAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page for a French player, with a fake Gateway that answers
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, new FakeGatewayClient(serverTime), "fr-FR");
        page.Find("h1").TextContent.ShouldBe("Ping de la plateforme");
        page.Find(ButtonSelector).TextContent.Trim().ShouldBe("Interroger la plateforme");

        // Clicking: the time is written day first, 24 hours
        page.Find(ButtonSelector).Click();
        page.WaitForAssertion(
            () => page.Find(StatusSelector).TextContent.ShouldBe("SQL Server a répondu : 30/10/2026 09:15:00 (UTC+01:00)"),
            TimeSpan.FromSeconds(EventTimeoutSeconds));

        // Rendering the page again with a fake Gateway that fails: the error is French
        await using BunitContext failingContext = new BunitContext();
        IRenderedComponent<PingPage> failingPage = RenderPage(failingContext, new FakeGatewayClient(new InvalidOperationException("x")), "fr-FR");
        failingPage.Find(ButtonSelector).Click();
        failingPage.WaitForAssertion(
            () => failingPage.Find(StatusSelector).TextContent.ShouldBe("La plateforme n'a pas répondu. Veuillez réessayer plus tard."),
            TimeSpan.FromSeconds(EventTimeoutSeconds));
    }


    /// <summary>
    /// Two quick clicks call the Gateway once
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TwoClicksCallTheGatewayOnceAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page with a fake Gateway that holds its answer
        FakeGatewayClient client = new FakeGatewayClient(new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero), hold: true);
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, client, "en-US");

        // Clicking twice before the answer
        page.Find(ButtonSelector).Click();
        page.Find(ButtonSelector).Click();
        client.Release();
        page.WaitForAssertion(
            () => page.Find(StatusSelector).TextContent.ShouldStartWith("SQL Server answered:"),
            TimeSpan.FromSeconds(EventTimeoutSeconds));

        // Checks: one call only
        client.PingCalls.ShouldBe(1);
    }

    /// <summary>
    /// Leaving the page cancels the call in progress and shows no error message
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task LeavingThePageCancelsTheCallWithoutAnErrorAsync()
    {
        // Stopping at once when the test run is canceled
        Xunit.TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Rendering the page with a fake Gateway that holds its answer, and clicking
        FakeGatewayClient client = new FakeGatewayClient(new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero), hold: true);
        await using BunitContext context = new BunitContext();
        IRenderedComponent<PingPage> page = RenderPage(context, client, "en-US");
        page.Find(ButtonSelector).Click();
        page.WaitForAssertion(
            () => client.PingCalls.ShouldBe(1),
            TimeSpan.FromSeconds(EventTimeoutSeconds));

        // Leaving the page: its disposal cancels the call
        await page.InvokeAsync(() => page.Instance.Dispose());
        await client.Canceled.WaitAsync(TimeSpan.FromSeconds(EventTimeoutSeconds), Xunit.TestContext.Current.CancellationToken);

        // Letting the page handle the cancellation: the dispatcher runs its work in order, so this runs after the handler of the page
        await page.InvokeAsync(() => { });

        // Checks: no error message, no result
        page.Markup.ShouldNotContain("The platform did not answer");
        page.Markup.ShouldNotContain("SQL Server answered");
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
