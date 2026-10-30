/*
 *	@file	src/BoardGamesPlatform.VeryBack/Settings/ConnectionStringsOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "ConnectionStrings" settings at startup
 */

// Using
using Microsoft.Data.SqlClient;
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
/// <para><b>Behavior:</b> fails when the main connection string is missing, empty or white spaces, or when the SQL client cannot read it.
/// The message names the setting and never repeats its value nor the error of the SQL client: for a malformed string, that error quotes the
/// unknown keyword, which can be a piece of a password holding a semicolon. Whether the server, the database and the login are right is
/// only known when a connection opens.</para>
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
    /// Method used to know whether the SQL client can read a connection string
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> builds a connection object with the string, as the call of a procedure does, without opening it: nothing
    /// reaches the network. The error of the SQL client is dropped on purpose, since its message can quote a piece of the string.</para>
    /// </remarks>
    /// <param name="connectionString">[Cannot be null] Connection string to read</param>
    /// <returns>
    /// True if the SQL client reads it, false otherwise
    /// </returns>
    public static bool IsWellFormed(string connectionString)
    {
        // Reading the string with the parser of the SQL client
        try
        {
            using SqlConnection probe = new SqlConnection(connectionString);

            // Function's return
            return true;
        }
        catch (ArgumentException)
        {
            // Function's return
            return false;
        }
    }

    /// <summary>
    /// Method used to check the connection strings of the Very Back
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure naming the missing or unreadable setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        // Check
        if (string.IsNullOrWhiteSpace(options.Main))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is missing: set it with " +
                "'dotnet user-secrets' in development or the environment variable ConnectionStrings__Main on a server (see doc/adm/configuration.md)");
        }
        if (!IsWellFormed(options.Main))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is not a connection string " +
                "the SQL client can read (unknown keyword, invalid value or unbalanced quote; a value holding a semicolon must be quoted)");
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
