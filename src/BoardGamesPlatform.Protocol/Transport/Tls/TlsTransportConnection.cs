/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsTransportConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connection between two tiers protected by mutual TLS 1.3, over a TCP connection
 */

// Using
using System.Net;
using System.Net.Security;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Connection whose bytes travel in a TLS 1.3 session, both ends authenticated by their certificate
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> created once the handshake succeeded, by <see cref="TlsTransportConnector"/> or <see cref="TlsTransportListener"/>. It owns
/// the TCP connection below it: disposing it closes the session, then the socket. Closing the sending side announces the end of the session
/// (close_notify), then sends the end of the TCP stream, the receiving side staying open.</para>
/// </remarks>
internal sealed class TlsTransportConnection : ITransportConnection
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// TCP connection below the session
    /// </summary>
    private readonly ITransportConnection _inner;

    /// <summary>
    /// TLS session
    /// </summary>
    private readonly SslStream _stream;

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
    public Stream Stream => _stream;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => _inner.RemoteEndPoint;

    /// <inheritdoc/>
    public string? PeerIdentity { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] TCP connection below the session; the connection takes ownership of it</param>
    /// <param name="stream">[Cannot be null] TLS session, authenticated</param>
    /// <param name="peerIdentity">[Cannot be null] Identity of the peer, proven by its certificate</param>
    public TlsTransportConnection(ITransportConnection inner, SslStream stream, string peerIdentity)
    {
        // Setting
        _inner = inner;
        _stream = stream;
        PeerIdentity = peerIdentity;
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
    /// Method used to stop sending, while still reading: the peer reads the end of the session, then of the stream
    /// </summary>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once the end of the session and of the stream are sent
    /// </returns>
    /// <exception cref="IOException">If the stream fails</exception>
    /// <exception cref="ObjectDisposedException">If the connection is closed</exception>
    public async Task CloseSendAsync(CancellationToken cancellationToken = default)
    {
        // Announcing the end of the session, then closing the sending side of the socket
        await _stream.ShutdownAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        await _inner.CloseSendAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to close the session and the TCP connection
    /// </summary>
    /// <returns>
    /// A task that completes once both are closed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Closing the session, then the connection below it, whatever the first close did
        try
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
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
