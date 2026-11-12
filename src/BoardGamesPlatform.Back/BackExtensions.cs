/*
 *	@file	src/BoardGamesPlatform.Back/BackExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Registers the settings, the client of the Very Back and the listener of the Gateway in the host of the Back
 */

// Using
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.Back.Settings;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.Vault;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back;

/// <summary>
/// Extensions used to set up the Back in its host
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps Program.cs a short list of steps, and lets the tests build the host exactly as Program.cs does.</para>
/// <para><b>Behavior:</b> registers the validated settings, the client of the Very Back and the listener of the Gateway.</para>
/// </remarks>
public static class BackExtensions
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
    /// Method used to register the services of the Back
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> everything the listener of the Gateway needs, declared once.</para>
    /// <para><b>Behavior:</b> adds the vault of the system first, which gives the two secrets of the Back (the passwords of its PFX and of its
    /// signing key) and refuses, outside Development, a secret from any other source (doc/dev/database.md, The vault). Binds "Back" (where the
    /// Back listens) and "VeryBack" (where it reaches the Very Back) with their validators, checked when the host starts: an invalid value stops
    /// it before anything listens (fail closed). Registers one client of the Very Back, shared by every connection of the Gateway and disposed
    /// with the host, and the listener of the Gateway.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    /// <exception cref="VaultException">If the vault is configured but cannot be read safely</exception>
    public static HostApplicationBuilder AddPlatformBack(this HostApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Secrets from the vault of the system
        builder.AddPlatformVault(
            $"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePassword)}",
            $"{HandshakeOptions.SectionName}:{nameof(HandshakeOptions.SigningKeyPassword)}");

        // Settings, checked when the host starts
        builder.Services.AddSingleton<IValidateOptions<BackOptions>, BackOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<VeryBackClientOptions>, VeryBackClientOptionsValidator>();
        builder.Services.AddOptions<BackOptions>().Bind(builder.Configuration.GetSection(BackOptions.SectionName)).ValidateOnStart();
        builder.Services.AddOptions<VeryBackClientOptions>().Bind(builder.Configuration.GetSection(VeryBackClientOptions.SectionName)).ValidateOnStart();

        // Mutual TLS: the settings, checked at startup, and the credentials of the Back, loaded and checked when first needed (when the
        // services are built, before anything listens or connects); a relative path is taken from the folder of the Back
        builder.Services.AddSingleton<IValidateOptions<TlsOptions>, TlsOptionsValidator>();
        builder.Services.AddOptions<TlsOptions>().Bind(builder.Configuration.GetSection(TlsOptions.SectionName)).ValidateOnStart();
        builder.Services.AddSingleton(services => TlsCredentials.FromOptions(PlatformRole.Back, services.GetRequiredService<IOptions<TlsOptions>>().Value,
            builder.Environment.ContentRootPath, TimeProvider.System, services.GetRequiredService<ILogger<TlsCredentials>>()));

        // Secure channel of the Very Back link: the settings, checked at startup, and the keys, loaded and checked when first needed, with the
        // TLS settings for the rule "the password of the signing key is never the one of the PFX"
        builder.Services.AddSingleton<IValidateOptions<HandshakeOptions>, HandshakeOptionsValidator>();
        builder.Services.AddOptions<HandshakeOptions>().Bind(builder.Configuration.GetSection(HandshakeOptions.SectionName)).ValidateOnStart();
        builder.Services.AddSingleton(services => SigningKeys.FromOptions(services.GetRequiredService<IOptions<HandshakeOptions>>().Value,
            services.GetRequiredService<IOptions<TlsOptions>>().Value, services.GetRequiredService<TlsCredentials>(), builder.Environment.ContentRootPath,
            TimeProvider.System, services.GetRequiredService<ILogger<SigningKeys>>()));
        builder.Services.AddSingleton(services => new SecureTransportConnector(
            new TlsTransportConnector(new TcpTransportConnector(), services.GetRequiredService<TlsCredentials>(), PlatformRole.VeryBack,
                services.GetRequiredService<ILogger<VeryBackClient>>()),
            services.GetRequiredService<SigningKeys>(), services.GetRequiredService<ILogger<VeryBackClient>>()));

        // Calling the Very Back over mutual TLS (only the Very Back is accepted as its server) and the secure channel, and listening for the Gateway
        builder.Services.AddSingleton(services => new VeryBackClient(services.GetRequiredService<IOptions<VeryBackClientOptions>>().Value,
            services.GetRequiredService<SecureTransportConnector>(), services.GetRequiredService<ILogger<VeryBackClient>>()));
        builder.Services.AddHostedService<BackListenerService>();

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
