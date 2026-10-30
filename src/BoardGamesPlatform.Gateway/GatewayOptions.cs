/*
 *	@file	src/BoardGamesPlatform.Gateway/GatewayOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "Gateway" configuration section
 */

// Using
using BoardGamesPlatform.Gateway.Contracts;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway;

/// <summary>
/// Settings of the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator change what the fronts see of the Gateway through its configuration only.</para>
/// <para><b>Behavior:</b> read from the "Gateway" section of the configuration (appsettings.json or environment variables such as
/// <c>Gateway__HubPath</c>). The values are validated when the Gateway starts: an invalid setting stops the startup.</para>
/// </remarks>
public sealed class GatewayOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "Gateway";

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
    /// Path of the SignalR hub, relative to the address of the Gateway; it must start with a slash
    /// </summary>
    public string HubPath { get; set; } = PlatformHubContract.DefaultPath;

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
