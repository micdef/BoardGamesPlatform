/*
 *	@file	src/BoardGamesPlatform.Gateway/BackLink/BackClientOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "Back" configuration section: where the Gateway finds the Back
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Links;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.BackLink;

/// <summary>
/// Settings used by the Gateway to reach the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator point the Gateway to the Back through its configuration only, without changing the code.</para>
/// <para><b>Behavior:</b> read from the "Back" section of the configuration (appsettings.json or environment variables such as
/// <c>Back__Port</c>). The Gateway validates them at startup (<see cref="BackClientOptionsValidator"/>) and refuses to start when a setting is
/// invalid (fail closed). The defaults suit a development machine, where both tiers run side by side.</para>
/// </remarks>
public sealed class BackClientOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "Back";

    /// <summary>
    /// Port the Back listens on when none is configured
    /// </summary>
    public const int DefaultPort = 7821;

    #endregion

    #region Variables

    /// <summary>
    /// Longest connect timeout accepted: waiting longer for a tier on the same network means it is down, and a far longer value could not be
    /// set as a cancellation deadline (about 49.7 days at most)
    /// </summary>
    public static readonly TimeSpan MaxConnectTimeout = LinkClient.MaxConnectTimeout;

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
    /// Host name or address of the Back
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port of the Back, from 1 to 65535
    /// </summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Longest time allowed to open a connection to the Back; greater than zero, and at most <see cref="MaxConnectTimeout"/>
    /// </summary>
    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

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
    /// Method used to find what is wrong in the settings
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> one place that knows the rules, for the startup check of the Gateway and for the client itself.</para>
    /// <para><b>Behavior:</b> the host must not be empty, the port must be from 1 to 65535 and the connection timeout greater than zero and at
    /// most <see cref="MaxConnectTimeout"/>. The message names the setting, never its value. Only the first problem found is returned.</para>
    /// </remarks>
    /// <returns>
    /// The message that describes the first invalid setting; null when every setting is valid
    /// </returns>
    public string? FindProblem()
    {
        // Checks
        if (string.IsNullOrWhiteSpace(Host))
            return $"The setting {SectionName}:{nameof(Host)} must not be empty";
        if (Port < 1 || Port > IPEndPoint.MaxPort)
            return $"The setting {SectionName}:{nameof(Port)} must be between 1 and {IPEndPoint.MaxPort}";
        if (ConnectTimeout <= TimeSpan.Zero || ConnectTimeout > MaxConnectTimeout)
            return $"The setting {SectionName}:{nameof(ConnectTimeout)} must be greater than zero and at most {MaxConnectTimeout}";

        // Function's return
        return null;
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
