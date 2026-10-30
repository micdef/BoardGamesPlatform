/*
 *	@file	test/BoardGamesPlatform.Platform.Infrastructure.Tests/VeryBack/HangingConnector.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connector that never opens a connection, to test the connect timeout
 */

// Using
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.Tests.VeryBack;

/// <summary>
/// Connector whose connection never completes
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a real TCP handshake is completed by the operating system as soon as the port listens (the connection waits in the
/// backlog, even if nobody accepts it), so a peer that never completes the handshake cannot be built deterministically with a loopback
/// socket. This connector stands in for it: it waits until the token it receives is canceled, which is exactly what the client must do.</para>
/// <para><b>Behavior:</b> <see cref="Started"/> completes when a connection attempt begins, so that a test can cancel at the right moment
/// without any delay.</para>
/// </remarks>
internal sealed class HangingConnector : ITransportConnector
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Completed when a connection attempt begins
    /// </summary>
    private readonly TaskCompletionSource _started;

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

    /// <summary>
    /// Task that completes when a connection attempt begins
    /// </summary>
    public Task Started => _started.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public HangingConnector()
    {
        // Setting
        _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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

    /// <inheritdoc/>
    public async Task<ITransportConnection> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        // Telling that the attempt began, then waiting for the cancellation
        _started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);

        // Never reached: the delay only ends by cancellation
        throw new InvalidOperationException("The hanging connector cannot connect");
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
