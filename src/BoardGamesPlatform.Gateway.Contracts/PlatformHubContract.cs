/*
 *	@file	src/BoardGamesPlatform.Gateway.Contracts/PlatformHubContract.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Address and method names of the SignalR hub that the Gateway exposes to the fronts
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Contracts;

/// <summary>
/// Address and method names of the SignalR hub that the Gateway exposes to the fronts
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place for the names shared by the Gateway, which maps the hub, and every front (Blazor, WPF, third-party UI),
/// which calls it.</para>
/// <para><b>Behavior:</b> the path is relative to the address of the Gateway; it can be changed by the configuration of the Gateway, this
/// is its default value.</para>
/// </remarks>
public static class PlatformHubContract
{
    #region Members

    #region Constants

    /// <summary>
    /// Default path of the hub, relative to the address of the Gateway
    /// </summary>
    public const string DefaultPath = "/hubs/platform";

    /// <summary>
    /// Hub method that checks that the chain of tiers answers; it takes no argument and returns a <see cref="Ping.PingResponse"/>
    /// </summary>
    public const string Ping = "Ping";

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
