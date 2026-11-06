/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Secure/SecureChannelTestKeys.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Keys, entropy and streams the tests of the secure channel share
 */

// Using
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport.Secure;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Secure;

/// <summary>
/// Helpers of the tests of the secure channel
/// </summary>
internal static class SecureChannelTestKeys
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
    /// Method used to make a new signing key of P-256
    /// </summary>
    /// <returns>
    /// The key, with its private part
    /// </returns>
    public static ECDsa CreateSigningKey()
    {
        // Function's return
        return ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }

    /// <summary>
    /// Method used to build the keys of one end
    /// </summary>
    /// <param name="signingKey">[Cannot be null] Signing key of the end; the set takes ownership of it</param>
    /// <param name="peerKeys">[Cannot be null] Signing keys of the peer, whose public parts are pinned</param>
    /// <returns>
    /// The set of keys
    /// </returns>
    public static SigningKeySet CreateSet(ECDsa signingKey, params ECDsa[] peerKeys)
    {
        // Function's return
        return SigningKeySet.Create(signingKey, peerKeys.Select(SigningPublicKey.FromKey), generation: 0);
    }

    /// <summary>
    /// Method used to open two ends of a loopback TCP connection
    /// </summary>
    /// <returns>
    /// The stream of the client and the stream of the server, owned by the caller
    /// </returns>
    public static async Task<(NetworkStream Client, NetworkStream Server)> OpenStreamsAsync()
    {
        // Listening on a port the system chooses, then connecting to it
        using TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        TcpClient client = new TcpClient();
        Task<TcpClient> accepting = listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).AsTask();
        await client.ConnectAsync(IPAddress.Loopback, ((IPEndPoint)listener.LocalEndpoint).Port, TestContext.Current.CancellationToken);
        TcpClient server = await accepting;

        // Function's return
        return (client.GetStream(), server.GetStream());
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
