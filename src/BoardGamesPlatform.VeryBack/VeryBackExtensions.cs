/*
 *	@file	src/BoardGamesPlatform.VeryBack/VeryBackExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Registers the settings, the procedures and the listener of the Very Back
 */

// Using
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Services;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack;

/// <summary>
/// Extensions used to set up the Very Back in its host
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps Program.cs a short list of steps, and lets the tests build the host exactly as Program.cs does.</para>
/// <para><b>Behavior:</b> registers the validated settings, the call of the Ping procedure and the listener of the Back.</para>
/// </remarks>
public static class VeryBackExtensions
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
    /// Method used to register the services of the Very Back
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> everything the listener needs, declared once.</para>
    /// <para><b>Behavior:</b> binds "VeryBack" and "ConnectionStrings" with their validators, checked when the host starts: an invalid
    /// address or port, or a missing or malformed connection string, stops it before anything listens (fail closed). Registers the call of
    /// the Ping procedure on SQL Server and the listener of the Back.</para>
    /// </remarks>
    /// <param name="builder">[Cannot be null] Builder of the host</param>
    /// <returns>
    /// The same builder, to chain the calls
    /// </returns>
    /// <exception cref="ArgumentNullException">If the builder is null</exception>
    public static HostApplicationBuilder AddPlatformVeryBack(this HostApplicationBuilder builder)
    {
        // Check
        if (builder is null)
            throw new ArgumentNullException(nameof(builder), $"The value of the parameter: {nameof(builder)} cannot be null");

        // Settings, checked when the host starts
        builder.Services.AddSingleton<IValidateOptions<VeryBackOptions>, VeryBackOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<ConnectionStringsOptions>, ConnectionStringsOptionsValidator>();
        builder.Services.AddOptions<VeryBackOptions>().Bind(builder.Configuration.GetSection(VeryBackOptions.SectionName)).ValidateOnStart();
        builder.Services.AddOptions<ConnectionStringsOptions>().Bind(builder.Configuration.GetSection(ConnectionStringsOptions.SectionName)).ValidateOnStart();

        // Listening for the Back and calling the database
        builder.Services.AddSingleton<IPingProcedure, SqlPingProcedure>();
        builder.Services.AddHostedService<VeryBackListenerService>();

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
