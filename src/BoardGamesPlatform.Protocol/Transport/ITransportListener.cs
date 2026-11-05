/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/ITransportListener.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link: waits for the connections of the tier in front of it
 */

// Using
using System.Net;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport;

/// <summary>
/// Interface of the server side of a link (the Back for the Gateway, the Very Back for the Back)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a tier accept connections without knowing whether they are plain TCP or TLS.</para>
/// <para><b>Behavior:</b> the listener is already listening when it is created. Disposing it stops accepting new connections; the
/// connections already accepted stay open until they are disposed themselves.</para>
/// <para><b>Accept loop rule:</b> an <see cref="ObjectDisposedException"/> or an <see cref="OperationCanceledException"/> means that the
/// listener is closed or that the tier stops: the loop ends. Any other exception concerns one incoming connection only (a peer that resets
/// at once): the loop logs it and keeps accepting. An accepted connection may already be dead: the code that serves it follows the connection
/// rule of the frame codec.</para>
/// <para><b>Two steps:</b> <see cref="AcceptAsync"/> only takes the next connection, at once; <see cref="EstablishAsync"/> then authenticates it
/// (the TLS handshake), in the task that serves that connection, so that a slow or hostile peer never holds the accept loop.</para>
/// </remarks>
public interface ITransportListener : IAsyncDisposable
{
    #region Enumerations

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members

    /// <summary>
    /// Address and port the listener is bound to; when port 0 was requested, the port given by the system
    /// </summary>
    public IPEndPoint LocalEndPoint { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Method used to wait for the next incoming connection
    /// </summary>
    /// <param name="cancellationToken">Token used to stop waiting</param>
    /// <returns>
    /// The accepted connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="OperationCanceledException">If the wait is canceled; the listener can still be used</exception>
    /// <exception cref="ObjectDisposedException">If the listener is disposed, before or during the wait</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the system fails to accept one incoming connection; the listener can still be used</exception>
    public Task<ITransportConnection> AcceptAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Method used to make an accepted connection ready to carry frames: authenticated on TLS, as it is on plain TCP
    /// </summary>
    /// <param name="accepted">[Cannot be null] Connection returned by <see cref="AcceptAsync"/></param>
    /// <param name="cancellationToken">Token used to give up, with the deadline of the handshake</param>
    /// <returns>
    /// The connection to serve: it owns the accepted one, and disposing it closes both. On a failure, the accepted connection stays owned by
    /// the caller, who disposes it.
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="System.Security.Authentication.AuthenticationException">If the peer is not one this listener accepts (TLS)</exception>
    /// <exception cref="IOException">If the connection fails during the handshake</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public Task<ITransportConnection> EstablishAsync(ITransportConnection accepted, CancellationToken cancellationToken = default);

    #endregion
}
