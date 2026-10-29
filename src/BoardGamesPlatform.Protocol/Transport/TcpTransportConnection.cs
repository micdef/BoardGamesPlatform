/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/TcpTransportConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	One open plain TCP connection, seen as a byte stream
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
/// Plain TCP connection between two tiers, created by <see cref="TcpTransportListener"/> or <see cref="TcpTransportConnector"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the transport of 0.3.0 and 0.4.0, development only: the bytes travel unencrypted.</para>
/// <para><b>Behavior:</b> takes ownership of the TCP client and disables the Nagle algorithm: a request is a small frame sent in one write,
/// and waiting to group it with the next one would only delay the response. An IPv4 address seen through an IPv6 socket
/// (<c>::ffff:127.0.0.1</c>) is given back as IPv4, so that both ends of a connection report the same form.</para>
/// <para>Disposing the connection closes the stream and the socket at once; disposing it twice does nothing more. When bytes sent by the
/// peer are still unread, the system resets the connection, and the peer may lose what this end wrote just before: a graceful close
/// comes with 0.4.0.</para>
/// </remarks>
internal sealed class TcpTransportConnection : ITransportConnection
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// TCP client owning the socket of the connection
    /// </summary>
    private readonly TcpClient _client;

    /// <summary>
    /// Stream of the socket
    /// </summary>
    private readonly NetworkStream _stream;

    /// <summary>
    /// Backing field of <see cref="RemoteEndPoint"/>, read once, since the socket no longer gives it after it is closed
    /// </summary>
    private readonly EndPoint _remoteEndPoint;

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
    public EndPoint RemoteEndPoint => _remoteEndPoint;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="client">[Cannot be null] Connected TCP client; the connection takes ownership of it</param>
    /// <exception cref="ArgumentNullException">If the client is null</exception>
    /// <exception cref="InvalidOperationException">If the client is disposed or not connected</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the peer has already reset the connection</exception>
    public TcpTransportConnection(TcpClient client)
    {
        // Checks
        if (client is null)
            throw new ArgumentNullException(nameof(client), $"The value of the parameter: {nameof(client)} cannot be null");
        if (client.Client is null || !client.Connected)
            throw new InvalidOperationException("The TCP client is disposed or not connected");

        // Setting the socket: no Nagle delay, then its stream and the address of the peer
        client.NoDelay = true;
        _client = client;
        _stream = client.GetStream();
        _remoteEndPoint = ToSameAddressForm(client.Client.RemoteEndPoint
            ?? throw new InvalidOperationException("The TCP client has no remote end point: it is not connected"));
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to give an IPv4 address seen through an IPv6 socket back in its IPv4 form
    /// </summary>
    /// <param name="endPoint">[Cannot be null] End point read from the socket</param>
    /// <returns>
    /// The same end point, with its address as IPv4 when it was an IPv4-mapped IPv6 address
    /// </returns>
    private static EndPoint ToSameAddressForm(EndPoint endPoint)
    {
        // Keeping any end point that is not an IPv4-mapped IPv6 address
        if (endPoint is not IPEndPoint ipEndPoint || !ipEndPoint.Address.IsIPv4MappedToIPv6)
        {
            // Function's return
            return endPoint;
        }

        // Function's return
        return new IPEndPoint(ipEndPoint.Address.MapToIPv4(), ipEndPoint.Port);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to close the connection, its stream and its socket
    /// </summary>
    /// <returns>
    /// A completed task: closing a socket does not wait for the peer
    /// </returns>
    public ValueTask DisposeAsync()
    {
        // Closing the stream, then the client
        _stream.Dispose();
        _client.Dispose();

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
