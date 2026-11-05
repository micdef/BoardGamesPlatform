/*
 *	@file	test/BoardGamesPlatform.TestKit/TestRequestClients.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Opens a request client on a listening tier the way a real client does: connection, then handshake
 */

// Using
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Request clients for the tests that play the tier in front of a listening tier
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a listening tier answers no request before the handshake of the connection; the tests that talk to it with a bare
/// request client open their connection here, so that they all do it the same way as the link client.</para>
/// </remarks>
public static class TestRequestClients
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

    /// <summary>
    /// Method used to connect to a listening tier, run the handshake and return a request client on the connection (Asynchronous)
    /// </summary>
    /// <param name="host">[Cannot be null] Host of the tier</param>
    /// <param name="port">Port of the tier</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The request client, owned by the caller, who disposes it
    /// </returns>
    public static async Task<RequestClient> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        // Connecting, then agreeing on the protocol (a failed handshake closes the connection)
        FrameCodec codec = new FrameCodec();
        ITransportConnection connection = await new TcpTransportConnector().ConnectAsync(host, port, cancellationToken);
        HandshakeResult handshake;
        try
        {
            handshake = await ProtocolHandshake.RunClientAsync(connection, codec, cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        // Function's return
        return new RequestClient(connection, codec, handshake.MaxConcurrentRequests);
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
