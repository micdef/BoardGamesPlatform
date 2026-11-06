/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureTransportListener.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of the Back → Very Back link: mutual TLS, then the handshake of the secure channel
 */

// Using
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using BoardGamesPlatform.Protocol.Logging;
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Listener of the Very Back: a TLS listener, then the secure channel on each connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> from 0.6.0 a connection reaches the protocol of the Very Back only when its client also holds a signing key that the
/// Very Back pins (<c>doc/dev/secure-channel.md</c>).</para>
/// <para><b>Behavior:</b> <see cref="AcceptAsync"/> takes the next TCP connection at once; <see cref="EstablishAsync"/> then runs the TLS handshake
/// of the listener below, then the handshake of the channel, in the task that serves the connection, within the deadline of the caller (the
/// handshake timeout of the link server). A refused handshake is logged with its reason (event id 9051), at most once every
/// <see cref="LogPace.DefaultInterval"/>; nothing is sent to the client. A connection open for <see cref="MaxAge"/> is closed, in case its
/// client never renews it, and the connections of a removed key are closed.</para>
/// </remarks>
public sealed class SecureTransportListener : ITransportListener
{
    #region Members

    #region Constants

    /// <summary>
    /// Age from which the server closes a connection
    /// </summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(48);

    #endregion

    #region Variables

    /// <summary>
    /// TLS listener below the channel
    /// </summary>
    private readonly ITransportListener _inner;

    /// <summary>
    /// Keys of this tier
    /// </summary>
    private readonly SigningKeys _keys;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Clock of the ages
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Source of the nonces and ephemeral keys
    /// </summary>
    private readonly HandshakeEntropy _entropy;

    /// <summary>
    /// Hard limit of the records of a direction
    /// </summary>
    private readonly long _hardLimit;

    /// <summary>
    /// Pace of the logs of the handshakes that fail, which a peer can trigger at will, one per level and reason so that a frequent reason
    /// never hides a rare one
    /// </summary>
    private readonly ConcurrentDictionary<string, LogPace> _failurePaces = new ConcurrentDictionary<string, LogPace>(StringComparer.Ordinal);

    /// <summary>
    /// Open connections, closed for a removed key or their age
    /// </summary>
    private readonly SecureConnectionRegistry _registry;

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

    /// <inheritdoc/>
    public IPEndPoint LocalEndPoint => _inner.LocalEndPoint;

