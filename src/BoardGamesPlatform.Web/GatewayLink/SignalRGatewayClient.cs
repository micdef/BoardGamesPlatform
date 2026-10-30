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
/// disposed with it. The connection is started on the first call, and started again on the next call when it was lost. The connection is
/// built without automatic reconnection: a drop between the end of the start and the call gives one failure, which the next call recovers
/// from. Each call is bounded by a timeout and by the caller's token; disposing the client cancels the calls in progress and the calls
/// waiting to connect. The hub is not authenticated until 0.10.0.</para>
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
    /// Longest duration of a call, connection included
    /// </summary>
    private readonly TimeSpan _timeout;

    /// <summary>
    /// Lets one call at a time start the connection; never disposed, so that a call still using it cannot fail on a disposed semaphore
    /// </summary>
    private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

    /// <summary>
    /// Canceled when the client is disposed, to end the calls in progress and the calls waiting for the lock
    /// </summary>
    private readonly CancellationTokenSource _disposing = new CancellationTokenSource();

    /// <summary>
    /// 1 once the client has been disposed, otherwise 0 (read and written with Interlocked)
    /// </summary>
    private int _disposed;

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
    /// <param name="timeout">Longest duration of a call, connection included; greater than zero</param>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the timeout is not greater than zero</exception>
    public SignalRGatewayClient(HubConnection connection, TimeSpan timeout)
    {
        // Check
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), $"The value of the parameter: {nameof(timeout)} must be greater than zero");

        // Setting
        _connection = connection;
        _timeout = timeout;
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
    /// <para><b>Behavior:</b> one caller at a time starts the connection; the others wait, then find it connected. A failure of the start
    /// is raised to the caller, which reports it (the page logs it).</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that ends when the connection is connected
    /// </returns>
    /// <exception cref="InvalidOperationException">If the connection is in a state that cannot be handled (connecting or reconnecting)</exception>
    private async Task EnsureConnectedAsync(CancellationToken cancellationToken)
    {
        // Waiting for our turn
        await _connectLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Giving up when the call was canceled while it waited: the lock can be granted just after a disposal canceled the calls
            cancellationToken.ThrowIfCancellationRequested();

            // Starting the connection when it is not connected; nothing to do when it is
            if (_connection.State == HubConnectionState.Disconnected)
                await _connection.StartAsync(cancellationToken).ConfigureAwait(false);
            else if (_connection.State != HubConnectionState.Connected)
                throw new InvalidOperationException($"The connection to the Gateway is in the unexpected state {_connection.State}");
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
    /// <exception cref="OperationCanceledException">If the caller cancels, the timeout elapses, or the client is disposed during the call</exception>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Check
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        // Bounding the call by the caller's token, the timeout and the disposal of the client
        using CancellationTokenSource call = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposing.Token);
        call.CancelAfter(_timeout);

        try
        {
            // Connecting on first use
            await EnsureConnectedAsync(call.Token).ConfigureAwait(false);

            // Function's return
            return await _connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, call.Token).ConfigureAwait(false);
        }
        catch (ObjectDisposedException exception) when (Volatile.Read(ref _disposed) == 1)
        {
            // The client was disposed during the call, between the check of the token and the use of the connection: one contract for every call
            // ended by the disposal, a cancellation
            throw new OperationCanceledException("The client of the Gateway was disposed during the call", exception, call.Token);
        }
    }

    /// <summary>
    /// Method used to close the connection when the circuit of the player ends
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> frees the connection with the circuit, without leaving a call hanging.</para>
    /// <para><b>Behavior:</b> the first call cancels the calls in progress, then disposes the connection; the next ones do nothing.</para>
    /// </remarks>
    /// <returns>
    /// A task that ends when the connection is disposed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Ending the calls in progress and the calls waiting to connect
        _disposing.Cancel();

        // Releasing the connection (the lock is left alone: a call may still be leaving it)
        await _connection.DisposeAsync().ConfigureAwait(false);
        _disposing.Dispose();
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
