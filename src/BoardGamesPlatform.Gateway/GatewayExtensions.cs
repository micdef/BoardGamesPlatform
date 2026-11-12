/*
 *	@file	src/BoardGamesPlatform.Gateway/GatewayExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Registers the services of the Gateway and maps its SignalR hub
 */

// Using
using BoardGamesPlatform.Gateway.BackLink;
using BoardGamesPlatform.Gateway.Hubs;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.Vault;
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
    /// <para><b>Behavior:</b> binds "Gateway" to <see cref="GatewayOptions"/>, validated at startup: when <see cref="MapPlatformHub"/> reads the hub path, and again by ValidateOnStart as a safety net (the hub path must start with
    /// a slash); binds "Back" to <see cref="BackClientOptions"/>, validated by ValidateOnStart (fail closed: an invalid setting stops the
    /// startup); adds SignalR with detailed errors off, so that no internal message reaches a front; registers a <see cref="BackClient"/>
    /// on plain TCP as the client of the Back when none is registered yet (the tests register their own). Adds first the vault of the system,
    /// which gives the secret of the Gateway (the password of its PFX) and refuses, outside Development, a secret from any other source
    /// (doc/dev/database.md, The vault).</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the web host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    /// <exception cref="VaultException">If the vault is configured but cannot be read safely</exception>
    public static WebApplicationBuilder AddPlatformGateway(this WebApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Secrets from the vault of the system
        builder.AddPlatformVault($"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePassword)}");

        // Reading the settings, refused at startup when invalid
        builder.Services.AddOptions<GatewayOptions>()
            .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.HubPath) && options.HubPath.StartsWith('/'),
                $"The setting {GatewayOptions.SectionName}:HubPath must start with a slash, for example /hubs/platform")
            .ValidateOnStart();

        // Reading the settings of the link to the Back, refused at startup when invalid
        builder.Services.AddSingleton<IValidateOptions<BackClientOptions>, BackClientOptionsValidator>();
        builder.Services.AddOptions<BackClientOptions>()
            .Bind(builder.Configuration.GetSection(BackClientOptions.SectionName))
            .ValidateOnStart();

        // Mutual TLS: the settings, checked at startup, and the credentials of the Gateway, loaded and checked when first needed (when the
        // services are built, before anything listens or connects); a relative path is taken from the folder of the Gateway
        builder.Services.AddSingleton<IValidateOptions<TlsOptions>, TlsOptionsValidator>();
        builder.Services.AddOptions<TlsOptions>().Bind(builder.Configuration.GetSection(TlsOptions.SectionName)).ValidateOnStart();
        builder.Services.AddSingleton(services => TlsCredentials.FromOptions(PlatformRole.Gateway, services.GetRequiredService<IOptions<TlsOptions>>().Value,
            builder.Environment.ContentRootPath, TimeProvider.System, services.GetRequiredService<ILogger<TlsCredentials>>()));

        // SignalR, with no internal message sent to a front
        builder.Services.AddSignalR(options => options.EnableDetailedErrors = false);

        // The client of the Back, one connection shared by all the fronts (disposed with the host), over mutual TLS: only the Back is accepted
        // as its server
        builder.Services.TryAddSingleton<IBackClient>(services => new BackClient(
            services.GetRequiredService<IOptions<BackClientOptions>>().Value,
            new TlsTransportConnector(new TcpTransportConnector(), services.GetRequiredService<TlsCredentials>(), PlatformRole.Back,
                services.GetRequiredService<ILogger<BackClient>>()),
            services.GetRequiredService<ILogger<BackClient>>()));

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
