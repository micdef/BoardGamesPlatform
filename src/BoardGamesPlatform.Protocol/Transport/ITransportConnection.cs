/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/ITransportConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	One open connection of a link, seen as a byte stream
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
/// Interface of one open connection between two tiers of the platform
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> hides how the bytes travel (plain TCP in 0.3.0 and 0.4.0, mutual TLS 1.3 from 0.5.0) from the layers above, which
/// only read and write frames on <see cref="Stream"/> with the <see cref="Framing.FrameCodec"/>.</para>
/// <para><b>Behavior:</b> disposing the connection closes it and its stream. The connection rule of the codec applies: after any exception
/// on the stream, the connection is disposed, never reused.</para>
/// </remarks>
public interface ITransportConnection : IAsyncDisposable
{
    #region Enumerations

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members

    /// <summary>
    /// Stream of the connection, read and written by the frame codec
    /// </summary>
    public Stream Stream { get; }

    /// <summary>
    /// Address and port of the other end of the connection, for the logs
    /// </summary>
    public EndPoint RemoteEndPoint { get; }

    /// <summary>
    /// Identity of the other end, proven by its certificate (for example "gateway.bgp.internal"); null on a connection that authenticates
    /// nobody (plain TCP)
    /// </summary>
    public string? PeerIdentity { get; }

    /// <summary>
    /// Method used to stop sending, while still reading what the other end sends
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the other end reads the end of the stream after the bytes already written, and may go on answering; this end can
    /// still read. On TLS, the end of the session is announced first (close_notify), so that the other end tells a clean end from a cut.
    /// Nothing may be written afterwards.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once the end of sending is written
    /// </returns>
    /// <exception cref="IOException">If the stream fails</exception>
    /// <exception cref="ObjectDisposedException">If the connection is closed</exception>
    public Task CloseSendAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Methods

    // #N/A

    #endregion
}
