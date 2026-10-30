/*
 *	@file	src/BoardGamesPlatform.Gateway/GatewayExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Registers the services of the Gateway and maps its SignalR hub
 */

// Using
using BoardGamesPlatform.Gateway.BackLink;
using BoardGamesPlatform.Gateway.Hubs;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway;

/// <summary>
/// Extensions used to set up the Gateway in its web host
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps Program.cs a short list of steps, and puts the Gateway setup in one place.</para>
/// <para><b>Behavior:</b> reads and validates the "Gateway" configuration section, registers SignalR (detailed errors off) and the client
/// of the Back, then maps the hub on the configured path.</para>
/// </remarks>
public static class GatewayExtensions
{
    #region Members

    #region Constants

    // #N/A

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// Method used to register the services of the Gateway
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> everything the hub needs, declared once.</para>
    /// <para><b>Behavior:</b> binds "Gateway" to <see cref="GatewayOptions"/>, validated when the host starts (the hub path must start with
    /// a slash); adds SignalR with detailed errors off, so that no internal message reaches a front; registers
    /// <see cref="UnavailableBackClient"/> as the client of the Back when none is registered yet (TEMPORARY, replaced by the real client
    /// in feature/back-gateway-link).</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the web host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    public static WebApplicationBuilder AddPlatformGateway(this WebApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Reading the settings, refused at startup when invalid
        builder.Services.AddOptions<GatewayOptions>()
            .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.HubPath) && options.HubPath.StartsWith('/'),
                $"The setting {GatewayOptions.SectionName}:HubPath must start with a slash, for example /hubs/platform")
            .ValidateOnStart();

        // SignalR, with no internal message sent to a front
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);

        // The client of the Back (temporary)
        builder.Services.TryAddSingleton<IBackClient, UnavailableBackClient>();

        // Function's return
        return builder;
    }

    /// <summary>
    /// Method used to map the SignalR hub on the configured path
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> makes the hub reachable by the fronts.</para>
    /// <para><b>Behavior:</b> the hub is not authenticated yet (authentication comes in 0.10.0).</para>
    /// </remarks>
    /// <param name="app">[Cannot be null] Built web application</param>
    /// <returns>
    /// The same application, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the application is null</exception>
    /// <exception cref="OptionsValidationException">If the hub path is invalid</exception>
    public static WebApplication MapPlatformHub(this WebApplication app)
    {
        // Check
        if (app is null)
            throw new ArgumentNullException(nameof(app), $"The value of the parameter: {nameof(app)} cannot be null");

        // Mapping the hub on its path
        GatewayOptions options = app.Services.GetRequiredService<IOptions<GatewayOptions>>().Value;
        app.MapHub<PlatformHub>(options.HubPath);

        // Function's return
        return app;
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
