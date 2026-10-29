/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/TcpTransportListener.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a plain TCP link
 */

// Using
using System.Net;
using System.Net.Sockets;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport;

/// <summary>
/// Plain TCP listener of a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the server side of the transport of 0.3.0 and 0.4.0, development only.</para>
/// <para><b>Behavior:</b> created by <see cref="Start"/>, already listening. Binding port 0 lets the system choose a free port, read back in
/// <see cref="LocalEndPoint"/>: this is how the tests listen without reserving a port in advance. Disposing the listener stops it; a wait
/// still pending then ends with an <see cref="ObjectDisposedException"/>, so that an accept loop tells a stop from a failure.
/// A listener bound to the IPv6 "any" address accepts IPv6 connections only.</para>
/// </remarks>
public sealed class TcpTransportListener : ITransportListener
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// TCP listener of the link
    /// </summary>
    private readonly TcpListener _listener;

    /// <summary>
    /// Backing field of <see cref="LocalEndPoint"/>, read once the listener is started
    /// </summary>
    private readonly IPEndPoint _localEndPoint;

    /// <summary>
    /// Whether the listener has been disposed; volatile, since it is set by the thread that disposes and read by the one that accepts
    /// </summary>
    private volatile bool _isDisposed;

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
    public IPEndPoint LocalEndPoint => _localEndPoint;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, used by <see cref="Start"/> once the listener is started
    /// </summary>
    /// <param name="listener">[Cannot be null] Started TCP listener; the instance takes ownership of it</param>
    private TcpTransportListener(TcpListener listener)
    {
        // Setting
        _listener = listener;
        _localEndPoint = (IPEndPoint)listener.LocalEndpoint;
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
    /// Method used to start listening on an address and a port
    /// </summary>
    /// <param name="localEndPoint">[Cannot be null] Address and port to bind; port 0 lets the system choose a free port. The tiers bind
    /// the loopback address unless configured otherwise.</param>
    /// <returns>
    /// The listener, already listening, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the end point is null</exception>
    /// <exception cref="SocketException">If the address and port cannot be bound, for example when another process already uses the port</exception>
    public static TcpTransportListener Start(IPEndPoint localEndPoint)
    {
        // Check
        if (localEndPoint is null)
            throw new ArgumentNullException(nameof(localEndPoint), $"The value of the parameter: {nameof(localEndPoint)} cannot be null");

        // Starting the listener, which is released if the port cannot be bound
        TcpListener listener = new TcpListener(localEndPoint);
        try
        {
            listener.Start();
        }
        catch
        {
            listener.Dispose();
            throw;
        }

        // Function's return
        return new TcpTransportListener(listener);
    }

    /// <summary>
    /// Method used to wait for the next incoming connection
    /// </summary>
    /// <param name="cancellationToken">Token used to stop waiting</param>
    /// <returns>
    /// The accepted connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the listener is disposed, before or during the wait</exception>
    /// <exception cref="OperationCanceledException">If the wait is canceled; the listener can still be used</exception>
    /// <exception cref="SocketException">If the system fails to accept one incoming connection; the listener can still be used</exception>
    public async Task<ITransportConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        // Check
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        // Waiting for a client; once the listener is disposed, the system reports the stop as a socket error (pending wait) or as an
        // invalid operation (wait started right after the check): both become the same ObjectDisposedException
        TcpClient client;
        try
        {
            client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (_isDisposed && exception is SocketException or InvalidOperationException)
        {
            throw new ObjectDisposedException("The listener has been disposed while waiting for a connection", exception);
        }

        // Setting up the connection, the client being released if it fails
        try
        {
            // Function's return
            return new TcpTransportConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Method used to stop listening
    /// </summary>
    /// <returns>
    /// A completed task
    /// </returns>
    public ValueTask DisposeAsync()
    {
        // Stopping the listener
        _isDisposed = true;
        _listener.Dispose();

        // Function's return
        return ValueTask.CompletedTask;
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