    /// <summary>
    /// Number of connections in their handshake or open, for the tests
    /// </summary>
    internal int ConnectionCount => _registry.Count;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] TLS listener, already listening; this listener takes ownership of it</param>
    /// <param name="keys">[Cannot be null] Keys of this tier</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public SecureTransportListener(ITransportListener inner, SigningKeys keys, ILogger logger)
        : this(inner, keys, logger, TimeProvider.System, HandshakeEntropy.System, SecureRecordStream.DefaultHardLimit, MaxAge)
    {
    }

    /// <summary>
    /// Constructor of the tests: a clock, fixed random values, smaller limits
    /// </summary>
    /// <param name="inner">[Cannot be null] TLS listener, already listening; this listener takes ownership of it</param>
    /// <param name="keys">[Cannot be null] Keys of this tier</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the ages</param>
    /// <param name="entropy">[Cannot be null] Source of the nonces and ephemeral keys</param>
    /// <param name="hardLimit">Hard limit of the records of a direction</param>
    /// <param name="maxAge">Age from which a connection is closed</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    internal SecureTransportListener(ITransportListener inner, SigningKeys keys, ILogger logger, TimeProvider timeProvider, HandshakeEntropy entropy, long hardLimit,
        TimeSpan maxAge)
    {
        // Checks
        if (inner is null)
            throw new ArgumentNullException(nameof(inner), $"The value of the parameter: {nameof(inner)} cannot be null");
        if (keys is null)
            throw new ArgumentNullException(nameof(keys), $"The value of the parameter: {nameof(keys)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (entropy is null)
            throw new ArgumentNullException(nameof(entropy), $"The value of the parameter: {nameof(entropy)} cannot be null");

        // Setting
        _inner = inner;
        _keys = keys;
        _logger = logger;
        _timeProvider = timeProvider;
        _entropy = entropy;
        _hardLimit = hardLimit;
        _registry = new SecureConnectionRegistry(keys, maxAge, timeProvider, logger);
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

    /// <inheritdoc/>
    public Task<ITransportConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        // Function's return: the TCP connection, authenticated later by EstablishAsync
        return _inner.AcceptAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run the TLS handshake, then the handshake of the secure channel, on an accepted connection
    /// </summary>
    /// <param name="accepted">[Cannot be null] Connection returned by <see cref="AcceptAsync"/></param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the handshake</param>
    /// <returns>
    /// The connection to serve, which owns the accepted one; on a failure, the accepted connection stays owned by the caller
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="System.Security.Authentication.AuthenticationException">If the client is not the tier expected (a
    /// <see cref="SecureChannelException"/> for the secure channel)</exception>
    /// <exception cref="InvalidOperationException">If the listener below is not TLS, or the keys of this tier cannot be used now</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public async Task<ITransportConnection> EstablishAsync(ITransportConnection accepted, CancellationToken cancellationToken = default)
    {
        // Check
        if (accepted is null)
            throw new ArgumentNullException(nameof(accepted), $"The value of the parameter: {nameof(accepted)} cannot be null");

        // Establishing TLS first; the secure channel only runs inside a TLS session, whose certificates go into its transcript
        string remoteEndPoint = accepted.RemoteEndPoint.ToString() ?? "unknown";
        ITransportConnection established = await _inner.EstablishAsync(accepted, cancellationToken).ConfigureAwait(false);
        if (established is not TlsTransportConnection { Stream: SslStream session } tls)
        {
            await established.Stream.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("The secure channel runs inside a TLS session only: the listener below must be a TLS listener");
        }

        // Running the handshake of the channel; the connection is registered as soon as the key of its peer is known
        SecureConnectionRegistry.Entry? entry = null;
        SessionSecrets? secrets = null;
        SecureRecordStream? stream = null;
        try
        {
            SigningKeySet keys = _keys.GetForNewConnection();
            secrets = await SecureHandshake.RunServerAsync(session, keys, tls.LocalCertificate, tls.RemoteCertificate, _entropy, key =>
            {
                entry = _registry.Register(key);

                // Function's return
                return _keys.IsPinned(key);
            }, cancellationToken).ConfigureAwait(false);
            SigningPublicKey peerKey = secrets.PeerKey;
            stream = new SecureRecordStream(session, secrets, isClient: false, _hardLimit, SecureRecordStream.DefaultRenewalThreshold,
                reason => SecureChannelLog.RecordFailed(_logger, remoteEndPoint, reason));
            SecureConnectionRegistry.Entry registered = entry ?? throw new InvalidOperationException("The handshake ended without registering its connection");
            SecureTransportConnection connection = new SecureTransportConnection(established, stream, peerKey, MaxAge, _timeProvider, _logger, registered.Remove);
            if (!registered.Attach(connection))
                throw new SecureChannelException(SecureChannelFailure.PeerKeyRemoved);
            SecureChannelLog.Opened(_logger, remoteEndPoint, peerKey.KeyIdHex);

            // Function's return
            return connection;
        }
        catch (Exception exception)
        {
            // Logging the failure at the pace of its reason (a cancellation is the deadline or the stop, told by the caller), so that frequent
            // reasons never hide a rare one; then erasing the keys and releasing the session, the accepted connection staying to its caller
            entry?.Remove();
            if (exception is not OperationCanceledException)
            {
                (string category, string reason, LogLevel level) = SecureChannelLog.Describe(exception);
                if (_failurePaces.GetOrAdd(category, _ => new LogPace(LogPace.DefaultInterval, _timeProvider)).TryEnter(out int heldBack))
                    SecureChannelLog.ServerRefused(_logger, level, exception, remoteEndPoint, reason, heldBack);
            }
            try
            {
                if (stream is not null)
                    await stream.DisposeAsync().ConfigureAwait(false);
                else
                    secrets?.Dispose();
            }
            catch (Exception eraseFailure) when (eraseFailure is not OutOfMemoryException)
            {
                // Erasing buffers does not fail in practice; the refusal is the failure to tell
            }
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeFailure) when (disposeFailure is IOException or ObjectDisposedException)
            {
                // The session is broken already: the failure of the handshake is the one to tell
            }
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        // Stopping the register, then the TLS listener
        _registry.Dispose();
        await _inner.DisposeAsync().ConfigureAwait(false);
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
