/*
 *	@file	src/BoardGamesPlatform.Web/GatewayLink/GatewayClientOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "Gateway" configuration section of the website
 */

// Using
using BoardGamesPlatform.Gateway.Contracts;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.GatewayLink;

/// <summary>
/// Settings used by the website to reach the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator point the website to the Gateway through its configuration only.</para>
/// <para><b>Behavior:</b> read from the "Gateway" section of the configuration (appsettings.json or environment variables such as
/// <c>Gateway__HubUrl</c>). The values are validated at startup: an invalid setting stops the website, with a message that names the
/// setting and never repeats its value.</para>
/// </remarks>
public sealed class GatewayClientOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "Gateway";

    /// <summary>
    /// Default address of the hub: the HTTP address of the Gateway in development (Properties/launchSettings.json of the Gateway) and the default hub path
    /// </summary>
    public const string DefaultHubUrl = "http://localhost:5273" + PlatformHubContract.DefaultPath;

    /// <summary>
    /// Greatest accepted value of <see cref="RequestTimeout"/>, in seconds
    /// </summary>
    public const int MaxRequestTimeoutSeconds = 60;

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

    /// <summary>
    /// Full address of the SignalR hub of the Gateway; an absolute HTTP or HTTPS address
    /// </summary>
    public string HubUrl { get; set; } = DefaultHubUrl;

    /// <summary>
    /// Longest duration of a call to the Gateway, connection included; greater than zero and at most <see cref="MaxRequestTimeoutSeconds"/> seconds
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// Method used to check that the hub address is an absolute HTTP or HTTPS address
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> refuses at startup an address the SignalR client could not use.</para>
    /// <para><b>Behavior:</b> only the shape is checked (absolute, http or https): the address is set by an administrator.</para>
    /// </remarks>
    /// <returns>
    /// True  ==> The address is valid<br/>
    /// False ==> The address is empty, relative or uses another scheme
    /// </returns>
    public bool IsHubUrlValid()
    {
        // Function's return
        return Uri.TryCreate(HubUrl, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    /// Method used to check that the request timeout is greater than zero and at most one minute
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a Gateway that accepts the connection but never answers must not leave a player waiting forever.</para>
    /// <para><b>Behavior:</b> refuses zero, negative values and values above <see cref="MaxRequestTimeoutSeconds"/> seconds.</para>
    /// </remarks>
    /// <returns>
    /// True  ==> The timeout is valid<br/>
    /// False ==> The timeout is zero, negative or too long
    /// </returns>
    public bool IsRequestTimeoutValid()
    {
        // Function's return
        return RequestTimeout > TimeSpan.Zero && RequestTimeout <= TimeSpan.FromSeconds(MaxRequestTimeoutSeconds);
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
