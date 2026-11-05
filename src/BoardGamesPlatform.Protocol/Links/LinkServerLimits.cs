/*
 *	@file	src/BoardGamesPlatform.Protocol/Links/LinkServerLimits.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Limits of the connections a link server holds: served, in their handshake, in their handshake per source, and the pace of the refusal logs
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Links;

/// <summary>
/// Limits of the connections of a <see cref="LinkServer"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a peer that can reach the port must not be able to take every place of the tier. The connections still in their
/// handshake (not yet authenticated) therefore get places of their own, separate from the served ones (authenticated by mutual TLS), and one
/// source can only hold a part of them; a flood of refusals cannot flood the logs either.</para>
/// <para><b>Behavior:</b> immutable, checked when built. A source is the IP address of the peer (its port is left out, since a peer chooses
/// it freely).</para>
/// </remarks>
public sealed class LinkServerLimits
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of connections served at the same time when none is given: a link has one client tier, which shares one connection, so a few
    /// places cover a reconnection that overlaps the end of the lost connection
    /// </summary>
    public const int DefaultMaxConnections = 8;

    /// <summary>
    /// Number of connections in their handshake at the same time when none is given: a client tier opens its connections one at a time
    /// </summary>
    public const int DefaultMaxPendingHandshakes = 4;

    /// <summary>
    /// Number of connections in their handshake at the same time from one source when none is given: half of the places, so that a single
    /// source never takes them all
    /// </summary>
    public const int DefaultMaxHandshakesPerSource = 2;

    /// <summary>
    /// Shortest time between two logs of a refused connection when none is given
    /// </summary>
    public static readonly TimeSpan DefaultRefusalLogInterval = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Limits used by the tiers
    /// </summary>
    public static readonly LinkServerLimits Default = new LinkServerLimits(DefaultMaxConnections, DefaultMaxPendingHandshakes, DefaultMaxHandshakesPerSource,
        DefaultRefusalLogInterval);

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
    /// Number of connections served at the same time, at most
    /// </summary>
    public int MaxConnections { get; }

    /// <summary>
    /// Number of connections in their handshake (TLS, then protocol) at the same time, at most
    /// </summary>
    public int MaxPendingHandshakes { get; }

    /// <summary>
    /// Number of connections in their handshake at the same time from one source, at most
    /// </summary>
    public int MaxHandshakesPerSource { get; }

    /// <summary>
    /// Shortest time between two logs of a refused connection; the refusals in between are counted, and their number is logged with the next
    /// log
    /// </summary>
    public TimeSpan RefusalLogInterval { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="maxConnections">Number of connections served at the same time, at most; 1 or more</param>
    /// <param name="maxPendingHandshakes">Number of connections in their handshake at the same time, at most; 1 or more</param>
    /// <param name="maxHandshakesPerSource">Number of connections in their handshake at the same time from one source, at most; from 1 to
    /// <paramref name="maxPendingHandshakes"/></param>
    /// <param name="refusalLogInterval">Shortest time between two logs of a refused connection; zero (every refusal logged) or more</param>
    /// <exception cref="ArgumentOutOfRangeException">If a maximum is below one, the maximum per source above the maximum of handshakes, or the
    /// interval negative</exception>
    public LinkServerLimits(int maxConnections, int maxPendingHandshakes, int maxHandshakesPerSource, TimeSpan refusalLogInterval)
    {
        // Checks
        if (maxConnections < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConnections), $"The value of the parameter: {nameof(maxConnections)} must be greater than zero");
        if (maxPendingHandshakes < 1)
            throw new ArgumentOutOfRangeException(nameof(maxPendingHandshakes), $"The value of the parameter: {nameof(maxPendingHandshakes)} must be greater than zero");
        if (maxHandshakesPerSource < 1 || maxHandshakesPerSource > maxPendingHandshakes)
            throw new ArgumentOutOfRangeException(nameof(maxHandshakesPerSource), $"The value of the parameter: {nameof(maxHandshakesPerSource)} must be from 1 to {nameof(maxPendingHandshakes)}");
        if (refusalLogInterval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(refusalLogInterval), $"The value of the parameter: {nameof(refusalLogInterval)} cannot be negative");

        // Setting
        MaxConnections = maxConnections;
        MaxPendingHandshakes = maxPendingHandshakes;
        MaxHandshakesPerSource = maxHandshakesPerSource;
        RefusalLogInterval = refusalLogInterval;
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
