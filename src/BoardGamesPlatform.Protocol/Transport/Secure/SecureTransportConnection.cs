/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureTransportConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connection whose bytes travel in the records of the secure channel, inside a TLS session
 */

// Using
using System.Net;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Connection of the Back → Very Back link once both handshakes succeeded
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> created by <see cref="SecureTransportConnector"/> or <see cref="SecureTransportListener"/>. It owns the TLS connection
/// below it. Closing the sending side writes the close record, then closes the sending side of TLS. <see cref="CloseForAsync"/> ends it from
/// another task (a revoked key, its age): the close record first, within <see cref="CloseDelay"/>, then the connection. Disposing it erases the
/// keys, then closes the TLS connection; it can be called more than once.</para>
/// </remarks>
internal sealed class SecureTransportConnection : ITransportConnection
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time given to the close record before a connection closed from another task is cut
    /// </summary>
    public static readonly TimeSpan CloseDelay = TimeSpan.FromSeconds(5);

    #endregion

    #region Variables

    /// <summary>
    /// TLS connection below the channel
    /// </summary>
    private readonly ITransportConnection _inner;

    /// <summary>
    /// Records of the channel
    /// </summary>
    private readonly SecureRecordStream _stream;

    /// <summary>
    /// Called once the connection is disposed, to remove it from its register
    /// </summary>
    private readonly Action? _onDisposed;

    /// <summary>
    /// Whether the connection was disposed (0: no, 1: yes)
    /// </summary>
    private int _isDisposed;

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

    /// <inheritdoc/>
    public Stream Stream => _stream;

    /// <inheritdoc/>
    public EndPoint RemoteEndPoint => _inner.RemoteEndPoint;

    /// <inheritdoc/>
    public string? PeerIdentity => _inner.PeerIdentity;

    /// <inheritdoc/>
    public bool IsRenewalDue => _stream.IsRenewalDue || IsTooOld(RenewalAge);

    /// <summary>
    /// Address and port of the peer, written once for the logs
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// Pinned key the peer proved it holds
    /// </summary>
    public SigningPublicKey PeerKey { get; }

    /// <summary>
    /// Age from which the client replaces the connection
    /// </summary>
    public TimeSpan RenewalAge { get; }

    /// <summary>
    /// Clock of the age of the connection
    /// </summary>
    private TimeProvider TimeProvider { get; }

    /// <summary>
    /// Time stamp of the clock when the connection opened
    /// </summary>
    private long OpenedAt { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="inner">[Cannot be null] TLS connection below; the connection takes ownership of it</param>
    /// <param name="stream">[Cannot be null] Records of the channel</param>
    /// <param name="peerKey">[Cannot be null] Pinned key of the peer</param>
    /// <param name="renewalAge">Age from which the connection should be replaced</param>
    /// <param name="timeProvider">[Cannot be null] Clock of the age</param>
    /// <param name="onDisposed">Called once the connection is disposed; null for nothing</param>
    public SecureTransportConnection(ITransportConnection inner, SecureRecordStream stream, SigningPublicKey peerKey, TimeSpan renewalAge, TimeProvider timeProvider,
        Action? onDisposed)
    {
        // Setting
        _inner = inner;
        _stream = stream;
        PeerKey = peerKey;
        RemoteName = inner.RemoteEndPoint.ToString() ?? "unknown";
        RenewalAge = renewalAge;
        TimeProvider = timeProvider;
        OpenedAt = timeProvider.GetTimestamp();
        _onDisposed = onDisposed;
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
    /// Method used to tell whether the connection is at least as old as the age given
    /// </summary>
    /// <param name="age">Age to compare with</param>
    /// <returns>
    /// True when the connection opened at least that long ago
    /// </returns>
    public bool IsTooOld(TimeSpan age)
    {
        // Function's return
        return TimeProvider.GetElapsedTime(OpenedAt) >= age;
    }

    /// <summary>
    /// Method used to stop sending, while still reading: the peer reads the close record, then the end of the TLS session
    /// </summary>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once both are sent
    /// </returns>
    /// <exception cref="IOException">If the stream fails</exception>
    /// <exception cref="ObjectDisposedException">If the connection is closed</exception>
    public async Task CloseSendAsync(CancellationToken cancellationToken = default)
    {
        // Sending the close record of the channel, then closing the sending side of TLS
        await _stream.WriteCloseAsync(cancellationToken).ConfigureAwait(false);
        await _inner.CloseSendAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to end the connection from another task: the close record first, then the connection
    /// </summary>
    /// <returns>
    /// A task that completes once the connection is closed; it never throws, since nobody waits for it but the logs
    /// </returns>
    public async Task CloseForAsync()
    {
        // Telling the peer with the close record, within the delay; a failure only means the connection is cut at once
        using CancellationTokenSource delay = new CancellationTokenSource(CloseDelay);
        try
        {
            await CloseSendAsync(delay.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or OperationCanceledException or InvalidOperationException
            or NotSupportedException or SecureChannelException)
        {
            // The connection is cut below: the reason was logged by the caller
        }

        // Closing it; the reader of the protocol then sees the end of the connection
        try
        {
            await DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            // Already broken: closed in any case
        }
    }

    /// <summary>
    /// Method used to erase the keys and close the TLS connection
    /// </summary>
    /// <returns>
    /// A task that completes once both are closed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once: the records, then the connection below, whatever the first close did
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            return;
        try
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            _onDisposed?.Invoke();
            await _inner.DisposeAsync().ConfigureAwait(false);
        }
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
