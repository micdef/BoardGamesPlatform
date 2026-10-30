/*
 *	@file	src/BoardGamesPlatform.Web/GatewayLink/GatewayClientExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Registers the settings and the client of the Gateway in the website
 */

// Using
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.GatewayLink;

/// <summary>
/// Extensions used to set up the link of the website to the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps Program.cs a short list of steps.</para>
/// <para><b>Behavior:</b> binds and validates the "Gateway" section, then registers <see cref="IGatewayClient"/> as scoped: in Blazor
/// Server a scoped service lives as long as the circuit of one player, and is disposed with it.</para>
/// </remarks>
public static class GatewayClientExtensions
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
    /// Method used to register the settings and the client of the Gateway
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> everything the pages need to call the Gateway, declared once.</para>
    /// <para><b>Behavior:</b> validates <see cref="GatewayClientOptions"/> at startup (the message names the setting, never its value), and
    /// registers a scoped <see cref="SignalRGatewayClient"/> on a connection built from the hub address. Nothing connects before the first call.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the web host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    public static WebApplicationBuilder AddGatewayClient(this WebApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Reading the settings, refused at startup when invalid
        builder.Services.AddOptions<GatewayClientOptions>()
            .Bind(builder.Configuration.GetSection(GatewayClientOptions.SectionName))
            .Validate(
                options => options.IsHubUrlValid(),
                $"The setting {GatewayClientOptions.SectionName}:HubUrl must be an absolute http or https address, for example {GatewayClientOptions.DefaultHubUrl}")
            .ValidateOnStart();

        // The client of the Gateway: one per circuit, disposed with it
        builder.Services.AddScoped<IGatewayClient>(services =>
        {
            GatewayClientOptions options = services.GetRequiredService<IOptions<GatewayClientOptions>>().Value;
            HubConnection connection = new HubConnectionBuilder()
                .WithUrl(options.HubUrl)
                .Build();
            return new SignalRGatewayClient(connection);
        });

        // Function's return
        return builder;
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
