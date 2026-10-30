/*
 *	@file	src/BoardGamesPlatform.VeryBack/Settings/VeryBackOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "VeryBack" configuration section: where the Very Back waits for the Back
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
/// Settings used to choose the address and the port on which the Very Back waits for the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator move the listener of the Very Back through its configuration only, without changing the code.</para>
/// <para><b>Behavior:</b> read from the "VeryBack" section of the configuration (appsettings.json or environment variables such as
/// <c>VeryBack__Port</c>) and checked at startup by <see cref="VeryBackOptionsValidator"/>: an invalid value stops the host. The defaults
/// listen on the loopback address only, so that nothing outside the machine can reach the Very Back unless it is configured on purpose.</para>
/// </remarks>
public sealed class VeryBackOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "VeryBack";

    /// <summary>
    /// Address listened to when none is configured: the loopback address
    /// </summary>
    public const string DefaultListenAddress = "127.0.0.1";

    /// <summary>
    /// Port listened to when none is configured (Back to Very Back, see the network flows of the roadmap)
    /// </summary>
    public const int DefaultPort = 7822;

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
    /// IP address (IPv4 or IPv6) the Very Back listens on; "127.0.0.1" by default
    /// </summary>
    public string ListenAddress { get; set; } = DefaultListenAddress;

    /// <summary>
    /// TCP port the Very Back listens on, from 1 to 65535; 7822 by default
    /// </summary>
    public int Port { get; set; } = DefaultPort;

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
