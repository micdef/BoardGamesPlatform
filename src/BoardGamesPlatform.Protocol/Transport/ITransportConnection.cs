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
/// <para><b>Purpose:</b> hides how the bytes travel (plain TCP in 0.3.0, TLS from 0.5.0) from the layers above, which only read and write
/// frames on <see cref="Stream"/> with the <see cref="Framing.FrameCodec"/>.</para>
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

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Methods

    // #N/A

    #endregion
}
