/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureTransportConnector.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of the Back → Very Back link: mutual TLS, then the handshake of the secure channel
 */

// Using
using System.Net.Security;
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Connector of the Back to the Very Back: a TLS connector, then the secure channel
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> from 0.6.0 the Back talks to a Very Back only when it also holds a signing key that the Back pins.</para>
/// <para><b>Behavior:</b> <see cref="ConnectAsync"/> opens the TLS session of the connector below, then runs the handshake of the channel, within
/// the deadline of the caller (the connect timeout of the link client). A refused server is logged with its reason (event id 9052), and the
/// connection is closed. A connection tells it should be renewed after <see cref="RenewalAge"/> or its renewal threshold of records; the
/// connections of a removed key are closed.</para>
/// </remarks>
public sealed class SecureTransportConnector : ITransportConnector, IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Age from which a connection should be renewed by the client
    /// </summary>
    public static readonly TimeSpan RenewalAge = TimeSpan.FromHours(24);

    #endregion

    #region Variables

    /// <summary>
    /// TLS connector below the channel
    /// </summary>
    private readonly ITransportConnector _inner;

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
    /// Records of a direction from which a renewal is due
    /// </summary>
    private readonly long _renewalThreshold;

    /// <summary>
    /// Open connections, closed for a removed key
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
    /// <param name="inner">[Cannot be null] TLS connector</param>
    /// <param name="keys">[Cannot be null] Keys of this tier</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public SecureTransportConnector(ITransportConnector inner, SigningKeys keys, ILogger logger)
        : this(inner, keys, logger, TimeProvider.System, HandshakeEntropy.System, SecureRecordStream.DefaultHardLimit, SecureRecordStream.DefaultRenewalThreshold)
    {
    }

    /// <summary>
    /// Constructor of the tests: a clock, fixed random values, smaller limits
    /// </summary>
    /// <param name="inner">[Cannot be null] TLS connector</param>
    /// <param name="keys">[Cannot be null] Keys of this tier</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the ages</param>
    /// <param name="entropy">[Cannot be null] Source of the nonces and ephemeral keys</param>
    /// <param name="hardLimit">Hard limit of the records of a direction</param>
    /// <param name="renewalThreshold">Records of a direction from which a renewal is due</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    internal SecureTransportConnector(ITransportConnector inner, SigningKeys keys, ILogger logger, TimeProvider timeProvider, HandshakeEntropy entropy, long hardLimit,
        long renewalThreshold)
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
        _renewalThreshold = renewalThreshold;
        _registry = new SecureConnectionRegistry(keys, maxAge: null, timeProvider, logger);
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
    /// Method used to open a connection to the server, its TLS session, then its secure channel
    /// </summary>
    /// <param name="host">[Cannot be null] Host name or address of the server</param>
    /// <param name="port">Port of the server, from 1 to 65535</param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the caller</param>
    /// <returns>
    /// The open connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the host is null, empty or white spaces</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the port is not between 1 and 65535</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the TCP connection cannot be opened</exception>
    /// <exception cref="System.Security.Authentication.AuthenticationException">If the server is not the tier expected (a
    /// <see cref="SecureChannelException"/> for the secure channel)</exception>
    /// <exception cref="InvalidOperationException">If the connector below is not TLS, or the keys of this tier cannot be used now</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    /// <exception cref="OperationCanceledException">If the connection is canceled</exception>
    public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // Opening the TLS session first; the secure channel only runs inside one
        ITransportConnection established = await _inner.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        SecureConnectionRegistry.Entry? entry = null;
        SessionSecrets? secrets = null;
        SecureRecordStream? stream = null;
        try
        {
            if (established is not TlsTransportConnection { Stream: SslStream session } tls)
                throw new InvalidOperationException("The secure channel runs inside a TLS session only: the connector below must be a TLS connector");

            // Running the handshake of the channel; the connection is registered as soon as the key of its peer is known
            SigningKeySet keys = _keys.GetForNewConnection();
            secrets = await SecureHandshake.RunClientAsync(session, keys, tls.RemoteCertificate, tls.LocalCertificate, _entropy, key =>
            {
                entry = _registry.Register(key);

                // Function's return
                return _keys.IsPinned(key);
            }, cancellationToken).ConfigureAwait(false);
            SigningPublicKey peerKey = secrets.PeerKey;
            string remoteEndPoint = established.RemoteEndPoint.ToString() ?? $"{host}:{port}";
            stream = new SecureRecordStream(session, secrets, isClient: true, _hardLimit, _renewalThreshold,
                reason => SecureChannelLog.RecordFailed(_logger, remoteEndPoint, reason));
            SecureConnectionRegistry.Entry registered = entry ?? throw new InvalidOperationException("The handshake ended without registering its connection");
            SecureTransportConnection connection = new SecureTransportConnection(established, stream, peerKey, RenewalAge, _timeProvider, _logger, registered.Remove);
            if (!registered.Attach(connection))
                throw new SecureChannelException(SecureChannelFailure.PeerKeyRemoved);
            SecureChannelLog.Opened(_logger, remoteEndPoint, peerKey.KeyIdHex);

            // Function's return
            return connection;
        }
        catch (Exception exception)
        {
            // Logging the refusal (a cancellation is the deadline of the caller, told by it), then erasing the keys and closing the connection in
            // any case; a failure of that close is logged, and never hides the refusal
            entry?.Remove();
            if (exception is not OperationCanceledException)
            {
                (_, string reason, LogLevel level) = SecureChannelLog.Describe(exception);
                SecureChannelLog.ClientRefused(_logger, level, exception, host, port, reason);
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
                await established.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeFailure) when (disposeFailure is not OutOfMemoryException)
            {
                SecureChannelLog.CloseFailed(_logger, disposeFailure, host, port);
            }

            // Keys of this tier that cannot be used now are a refusal of the link for the caller (link.unavailable), as any refused peer; any
            // other failure keeps its own type and message
            if (exception is SigningKeysUnusableException)
                throw new System.Security.Authentication.AuthenticationException("The keys of the secure channel of this tier cannot be used now", exception);
            throw;
        }
    }

    /// <summary>
    /// Method used to stop watching the removed keys
    /// </summary>
    public void Dispose()
    {
        // Stopping the register; the open connections stay to their owners
        _registry.Dispose();
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
