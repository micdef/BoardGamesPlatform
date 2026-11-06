/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureConnectionRegistry.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connections of the secure channel of a tier, closed when the key of their peer is removed or when they are too old
 */

// Using
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Register of the open connections of one listener or connector of the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> removing a key revokes it, established connections included; and a server never keeps a connection longer than its
/// age limit, in case a client never renews it.</para>
/// <para><b>Behavior:</b> a handshake registers its connection as soon as it knows the key of its peer, before its last check that the key is
/// still pinned, so that a reload during the handshake is never missed: a key removed meanwhile marks the entry, and the connection is closed
/// as soon as it is attached. A connection is closed with its close record first (<see cref="SecureTransportConnection.CloseForAsync"/>). The
/// age is checked every <see cref="SigningKeys.CheckInterval"/> when an age limit is given. The logs use the event ids 9061 and 9062.</para>
/// </remarks>
internal sealed partial class SecureConnectionRegistry : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Keys whose removals are watched
    /// </summary>
    private readonly SigningKeys _keys;

    /// <summary>
    /// Age from which a connection is closed; null for none
    /// </summary>
    private readonly TimeSpan? _maxAge;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Lock of the entries
    /// </summary>
    private readonly Lock _lock = new Lock();

    /// <summary>
    /// Entries of the connections in their handshake or open
    /// </summary>
    private readonly HashSet<Entry> _entries = new HashSet<Entry>();

    /// <summary>
    /// Timer of the check of the ages; null without an age limit
    /// </summary>
    private readonly ITimer? _timer;

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
    /// Number of connections registered, for the tests
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                // Function's return
                return _entries.Count;
            }
        }
    }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="keys">[Cannot be null] Keys of the tier, whose removed keys close their connections</param>
    /// <param name="maxAge">Age from which a connection is closed; null for none</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the check of the ages</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter that cannot be null is null</exception>
    public SecureConnectionRegistry(SigningKeys keys, TimeSpan? maxAge, TimeProvider timeProvider, ILogger logger)
    {
        // Checks
        if (keys is null)
            throw new ArgumentNullException(nameof(keys), $"The value of the parameter: {nameof(keys)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting, then watching the removed keys and the ages
        _keys = keys;
        _maxAge = maxAge;
        _logger = logger;
        _keys.PeerKeysRemoved += OnPeerKeysRemoved;
        if (maxAge is not null)
            _timer = timeProvider.CreateTimer(_ => CloseTooOld(), null, SigningKeys.CheckInterval, SigningKeys.CheckInterval);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a connection closed because the key of its peer was removed
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="keyId">Key id removed</param>
    [LoggerMessage(EventId = 9061, Level = LogLevel.Information, Message = "Connection with {RemoteEndPoint} closed: the key {KeyId} of the peer was removed")]
    private partial void LogClosedForRemovedKey(string remoteEndPoint, string keyId);

    /// <summary>
    /// Method used to log a connection closed for its age
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    /// <param name="maxAge">Age limit</param>
    [LoggerMessage(EventId = 9062, Level = LogLevel.Information, Message = "Connection with {RemoteEndPoint} closed: it is older than {MaxAge}, and was never renewed")]
    private partial void LogClosedForAge(string remoteEndPoint, TimeSpan maxAge);

    /// <summary>
    /// Method used to mark the entries of the removed keys, and close their connections
    /// </summary>
    /// <param name="sender">Keys of the tier</param>
    /// <param name="removed">[Cannot be null] Keys removed</param>
    private void OnPeerKeysRemoved(object? sender, PeerKeysRemovedEventArgs removed)
    {
        // Marking every entry of a removed key, the ones still in their handshake included
        List<SecureTransportConnection> toClose = new List<SecureTransportConnection>();
        lock (_lock)
        {
            foreach (Entry entry in _entries.Where(entry => removed.RemovedKeys.Contains(entry.PeerKey)))
            {
                entry.Revoke();
                if (entry.Connection is not null)
                    toClose.Add(entry.Connection);
            }
        }

        // Closing the open ones, outside the lock
        foreach (SecureTransportConnection connection in toClose)
        {
            LogClosedForRemovedKey(connection.RemoteName, connection.PeerKey.KeyIdHex);
            _ = connection.CloseForAsync();
        }
    }

    /// <summary>
    /// Method used to close the connections older than the age limit
    /// </summary>
    private void CloseTooOld()
    {
        // Collecting them, then closing them outside the lock
        TimeSpan maxAge = _maxAge!.Value;
        List<SecureTransportConnection> toClose;
        lock (_lock)
            toClose = _entries.Select(entry => entry.Connection).OfType<SecureTransportConnection>().Where(connection => connection.IsTooOld(maxAge)).ToList();
        foreach (SecureTransportConnection connection in toClose)
        {
            LogClosedForAge(connection.RemoteName, maxAge);
            _ = connection.CloseForAsync();
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to register a connection in its handshake, as soon as the key of its peer is known
    /// </summary>
    /// <param name="peerKey">[Cannot be null] Pinned key named by the peer</param>
    /// <returns>
    /// The entry, to attach the connection to once it is built, or to remove when the handshake fails
    /// </returns>
    public Entry Register(SigningPublicKey peerKey)
    {
        // Adding the entry
        Entry entry = new Entry(this, peerKey);
        lock (_lock)
            _entries.Add(entry);

        // Function's return
        return entry;
    }

    /// <summary>
    /// Method used to stop watching
    /// </summary>
    public void Dispose()
    {
        // Stopping the timer and the event
        _timer?.Dispose();
        _keys.PeerKeysRemoved -= OnPeerKeysRemoved;
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

    /// <summary>
    /// Entry of one connection, from the moment the key of its peer is known
    /// </summary>
    internal sealed class Entry
    {
        /// <summary>
        /// Register of the entry
        /// </summary>
        private readonly SecureConnectionRegistry _registry;

        /// <summary>
        /// Whether the key of the peer was removed; under the lock of the register
        /// </summary>
        private bool _isRevoked;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="registry">[Cannot be null] Register of the entry</param>
        /// <param name="peerKey">[Cannot be null] Pinned key of the peer</param>
        public Entry(SecureConnectionRegistry registry, SigningPublicKey peerKey)
        {
            // Setting
            _registry = registry;
            PeerKey = peerKey;
        }

        /// <summary>
        /// Pinned key of the peer
        /// </summary>
        public SigningPublicKey PeerKey { get; }

        /// <summary>
        /// Connection, once attached; under the lock of the register
        /// </summary>
        public SecureTransportConnection? Connection { get; private set; }

        /// <summary>
        /// Method used to mark the entry of a removed key; under the lock of the register
        /// </summary>
        public void Revoke()
        {
            // Setting
            _isRevoked = true;
        }

        /// <summary>
        /// Method used to attach the connection built at the end of the handshake, unless its key was removed meanwhile
        /// </summary>
        /// <param name="connection">[Cannot be null] Connection of the entry</param>
        /// <returns>
        /// True when the connection may be used; false when its key was removed during the handshake: the caller closes it
        /// </returns>
        public bool Attach(SecureTransportConnection connection)
        {
            // Attaching, unless the key was removed during the handshake
            bool isRevoked;
            lock (_registry._lock)
            {
                isRevoked = _isRevoked;
                if (!isRevoked)
                    Connection = connection;
            }
            if (isRevoked)
                _registry.LogClosedForRemovedKey(connection.RemoteName, PeerKey.KeyIdHex);

            // Function's return
            return !isRevoked;
        }

        /// <summary>
        /// Method used to remove the entry, once its connection is closed or its handshake failed
        /// </summary>
        public void Remove()
        {
            // Removing it
            lock (_registry._lock)
                _registry._entries.Remove(this);
        }
    }
}
