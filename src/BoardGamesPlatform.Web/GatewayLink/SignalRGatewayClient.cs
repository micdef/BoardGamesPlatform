/*
 *	@file	src/BoardGamesPlatform.Web/GatewayLink/SignalRGatewayClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	SignalR client of the website for the hub of the Gateway
 */

// Using
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using Microsoft.AspNetCore.SignalR.Client;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.GatewayLink;

/// <summary>
/// Service used to call the hub of the Gateway through a SignalR connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the website server is the only SignalR client of the Gateway; the browser never reaches it.</para>
/// <para><b>Behavior:</b> registered as scoped, so one instance (and one connection) lives as long as the circuit of one player, and is
/// disposed with it. The connection is started on the first call, and started again on the next call when it was lost. The hub is not
/// authenticated until 0.10.0.</para>
/// </remarks>
public sealed class SignalRGatewayClient : IGatewayClient, IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Connection to the hub, built but not started by the caller of the constructor
    /// </summary>
    private readonly HubConnection _connection;

    /// <summary>
    /// Lets one call at a time start the connection
    /// </summary>
    private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// True once the client has been disposed
    /// </summary>
    private bool _disposed;

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
    /// Constructor
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to the hub, not started; the client owns it and disposes it</param>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    public SignalRGatewayClient(HubConnection connection)
    {
        // Check
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");

        // Setting
        _connection = connection;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to start the connection when it is not connected
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> connects on first use, and again after a lost connection.</para>
    /// <para><b>Behavior:</b> one caller at a time starts the connection; the others wait, then find it connected.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that ends when the connection is connected
    /// </returns>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        // Waiting for our turn
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Starting the connection when it is not connected
            if (_connection.State == HubConnectionState.Disconnected)
                await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectLock.Release();
        }
    }

    #endregion

    #region Publics

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Check
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Connecting on first use
        await EnsureConnectedAsync(cancellationToken).ConfigureAwait(false);

        // Function's return
        return await _connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to close the connection when the circuit of the player ends
    /// </summary>
    /// <returns>
    /// A task that ends when the connection is disposed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once
        if (_disposed)
            return;
        _disposed = true;

        // Releasing the connection and the lock
        await _connection.DisposeAsync().ConfigureAwait(false);
        _connectLock.Dispose();
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
