/*
 *	@file	src/BoardGamesPlatform.Gateway/BackLink/BackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client of the Back used by the Gateway: opens the link on first use and calls its operations
 */

// Using
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.BackLink;

/// <summary>
/// Client used by the Gateway to call the operations of the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> gives the Gateway one place to reach the Back, hiding the connection, the framing and the messages.</para>
/// <para><b>Behavior:</b> the connection is managed by a <see cref="LinkClient"/>: it opens on the first call, within the connect timeout of the
/// options, is shared by all the concurrent calls, and a new one opens on the next call once it is lost. A request that fails is never retried
/// here (retries come with 0.4.0). The settings are copied once validated, at construction: changing the settings object afterwards has no
/// effect. After <see cref="DisposeAsync"/>, every call throws an <see cref="ObjectDisposedException"/>. The link is plain TCP, for development
/// only, until TLS comes in 0.5.0. The logs give the address of the Back only. The reconnection after a lost connection and the connect
/// timeout belong to <see cref="LinkClient"/>: they are proven by its own tests and by those of the Very Back client, not again by the tests of
/// this class.</para>
/// </remarks>
public sealed class BackClient : IBackClient, IAsyncDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the Back in the logs and the messages
    /// </summary>
    private const string PeerName = "the Back";

    #endregion

    #region Variables

    /// <summary>
    /// Connection management of the link to the Back
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
    /// <param name="options">[Cannot be null] Settings of the link; checked here too, so that a wrong setting never reaches the link</param>
    /// <param name="connector">[Cannot be null] Connector used to open the connection</param>
    /// <param name="logger">[Cannot be null] Logger of the client</param>
    /// <exception cref="ArgumentNullException">If the options, the connector or the logger is null</exception>
    /// <exception cref="InvalidOperationException">If a setting is invalid</exception>
    public BackClient(BackClientOptions options, ITransportConnector connector, ILogger<BackClient> logger)
    {
        // Checks
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");
        if (connector is null)
            throw new ArgumentNullException(nameof(connector), $"The value of the parameter: {nameof(connector)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");
        string? problem = options.FindProblem();
        if (problem is not null)
            throw new InvalidOperationException(problem);

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
    /// Method used to ask the Back whether the chain of tiers answers (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves that the Gateway reaches the Back, the Back the Very Back, and the Very Back its database.</para>
    /// <para><b>Behavior:</b> sends the Ping operation on the shared connection, opening it first when needed. The request is not retried
    /// when it fails.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to stop waiting, for the connection as for the response</param>
    /// <returns>
    /// The time of the SQL Server, as answered by the Back
    /// </returns>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="System.Net.Sockets.SocketException">If the Back cannot be reached</exception>
    /// <exception cref="TimeoutException">If the connection is not open within the connect timeout</exception>
    /// <exception cref="IOException">If the connection closes before the response arrives</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="OperationCanceledException">If the caller cancels</exception>
    public Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return _link.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken);
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
