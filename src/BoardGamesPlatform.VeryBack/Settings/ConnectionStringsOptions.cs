/*
 *	@file	src/BoardGamesPlatform.VeryBack/Settings/ConnectionStringsOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "ConnectionStrings" configuration section: how the Very Back reaches the database
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Settings;

/// <summary>
/// Settings holding the connection strings of the Very Back, the only tier allowed to reach the database
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a connection string is a secret: it is read from the configuration, never written in a committed file.</para>
/// <para><b>Behavior:</b> read from the "ConnectionStrings" section (user secrets in development, the environment variable
/// <c>ConnectionStrings__Main</c> on the servers). It is empty in appsettings.json to document the key, and <see cref="ConnectionStringsOptionsValidator"/>
/// stops the host at startup while it is missing (fail closed). Its value is never logged.</para>
/// </remarks>
public sealed class ConnectionStringsOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "ConnectionStrings";

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
    /// Connection string of the main database, used with the application SQL user; a secret
    /// </summary>
    public string? Main { get; set; }

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

    // #N/A

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
