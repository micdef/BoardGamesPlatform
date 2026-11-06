/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/SecureTransportConnection.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Connection whose bytes travel in the records of the secure channel, inside a TLS session
 */

// Using
using System.Net;
using Microsoft.Extensions.Logging;

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
/// below it.</para>
/// <list type="bullet">
/// <item><description>Closing the sending side writes the close record, then closes the sending side of TLS, once, within <see cref="CloseDelay"/>
/// whatever the token of the first caller: a second call waits for the same close, within its own token.</description></item>
/// <item><description>Disposing it ends it cleanly: the close record first, within <see cref="DisposeCloseDelay"/>, when the stream is still sound
/// and the record was not sent, so that the peer reads a clean end and never takes a normal close for a truncation; then the keys are erased
/// and the TLS connection closed, whatever the close record did, a close still being written stopped first so that the keys are never erased
/// under it; a close record of a normal close that cannot be sent is traced (event id 9068, Debug). It can be called more than once.</description></item>
/// <item><description><see cref="CloseForAsync"/> ends it from another task (a revoked key, its age), once: the close record first, within
/// <see cref="CloseDelay"/>, then the connection; a close record that cannot be sent is logged (event id 9064), and the connection is cut in
/// any case (a failure of the close below logged as 9066).</description></item>
/// </list>
/// </remarks>
internal sealed partial class SecureTransportConnection : ITransportConnection
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time given to the close record before a connection closed from another task is cut
    /// </summary>
    public static readonly TimeSpan CloseDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Longest time given to the close record of a normal disposal
    /// </summary>
    public static readonly TimeSpan DisposeCloseDelay = TimeSpan.FromSeconds(1);

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
    /// Logger of the tier
    /// </summary>
    private readonly ILogger _logger;

    /// <summary>
    /// Clock of the age of the connection
    /// </summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Time stamp of the clock when the connection opened
    /// </summary>
    private readonly long _openedAt;

    /// <summary>
    /// Close of the sending side, started once and shared by every caller; null before it starts
    /// </summary>
    private Task? _closingSend;

    /// <summary>
    /// Delay of the shared close (<see cref="CloseDelay"/>), canceled by the disposal before it erases the keys; null before the close starts
    /// </summary>
    private CancellationTokenSource? _closingDelay;

    /// <summary>
    /// Lock of the start of the close of the sending side
    /// </summary>
    private readonly Lock _closeLock = new Lock();

    /// <summary>
    /// Whether <see cref="CloseForAsync"/> was called (0: no, 1: yes)
    /// </summary>
    private int _isClosing;

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
    /// Whether the connection is being ended from another task (<see cref="CloseForAsync"/>), so that the register does not close it again
    /// </summary>
    public bool IsClosing => Volatile.Read(ref _isClosing) == 1;

    /// <summary>
    /// Address and port of the peer, written once for the logs
    /// </summary>
    public string RemoteName { get; }

    /// <summary>
    /// Pinned key the peer proved it holds
    /// </summary>
    public SigningPublicKey PeerKey { get; }

    /// <summary>
    /// Age from which the connection should be replaced
    /// </summary>
    public TimeSpan RenewalAge { get; }

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
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <param name="onDisposed">Called once the connection is disposed; null for nothing</param>
    /// <exception cref="ArgumentNullException">If a parameter that cannot be null is null</exception>
    public SecureTransportConnection(ITransportConnection inner, SecureRecordStream stream, SigningPublicKey peerKey, TimeSpan renewalAge, TimeProvider timeProvider,
        ILogger logger, Action? onDisposed)
    {
        // Checks
        if (inner is null)
            throw new ArgumentNullException(nameof(inner), $"The value of the parameter: {nameof(inner)} cannot be null");
        if (stream is null)
            throw new ArgumentNullException(nameof(stream), $"The value of the parameter: {nameof(stream)} cannot be null");
        if (peerKey is null)
            throw new ArgumentNullException(nameof(peerKey), $"The value of the parameter: {nameof(peerKey)} cannot be null");
        if (timeProvider is null)
            throw new ArgumentNullException(nameof(timeProvider), $"The value of the parameter: {nameof(timeProvider)} cannot be null");
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting
        _inner = inner;
        _stream = stream;
        PeerKey = peerKey;
        RemoteName = inner.RemoteEndPoint.ToString() ?? "unknown";
        RenewalAge = renewalAge;
        _timeProvider = timeProvider;
        _logger = logger;
        _openedAt = timeProvider.GetTimestamp();
        _onDisposed = onDisposed;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a close record that could not be sent before a connection closed from another task is cut
    /// </summary>
    /// <param name="exception">Why the close record failed</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9064, Level = LogLevel.Warning, Message = "The close record to {RemoteEndPoint} could not be sent; the connection is cut")]
    private partial void LogCloseRecordFailed(Exception exception, string remoteEndPoint);

    /// <summary>
    /// Method used to log a connection closed from another task that could not be closed below
    /// </summary>
    /// <param name="exception">Why the close failed</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9066, Level = LogLevel.Warning, Message = "The connection to {RemoteEndPoint} could not be closed cleanly; its socket is released")]
    private partial void LogCloseFailed(Exception exception, string remoteEndPoint);

    /// <summary>
    /// Method used to trace a close record of a normal close that could not be sent: the stream broke meanwhile (its cause already logged), or
    /// the peer is gone or does not read within <see cref="DisposeCloseDelay"/>; at the Debug level, since a peer gone is a normal end
    /// </summary>
    /// <param name="exception">Why the close record failed</param>
    /// <param name="remoteEndPoint">Address and port of the peer</param>
    [LoggerMessage(EventId = 9068, Level = LogLevel.Debug, Message = "The close record of the normal close to {RemoteEndPoint} could not be sent; the peer may log a truncation")]
    private partial void LogDisposeCloseRecordFailed(Exception exception, string remoteEndPoint);

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
        return _timeProvider.GetElapsedTime(_openedAt) >= age;
    }

    /// <summary>
    /// Method used to stop sending, while still reading: the peer reads the close record, then the end of the TLS session
    /// </summary>
    /// <param name="cancellationToken">Token used to give up</param>
    /// <returns>
    /// A task that completes once both are sent; at once when they were sent already
    /// </returns>
    /// <exception cref="IOException">If the stream fails, or the channel is closed or broken</exception>
    public Task CloseSendAsync(CancellationToken cancellationToken = default)
    {
        // Closing once, within a delay of its own, whatever the token of the caller who started it: every caller waits for the same close,
        // within its own token, so that a disposal never erases the keys while the close record is being written
        Task closing;
        lock (_closeLock)
        {
            if (_closingSend is null)
            {
                _closingDelay = new CancellationTokenSource(CloseDelay);
                _closingSend = ObserveFailure(CloseSendOnceAsync(_closingDelay.Token));
            }
            closing = _closingSend;
        }

        // Function's return
        return closing.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to write the close record of the channel, then close the sending side of TLS, within <see cref="CloseDelay"/>
    /// </summary>
    /// <param name="cancellationToken">Token of the delay of the close, canceled by the disposal</param>
    /// <returns>
    /// A task that completes once both are sent
    /// </returns>
    private async Task CloseSendOnceAsync(CancellationToken cancellationToken)
    {
        // The close record, then the end of TLS, a peer that does not read stopping it at the delay
        await _stream.WriteCloseAsync(cancellationToken).ConfigureAwait(false);
        await _inner.CloseSendAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Method used to observe the failure of the shared close, so that it is never an unobserved task when every caller gave up waiting
    /// </summary>
    /// <param name="closing">[Cannot be null] Shared close</param>
    /// <returns>
    /// The same task
    /// </returns>
    private static Task ObserveFailure(Task closing)
    {
        // Reading its exception once it fails; each caller that waits for it still gets the failure
        _ = closing.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        // Function's return
        return closing;
    }

    /// <summary>
    /// Method used to end the connection from another task, once: the close record first, then the connection
    /// </summary>
    /// <returns>
    /// A task that completes once the connection is closed, or at once for a caller after the first one; it never throws, since nobody waits
    /// for it but the logs
    /// </returns>
    public async Task CloseForAsync()
    {
        // Ending it once, whoever asks first (a removed key, its age)
        if (Interlocked.Exchange(ref _isClosing, 1) == 1)
            return;

        // Telling the peer with the close record, within the delay; a failure is logged, and only means the connection is cut at once
        try
        {
            using CancellationTokenSource delay = new CancellationTokenSource(CloseDelay);
            await CloseSendAsync(delay.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LogCloseRecordFailed(exception, RemoteName);
        }

        // Closing it; the reader of the protocol then sees the end of the connection
        try
        {
            await DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            LogCloseFailed(exception, RemoteName);
        }
    }

    /// <summary>
    /// Method used to end the connection cleanly, then erase the keys and close the TLS connection
    /// </summary>
    /// <returns>
    /// A task that completes once all are closed
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            return;

        // Sending the close record when the writes are still sound, or waiting for the one being sent, so that the peer reads a clean end; a
        // peer that no longer reads only delays the close by the short delay
        bool isClosingSend;
        lock (_closeLock)
            isClosingSend = _closingSend is not null;
        if (_stream.IsSound || isClosingSend)
        {
            try
            {
                using CancellationTokenSource delay = new CancellationTokenSource(DisposeCloseDelay);
                await CloseSendAsync(delay.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The peer is gone or does not read: told for the diagnosis (the peer may log a truncation), the connection closed below; a
                // close from another task already logged its failure (9064)
                if (!IsClosing)
                    LogDisposeCloseRecordFailed(exception, RemoteName);
            }
        }

        // A close still being written is stopped, and its end awaited (its failure was told above), so that the keys are never erased under it
        Task? closing;
        CancellationTokenSource? closingDelay;
        lock (_closeLock)
        {
            closing = _closingSend;
            closingDelay = _closingDelay;
        }
        if (closing is not null && closingDelay is not null)
        {
            await closingDelay.CancelAsync().ConfigureAwait(false);
            await closing.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            closingDelay.Dispose();
        }

        // Erasing the keys, then closing the TLS connection, then leaving the register, each step whatever the one before did
        try
        {
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _inner.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                _onDisposed?.Invoke();
            }
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
