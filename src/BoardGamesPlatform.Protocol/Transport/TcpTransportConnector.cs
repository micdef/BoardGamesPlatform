/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/TcpTransportConnector.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a plain TCP link
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
/// Plain TCP connector of a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the client side of the transport of 0.3.0 and 0.4.0, development only.</para>
/// <para><b>Behavior:</b> each call opens a new connection. When the host name has several addresses (for example IPv6 and IPv4 for
/// "localhost"), they are tried in turn. The connector holds no resource and can be shared.</para>
/// </remarks>
public sealed class TcpTransportConnector : ITransportConnector
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    // #N/A

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

    // #N/A

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
    public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // Checks
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentNullException(nameof(host), $"The value of the parameter: {nameof(host)} cannot be null or empty");
        if (port < 1 || port > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(port), port, $"The value of the parameter: {nameof(port)} must be between 1 and {IPEndPoint.MaxPort}");

        // Connecting, the client being released if the connection fails or is canceled
        TcpClient client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);

            // Function's return
            return new TcpTransportConnection(client);
        }
        catch
        {
            client.Dispose();
            throw;
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
