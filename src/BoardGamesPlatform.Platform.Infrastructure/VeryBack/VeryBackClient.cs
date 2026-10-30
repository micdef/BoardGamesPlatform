/*
 *	@file	src/BoardGamesPlatform.Platform.Infrastructure/VeryBack/VeryBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client of the Very Back used by the Back: opens the link on first use and calls its operations
 */

// Using
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.VeryBack;

/// <summary>
/// Client used by the Back to call the operations of the Very Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> gives the Back one place to reach the Very Back, hiding the connection, the framing and the messages.</para>
/// <para><b>Behavior:</b> the connection is managed by a <see cref="LinkClient"/>: it opens on the first call, within the connect timeout of the
/// options, is shared by all the concurrent calls, and a new one opens on the next call once it is lost. A request that fails is never retried
/// here (retries come with 0.4.0). The settings are copied once validated, at construction: changing the settings object afterwards has no
/// effect. After <see cref="DisposeAsync"/>, every call throws an <see cref="ObjectDisposedException"/>. The logs give the address of the Very
/// Back only.</para>
/// </remarks>
public sealed class VeryBackClient : IAsyncDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the Very Back in the logs and the messages
    /// </summary>
    private const string PeerName = "the Very Back";

    #endregion

    #region Variables

    /// <summary>
    /// Connection management of the link to the Very Back
    /// </summary>
    private readonly LinkClient _link;

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

    /// <summary>
    /// Constructor; opens nothing, the connection opens on the first call
    /// </summary>
    /// <param name="options">[Cannot be null] Settings of the link; checked here, so that a wrong setting stops the Back at startup</param>
    /// <param name="connector">[Cannot be null] Connector used to open the connection</param>
    /// <param name="logger">[Cannot be null] Logger of the client</param>
    /// <exception cref="ArgumentNullException">If the options, the connector or the logger is null</exception>
    /// <exception cref="InvalidOperationException">If a setting is invalid</exception>
    public VeryBackClient(VeryBackClientOptions options, ITransportConnector connector, ILogger<VeryBackClient> logger)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (connector is null)
            throw new ArgumentNullException(nameof(connector), $"The value of the parameter: {nameof(connector)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        options.Validate();

        // Setting (the link copies the validated values)
        _link = new LinkClient(options.Host, options.Port, options.ConnectTimeout, PeerName, connector, logger);
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
    /// Method used to ask the Very Back whether the chain of tiers answers (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves that the Back reaches the Very Back, and the Very Back its database.</para>
    /// <para><b>Behavior:</b> sends the Ping operation on the shared connection, opening it first when needed. The request is not retried
    /// when it fails.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to stop waiting, for the connection as for the response</param>
    /// <returns>
    /// The time of the SQL Server, as answered by the Very Back, with its offset (the message of the Very Back stays inside this client)
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the Very Back cannot be reached</exception>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="IOException">If the connection closes before the response arrives</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    public async Task<DateTimeOffset> PingAsync(CancellationToken cancellationToken = default)
    {
        // Sending the Ping operation on the shared connection
        PingResponse response = await _link.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken).ConfigureAwait(false);

        // Function's return
        return response.ServerTime;
    }

    /// <summary>
    /// Method used to close the connection; the requests in progress fail with an <see cref="ObjectDisposedException"/>
    /// </summary>
    /// <returns>
    /// A task that completes when the connection is closed
    /// </returns>
    public ValueTask DisposeAsync()
    {
        // Function's return
        return _link.DisposeAsync();
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
