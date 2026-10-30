/*
 *	@file	src/BoardGamesPlatform.Web/Components/Pages/PingPage.razor.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logic of the page that pings the platform
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using BoardGamesPlatform.Web.GatewayLink;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Components.Pages;

/// <summary>
/// Page that pings the platform through the Gateway and shows the time of SQL Server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves, from a browser, that the whole chain answers: website, Gateway, Back, Very Back and SQL Server.</para>
/// <para><b>Behavior:</b> a button calls <see cref="IGatewayClient.PingAsync"/>. While the call runs the button is disabled and a
/// message says so. The time of SQL Server is then shown with its offset, formatted with the culture of the request. Whatever the failure,
/// a generic localized message is shown: the text of the exception is only logged. Every text comes from the resources
/// (<see cref="WebTexts"/>).</para>
/// </remarks>
public sealed partial class PingPage : ComponentBase, IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// What the page is doing and shows
    /// </summary>
    private PingState _state = PingState.Idle;

    /// <summary>
    /// Text of the successful answer, already formatted; null when there is none
    /// </summary>
    private string? _resultText;

    /// <summary>
    /// Cancels the call in progress when the player leaves the page
    /// </summary>
    private readonly CancellationTokenSource _leaving = new CancellationTokenSource();

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Enumerations

    /// <summary>
    /// States of the page
    /// </summary>
    private enum PingState
    {
        /// <summary>
        /// Nothing asked yet
        /// </summary>
        Idle,

        /// <summary>
        /// The call is running
        /// </summary>
        InProgress,

        /// <summary>
        /// The platform answered
        /// </summary>
        Succeeded,

        /// <summary>
        /// The call failed
        /// </summary>
        Failed
    }

    #endregion

    #region Services

    /// <summary>
    /// Client used to call the Gateway
    /// </summary>
    private readonly IGatewayClient _svcGatewayClient;

    /// <summary>
    /// Service giving the texts of the page in the language of the player
    /// </summary>
    private readonly IStringLocalizer<WebTexts> _svcTexts;

    /// <summary>
    /// Logger of the page
    /// </summary>
    private readonly ILogger<PingPage> _logger;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="svcGatewayClient">[Cannot be null] Client used to call the Gateway</param>
    /// <param name="svcTexts">[Cannot be null] Service giving the texts in the language of the player</param>
    /// <param name="logger">[Cannot be null] Logger of the page</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public PingPage(IGatewayClient svcGatewayClient, IStringLocalizer<WebTexts> svcTexts, ILogger<PingPage> logger)
    {
        // Check
        if (svcGatewayClient is null)
            throw new ArgumentNullException(nameof(svcGatewayClient), $"The value of the parameter: {nameof(svcGatewayClient)} cannot be null");
        if (svcTexts is null)
            throw new ArgumentNullException(nameof(svcTexts), $"The value of the parameter: {nameof(svcTexts)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting
        _svcGatewayClient = svcGatewayClient;
        _svcTexts = svcTexts;
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to get a text of the page in the language of the player
    /// </summary>
    /// <param name="key">[Cannot be null] Key of the text in the resources</param>
    /// <returns>
    /// The text; the key itself when the resources do not hold it
    /// </returns>
    private string Text(string key)
    {
        // Function's return
        return _svcTexts[key].Value;
    }

    /// <summary>
    /// Method used to build the text of a successful answer
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> shows the time of SQL Server with its offset in the format of the player.</para>
    /// <para><b>Behavior:</b> the date and time use the general format of the current culture (set by the localization middleware from the
    /// request); the offset is written as hours and minutes after "UTC".</para>
    /// </remarks>
    /// <param name="serverTime">Time of SQL Server, with its offset</param>
    /// <returns>
    /// The localized sentence
    /// </returns>
    private string FormatResult(DateTimeOffset serverTime)
    {
        // Formatting the time and its offset with the current culture
        CultureInfo culture = CultureInfo.CurrentCulture;
        string time = serverTime.ToString("G", culture);
        string offset = serverTime.ToString("zzz", culture);

        // Function's return
        return string.Format(culture, _svcTexts["Ping_Result"].Value, time, offset);
    }

    /// <summary>
    /// Method used to log that the call failed
    /// </summary>
    /// <param name="exception">Reason of the failure; logged, never shown</param>
    [LoggerMessage(EventId = 100, Level = LogLevel.Warning, Message = "The ping of the platform failed")]
    private partial void LogPingFailed(Exception exception);

    /// <summary>
    /// Method used when the player clicks the button
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> asks the platform for the time of SQL Server.</para>
    /// <para><b>Behavior:</b> every failure except the cancellation by leaving the page shows the generic error message.</para>
    /// </remarks>
    /// <returns>
    /// A task that ends when the answer, or the failure, is shown
    /// </returns>
    private async Task OnPingClickedAsync()
    {
        // Ignoring a click while a call is already running (the button is disabled, this covers two clicks before the page redraws)
        if (_state == PingState.InProgress)
            return;

        // Showing the progress
        _state = PingState.InProgress;
        _resultText = null;

        try
        {
            // Calling the platform
            PingResponse response = await _svcGatewayClient.PingAsync(_leaving.Token);

            // Showing the time
            _resultText = FormatResult(response.ServerTime);
            _state = PingState.Succeeded;
        }
        catch (OperationCanceledException) when (_leaving.IsCancellationRequested)
        {
            // The player left the page: nothing to show
        }
        catch (Exception exception)
        {
            // Showing a generic message, the details only go to the log
            LogPingFailed(exception);
            _state = PingState.Failed;
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to cancel the call in progress when the page is left
    /// </summary>
    public void Dispose()
    {
        // Cancelling, then releasing even if a callback of the cancellation fails
        try
        {
            _leaving.Cancel();
        }
        finally
        {
            _leaving.Dispose();
        }
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
