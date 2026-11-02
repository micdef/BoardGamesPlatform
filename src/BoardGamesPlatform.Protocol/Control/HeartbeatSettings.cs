/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HeartbeatSettings.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Timings of the heartbeat: how often the client pings, and how long each end waits for a frame before closing
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Timings of the heartbeat of a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place for the three durations that both ends must agree on, so that the server never closes a client that pings
/// as expected.</para>
/// <para><b>Behavior:</b> the client pings every <see cref="PingInterval"/>, and closes when it has received no frame for
/// <see cref="ClientTimeout"/>; the server closes a connection on which it has received no frame for <see cref="ServerIdleTimeout"/>. Any
/// frame counts, a request or a response as well as a ping or a pong. Both timeouts are longer than the interval, so that one lost ping is
/// not enough to close. The tiers use <see cref="Default"/>; the tests use shorter timings with a fake clock.</para>
/// </remarks>
public sealed class HeartbeatSettings
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Timings used by the tiers: a ping every 10 seconds, a client that gives up after 30 seconds without a frame, a server after 45
    /// </summary>
    public static readonly HeartbeatSettings Default = new HeartbeatSettings(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(45));

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
    /// Time between two pings of the client
    /// </summary>
    public TimeSpan PingInterval { get; }

    /// <summary>
    /// Time without any frame after which the client closes the connection
    /// </summary>
    public TimeSpan ClientTimeout { get; }

    /// <summary>
    /// Time without any frame after which the server closes the connection
    /// </summary>
    public TimeSpan ServerIdleTimeout { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="pingInterval">Time between two pings of the client; greater than zero</param>
    /// <param name="clientTimeout">Time without any frame after which the client closes; longer than the interval</param>
    /// <param name="serverIdleTimeout">Time without any frame after which the server closes; longer than the interval</param>
    /// <exception cref="ArgumentOutOfRangeException">If a duration is out of its range</exception>
    public HeartbeatSettings(TimeSpan pingInterval, TimeSpan clientTimeout, TimeSpan serverIdleTimeout)
    {
        // Checks
        if (pingInterval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(pingInterval), pingInterval, $"The value of the parameter: {nameof(pingInterval)} must be greater than zero");
        if (clientTimeout <= pingInterval)
            throw new ArgumentOutOfRangeException(nameof(clientTimeout), clientTimeout, $"The value of the parameter: {nameof(clientTimeout)} must be longer than the ping interval");
        if (serverIdleTimeout <= pingInterval)
            throw new ArgumentOutOfRangeException(nameof(serverIdleTimeout), serverIdleTimeout, $"The value of the parameter: {nameof(serverIdleTimeout)} must be longer than the ping interval");

        // Setting
        PingInterval = pingInterval;
        ClientTimeout = clientTimeout;
        ServerIdleTimeout = serverIdleTimeout;
    }

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
