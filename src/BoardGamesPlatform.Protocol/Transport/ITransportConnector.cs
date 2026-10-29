/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/ITransportConnector.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of a link: opens a connection to the tier behind it
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport;

/// <summary>
/// Interface of the client side of a link (the Gateway towards the Back, the Back towards the Very Back)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a tier open connections without knowing whether they are plain TCP or TLS.</para>
/// <para><b>Behavior:</b> the connector has no timeout of its own: the caller passes a token with a deadline, otherwise an unreachable
/// host is waited for as long as the system allows (about 21 seconds on Windows, not measured here).</para>
/// </remarks>
public interface ITransportConnector
{
    #region Enumerations

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Method used to open a connection to a listener
    /// </summary>
    /// <param name="host">[Cannot be null] Host name or address of the listener</param>
    /// <param name="port">Port of the listener, from 1 to 65535</param>
    /// <param name="cancellationToken">Token used to give up the connection</param>
    /// <returns>
    /// The open connection, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the host is null, empty or white spaces</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the port is not between 1 and 65535</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the connection cannot be opened (no listener, unknown host, network failure)</exception>
    /// <exception cref="OperationCanceledException">If the connection is canceled</exception>
    public Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    #endregion
}
