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
    /// <para><b>Behavior:</b> binds "Back" (where the Back listens) and "VeryBack" (where it reaches the Very Back) with their validators,
    /// checked when the host starts: an invalid value stops it before anything listens (fail closed). Registers one client of the Very Back,
    /// shared by every connection of the Gateway and disposed with the host, and the listener of the Gateway.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    public static HostApplicationBuilder AddPlatformBack(this HostApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Settings, checked when the host starts
        builder.Services.AddSingleton<IValidateOptions<BackOptions>, BackOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<VeryBackClientOptions>, VeryBackClientOptionsValidator>();
        builder.Services.AddOptions<BackOptions>().Bind(builder.Configuration.GetSection(BackOptions.SectionName)).ValidateOnStart();
        builder.Services.AddOptions<VeryBackClientOptions>().Bind(builder.Configuration.GetSection(VeryBackClientOptions.SectionName)).ValidateOnStart();

        // Calling the Very Back over plain TCP, and listening for the Gateway
        builder.Services.AddSingleton(services => new VeryBackClient(services.GetRequiredService<IOptions<VeryBackClientOptions>>().Value,
            new TcpTransportConnector(), services.GetRequiredService<ILogger<VeryBackClient>>()));
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
