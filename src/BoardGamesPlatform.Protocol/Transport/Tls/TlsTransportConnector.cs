/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsTransportConnector.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link protected by mutual TLS 1.3: opens TCP, then a TLS session with the tier expected only
 */

// Using
using System.Net.Security;
using System.Security.Authentication;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Connector of the side that opens a link (the Gateway to the Back, the Back to the Very Back), over mutual TLS 1.3
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> from 0.5.0 no byte of a link travels unencrypted, and a tier only talks to the tier it expects.</para>
/// <para><b>Behavior:</b> opens the TCP connection, then the TLS session: TLS 1.3 only, the certificate of the tier given to the server, the one
/// of the server checked by <see cref="PeerCertificateValidator"/> for the role expected (its name is the one of that role, whatever the address
/// connected to). A refused server ends with an <see cref="AuthenticationException"/>, logged with the reason (event id 9034); the TCP connection
/// is then closed. In TLS 1.3 the server may refuse the certificate of this tier after the handshake ends here: that refusal then shows on the
/// first read, as a failure of the connection. No session is resumed: every connection runs the full handshake, so that every certificate
/// is checked again (a resumed session would skip the check of the refused list and of the validity). The token of the caller bounds both
/// steps.</para>
/// </remarks>
public sealed partial class TlsTransportConnector : ITransportConnector
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Connector of the TCP connection below the session
    /// </summary>
    private readonly ITransportConnector _inner;

    /// <summary>
    /// Certificate of this tier and roots of the platform
    /// </summary>
    private readonly TlsCredentials _credentials;

    /// <summary>
    /// Role the server must have
    /// </summary>
    private readonly PlatformRole _serverRole;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

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
    /// <param name="inner">[Cannot be null] Connector of the TCP connection below the session</param>
    /// <param name="credentials">[Cannot be null] Certificate of this tier, which must open connections, and roots of the platform</param>
    /// <param name="serverRole">Role the server must have, one that accepts connections</param>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    /// <exception cref="ArgumentException">If this tier never opens connections, or the role given never accepts them</exception>
    public TlsTransportConnector(ITransportConnector inner, TlsCredentials credentials, PlatformRole serverRole, ILogger logger)
    {
        // Checks
        if (inner is null)
            throw new ArgumentNullException(nameof(inner), $"The value of the parameter: {nameof(inner)} cannot be null");
        if (credentials is null)
            throw new ArgumentNullException(nameof(credentials), $"The value of the parameter: {nameof(credentials)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        if (!PlatformIdentity.IsClient(credentials.Role))
            throw new ArgumentException("The role of these credentials never opens a connection", nameof(credentials));
        if (!PlatformIdentity.IsServer(serverRole))
            throw new ArgumentException("This role never accepts a connection", nameof(serverRole));

        // Setting
        _inner = inner;
        _credentials = credentials;
        _serverRole = serverRole;
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a server refused
    /// </summary>
    /// <param name="host">Host connected to</param>
    /// <param name="port">Port connected to</param>
    /// <param name="reason">Why it is refused</param>
    [LoggerMessage(EventId = 9034, Level = LogLevel.Warning, Message = "The server at {Host}:{Port} is refused: {Reason}")]
    private partial void LogServerRefused(string host, int port, string reason);

    /// <summary>
    /// Method used to log a TLS session opened
    /// </summary>
    /// <param name="identity">Identity of the server</param>
    /// <param name="host">Host connected to</param>
    /// <param name="port">Port connected to</param>
    [LoggerMessage(EventId = 9035, Level = LogLevel.Information, Message = "TLS session opened with {Identity} at {Host}:{Port}")]
    private partial void LogOpened(string identity, string host, int port);

    #endregion

    #region Publics

    /// <summary>
    /// Method used to open a connection to the server, and its TLS session
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
    /// <exception cref="AuthenticationException">If the server is not the tier expected, or the TLS handshake fails</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    /// <exception cref="OperationCanceledException">If the connection is canceled</exception>
    public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // Opening the TCP connection, then the session on it; the connection is closed if the session fails
        SslStreamCertificateContext context = _credentials.GetContextForNewConnection();
        ITransportConnection connection = await _inner.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        SslStream stream = new SslStream(connection.Stream, leaveInnerStreamOpen: true);
        string expected = PlatformIdentity.GetDnsName(_serverRole, _credentials.Suffix);
        string? refusal = null;
        try
        {
            SslClientAuthenticationOptions options = new SslClientAuthenticationOptions
            {
                TargetHost = expected,
                EnabledSslProtocols = SslProtocols.Tls13,
                ClientCertificateContext = context,
                CertificateRevocationCheckMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck,
                AllowRenegotiation = false,
                AllowTlsResume = false,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    refusal = PeerCertificateValidator.Validate(certificate, _serverRole, _credentials.Suffix, PlatformIdentity.ServerAuthenticationOid,
                        _credentials.TrustedRoots, _credentials.RefusedThumbprints, _credentials.TimeProvider.GetUtcNow());

                    // Function's return
                    return refusal is null;
                }
            };
            await stream.AuthenticateAsClientAsync(options, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Releasing the session, then the connection in any case; a failure of the release must not hide the failure of the handshake
            if (refusal is not null)
                LogServerRefused(host, port, refusal);
            try
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception disposeFailure) when (disposeFailure is IOException or ObjectDisposedException)
            {
                // The session is broken already: the failure of the handshake is the one to tell
            }
            finally
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            throw;
        }
        LogOpened(expected, host, port);

        // Function's return
        return new TlsTransportConnection(connection, stream, expected, context.TargetCertificate);
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
