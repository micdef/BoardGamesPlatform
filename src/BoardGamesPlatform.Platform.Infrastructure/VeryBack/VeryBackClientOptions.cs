/*
 *	@file	src/BoardGamesPlatform.Platform.Infrastructure/VeryBack/VeryBackClientOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings of the "VeryBack" configuration section: where the Back finds the Very Back
 */

// Using
using System.Net;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.VeryBack;

/// <summary>
/// Settings used by the Back to reach the Very Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets an administrator point the Back to the Very Back through its configuration only, without changing the code.</para>
/// <para><b>Behavior:</b> read from the "VeryBack" section of the configuration (appsettings.json or environment variables such as
/// <c>VeryBack__Port</c>). The Back calls <see cref="Validate"/> at startup and refuses to start when a setting is invalid (fail closed).
/// The defaults suit a development machine, where both tiers run side by side.</para>
/// </remarks>
public sealed class VeryBackClientOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section holding these settings
    /// </summary>
    public const string SectionName = "VeryBack";

    /// <summary>
    /// Port the Very Back listens on when none is configured
    /// </summary>
    public const int DefaultPort = 7822;

    #endregion

    #region Variables

    /// <summary>
    /// Longest connect timeout accepted: waiting longer for a tier on the same network means it is down, and a far longer value could not be
    /// set as a cancellation deadline (about 49.7 days at most)
    /// </summary>
    public static readonly TimeSpan MaxConnectTimeout = TimeSpan.FromMinutes(5);

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
    /// Host name or address of the Very Back
    /// </summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>
    /// Port of the Very Back, from 1 to 65535
    /// </summary>
    public int Port { get; set; } = DefaultPort;

    /// <summary>
    /// Longest time allowed to open a connection to the Very Back; greater than zero, and at most <see cref="MaxConnectTimeout"/>
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
    /// Method used to check the settings
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> stops the startup of the Back on a wrong setting, rather than failing at the first call to the Very Back.</para>
    /// <para><b>Behavior:</b> the host must not be empty, the port must be from 1 to 65535 and the connection timeout greater than zero and at
    /// most <see cref="MaxConnectTimeout"/>. The message names the setting, never its value.</para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">If a setting is invalid</exception>
    public void Validate()
    {
        // Checks
        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException($"The setting {SectionName}:{nameof(Host)} must not be empty");
        if (Port < 1 || Port > IPEndPoint.MaxPort)
            throw new InvalidOperationException($"The setting {SectionName}:{nameof(Port)} must be between 1 and {IPEndPoint.MaxPort}");
        if (ConnectTimeout <= TimeSpan.Zero || ConnectTimeout > MaxConnectTimeout)
            throw new InvalidOperationException($"The setting {SectionName}:{nameof(ConnectTimeout)} must be greater than zero and at most {MaxConnectTimeout}");
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
