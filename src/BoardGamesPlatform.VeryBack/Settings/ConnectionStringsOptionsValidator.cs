/*
 *	@file	src/BoardGamesPlatform.VeryBack/Settings/ConnectionStringsOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "ConnectionStrings" settings at startup
 */

// Using
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Settings;

/// <summary>
/// Validator of <see cref="ConnectionStringsOptions"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> fail closed: a Very Back without a connection string refuses to start, instead of failing at the first request.</para>
/// <para><b>Behavior:</b> fails when the main connection string is missing, empty or white spaces. The message names the setting and never
/// repeats its value. The content of the string is not checked here: the SQL client does it when it opens a connection.</para>
/// </remarks>
public sealed class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
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
    /// Method used to check the connection strings of the Very Back
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure naming the missing setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        // Check
        if (string.IsNullOrWhiteSpace(options.Main))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is missing: set it with " +
                "'dotnet user-secrets' in development or the environment variable ConnectionStrings__Main on a server (see doc/adm/configuration.md)");
        }

        // Function's return
        return ValidateOptionsResult.Success;
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
