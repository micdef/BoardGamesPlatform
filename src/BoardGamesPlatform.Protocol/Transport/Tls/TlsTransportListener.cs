/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsTransportListener.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link protected by mutual TLS 1.3: accepts TCP, then a TLS session with the tier expected only
 */

// Using
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Logging;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Listener of the side that accepts a link (the Back for the Gateway, the Very Back for the Back), over mutual TLS 1.3
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> from 0.5.0 a connection without the certificate of the tier expected is refused before any frame is read.</para>
/// <para><b>Behavior:</b> <see cref="AcceptAsync"/> takes the next TCP connection at once; <see cref="EstablishAsync"/> then runs the TLS
/// handshake, in the task that serves the connection: TLS 1.3 only, the certificate of this tier given to the client, a client certificate
/// required and checked by <see cref="PeerCertificateValidator"/> for the role expected. A client refused is logged with the reason (event id
/// 9031), any other failure of the handshake too (9033), at most once every <see cref="LogPace.DefaultInterval"/> for both (a peer can fail
/// handshakes in a loop; the next entry gives the number held back), and the handshake ends with an <see cref="AuthenticationException"/>.
/// The connection is first established by the listener below (nothing for TCP), so that a layer below TLS is never skipped. No session is
/// resumed, so that every connection checks the certificate of its client again. The certificate files are taken again before each handshake
/// when they changed (see <see cref="TlsCredentials"/>).</para>
/// </remarks>
public sealed partial class TlsTransportListener : ITransportListener
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Listener of the TCP connections below the sessions
    /// </summary>
    private readonly ITransportListener _inner;

    /// <summary>
    /// Certificate of this tier and roots of the platform
    /// </summary>
    private readonly TlsCredentials _credentials;

    /// <summary>
    /// Role the clients must have
    /// </summary>
    private readonly PlatformRole _clientRole;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Pace of the logs of the handshakes that fail, which a peer can trigger at will
    /// </summary>
    private readonly LogPace _failurePace;

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

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] Listener of the TCP connections, already listening; this listener takes ownership of it</param>
    /// <param name="credentials">[Cannot be null] Certificate of this tier, which must accept connections, and roots of the platform</param>
    /// <param name="clientRole">Role the clients must have, one that opens connections</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If this tier never accepts connections, or the role given never opens them</exception>
    public TlsTransportListener(ITransportListener inner, TlsCredentials credentials, PlatformRole clientRole, ILogger logger)
    {
        // Checks
        if (inner is null)
            throw new ArgumentNullException(nameof(inner), $"The value of the parameter: {nameof(inner)} cannot be null");
        if (credentials is null)
            throw new ArgumentNullException(nameof(credentials), $"The value of the parameter: {nameof(credentials)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (!PlatformIdentity.IsServer(credentials.Role))
            throw new ArgumentException("The role of these credentials never accepts a connection", nameof(credentials));
        if (!PlatformIdentity.IsClient(clientRole))
            throw new ArgumentException("This role never opens a connection", nameof(clientRole));

        // Setting
        _inner = inner;
        _credentials = credentials;
        _clientRole = clientRole;
        _logger = logger;
        _failurePace = new LogPace(LogPace.DefaultInterval, credentials.TimeProvider);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a client refused
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the client</param>
    /// <param name="reason">Why it is refused</param>
    /// <param name="heldBack">Number of failed handshakes not logged since the last one logged</param>
    [LoggerMessage(EventId = 9031, Level = LogLevel.Warning, Message = "Connection from {RemoteEndPoint} refused: {Reason} ({HeldBack} other failed handshakes not logged since the last one)")]
    private partial void LogClientRefused(string remoteEndPoint, string reason, int heldBack);

    /// <summary>
    /// Method used to log a TLS session opened
    /// </summary>
    /// <param name="remoteEndPoint">Address and port of the client</param>
    /// <param name="identity">Identity of the client</param>
    /// <param name="thumbprint">Thumbprint of its certificate</param>
    [LoggerMessage(EventId = 9032, Level = LogLevel.Information, Message = "Connection from {RemoteEndPoint} authenticated as {Identity} ({Thumbprint})")]
    private partial void LogAuthenticated(string remoteEndPoint, string identity, string thumbprint);

    /// <summary>
    /// Method used to log a TLS handshake that failed for another reason than the certificate of the client
    /// </summary>
    /// <param name="exception">Why it failed</param>
    /// <param name="remoteEndPoint">Address and port of the client</param>
    /// <param name="heldBack">Number of failed handshakes not logged since the last one logged</param>
    [LoggerMessage(EventId = 9033, Level = LogLevel.Warning, Message = "The TLS handshake with {RemoteEndPoint} failed ({HeldBack} other failed handshakes not logged since the last one)")]
    private partial void LogHandshakeFailed(Exception exception, string remoteEndPoint, int heldBack);

    #endregion

    #region Publics

    /// <inheritdoc/>
    public Task<ITransportConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        // Function's return: the TCP connection, authenticated later by EstablishAsync
        return _inner.AcceptAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run the TLS handshake on an accepted connection
    /// </summary>
    /// <param name="accepted">[Cannot be null] Connection returned by <see cref="AcceptAsync"/></param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the handshake</param>
    /// <returns>
    /// The connection to serve, which owns the accepted one; on a failure, the accepted connection stays owned by the caller (a listener below
    /// that wraps it must follow the same rule)
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="AuthenticationException">If the client is not the tier expected, or the TLS handshake fails</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public async Task<ITransportConnection> EstablishAsync(ITransportConnection accepted, CancellationToken cancellationToken = default)
    {
        // Check
        if (accepted is null)
            throw new ArgumentNullException(nameof(accepted), $"The value of the parameter: {nameof(accepted)} cannot be null");

        // Establishing the connection below first, then running the handshake on it, the certificate of the client being required and checked;
        // the session is released if it fails
        string remoteEndPoint = accepted.RemoteEndPoint.ToString() ?? "unknown";
        ITransportConnection established = await _inner.EstablishAsync(accepted, cancellationToken).ConfigureAwait(false);
        SslStream stream = new SslStream(established.Stream, leaveInnerStreamOpen: true);
        SslStreamCertificateContext context = _credentials.GetContextForNewConnection();
        string? refusal = null;
        string thumbprint = string.Empty;
        try
        {
            SslServerAuthenticationOptions options = new SslServerAuthenticationOptions
            {
                ServerCertificateContext = context,
                ClientCertificateRequired = true,
                EnabledSslProtocols = SslProtocols.Tls13,
                CertificateRevocationCheckMode = X509RevocationMode.NoCheck,
                AllowRenegotiation = false,
                AllowTlsResume = false,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    refusal = PeerCertificateValidator.Validate(certificate, _clientRole, _credentials.Suffix, PlatformIdentity.ClientAuthenticationOid,
                        _credentials.TrustedRoots, _credentials.RefusedThumbprints, _credentials.TimeProvider.GetUtcNow());
                    if (refusal is null && certificate is not null)
                        thumbprint = Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData()));

                    // Function's return
                    return refusal is null;
                }
            };
            await stream.AuthenticateAsServerAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            // Logging the failure at its pace (a cancellation is the deadline or the stop, told by the caller), then releasing the session; a
            // failure of that release must not hide the failure of the handshake
            if (exception is not OperationCanceledException && _failurePace.TryEnter(out int heldBack))
            {
                if (refusal is not null)
                    LogClientRefused(remoteEndPoint, refusal, heldBack);
                else
                    LogHandshakeFailed(exception, remoteEndPoint, heldBack);
            }
            try
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeFailure) when (disposeFailure is IOException or ObjectDisposedException)
            {
                // The session is broken already: the failure of the handshake is the one to tell
            }
            throw;
        }
        string identity = PlatformIdentity.GetDnsName(_clientRole, _credentials.Suffix);
        LogAuthenticated(remoteEndPoint, identity, thumbprint);

        // Function's return
        return new TlsTransportConnection(established, stream, identity, context.TargetCertificate);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Function's return: stopping the TCP listener
        return _inner.DisposeAsync();
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
