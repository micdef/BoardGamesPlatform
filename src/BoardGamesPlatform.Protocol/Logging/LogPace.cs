/*
 *	@file	src/BoardGamesPlatform.Protocol/Logging/LogPace.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Pace of a log that a peer can trigger at will: at most one entry per interval, the entries in between being counted
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Logging;

/// <summary>
/// Pace of a log that a peer can trigger at will, for example the refusal of a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a peer that opens and fails connections in a loop must not flood the logs (the disk, the collector), nor drown the
/// other events.</para>
/// <para><b>Behavior:</b> <see cref="TryEnter"/> lets one entry through per interval, and counts the others; the next entry let through
/// gives that count, so that nothing is lost, only grouped. Thread-safe.</para>
/// </remarks>
internal sealed class LogPace
{
    #region Members

    #region Constants

    /// <summary>
    /// Shortest time between two entries when none is given
    /// </summary>
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    #endregion

    #region Variables

    /// <summary>
    /// Shortest time between two entries
    /// </summary>
    private readonly TimeSpan _interval;

    /// <summary>
    /// Clock of the pace
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Lock of the state of the pace
    /// </summary>
    private readonly Lock _lock = new Lock();

    /// <summary>
    /// Whether an entry was already let through
    /// </summary>
    private bool _hasEntered;

    /// <summary>
    /// Time stamp of the clock of the last entry let through
    /// </summary>
    private long _lastEntry;

    /// <summary>
    /// Number of entries held back since the last one let through
    /// </summary>
    private int _heldBack;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="interval">Shortest time between two entries; zero (every entry let through) or more</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the pace</param>
    /// <exception cref="ArgumentNullException">If the clock is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the interval is negative</exception>
    public LogPace(TimeSpan interval, TimeProvider timeProvider)
    {
        // Checks
        if (interval < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), $"The value of the parameter: {nameof(interval)} cannot be negative");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");

        // Setting
        _interval = interval;
        _timeProvider = timeProvider;
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

    /// <summary>
    /// Method used to ask whether an entry may be logged now
    /// </summary>
    /// <param name="heldBack">Number of entries held back since the last one let through, to give with this one; zero when it is held back</param>
    /// <returns>
    /// True when the entry may be logged; false when it is held back and counted
    /// </returns>
    public bool TryEnter(out int heldBack)
    {
        lock (_lock)
        {
            // Holding the entry back when the last one is too recent
            long now = _timeProvider.GetTimestamp();
            if (_hasEntered && _timeProvider.GetElapsedTime(_lastEntry, now) < _interval)
            {
                _heldBack++;
                heldBack = 0;

                // Function's return
                return false;
            }

            // Letting it through with the count of the ones held back
            heldBack = _heldBack;
            _heldBack = 0;
            _lastEntry = now;
            _hasEntered = true;
        }

        // Function's return
        return true;
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
