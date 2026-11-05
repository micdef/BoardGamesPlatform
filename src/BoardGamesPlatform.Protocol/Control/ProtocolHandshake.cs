/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/ProtocolHandshake.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Opening of a connection: both ends agree on the protocol version and check their limits before any request
 */

// Using
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Handshake that opens every connection of a link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> two tiers of different versions must find out at once that they cannot talk, with a clear code, instead of
/// misreading each other's requests; and both ends must agree on the largest frame before a frame is refused in the middle of a session.</para>
/// <para><b>Behavior:</b> the client speaks first. Both messages are control messages, on the reserved correlation identifier:</para>
/// <list type="number">
/// <item><description>the client sends a <see cref="HelloMessage"/>: the range of versions it speaks and its largest payload;</description></item>
/// <item><description>the server chooses the newest version both ends speak and answers a <see cref="HelloAckMessage"/>, with its own limit
/// of requests handled at the same time; or it answers a <see cref="HelloRefusedMessage"/>
/// (<see cref="ErrorCodes.ProtocolVersionUnsupported"/>, <see cref="ErrorCodes.ProtocolLimitsMismatch"/>) and the connection is closed.</description></item>
/// </list>
/// <para>Any other frame before the end of the handshake (a request, a malformed message) is a protocol violation. The server allows a limited
/// time for the whole handshake, so that a connection that never speaks does not hold a place. After any failure the connection is closed
/// by the caller, following the connection rule of <see cref="FrameCodec"/>.</para>
/// </remarks>
public static class ProtocolHandshake
{
    #region Members

    #region Constants

    /// <summary>
    /// Oldest protocol version this build speaks
    /// </summary>
    public const int MinVersion = 1;

    /// <summary>
    /// Newest protocol version this build speaks
    /// </summary>
    public const int MaxVersion = 1;

    #endregion

    #region Variables

    /// <summary>
    /// Time the server allows a client for the whole handshake when none is given
    /// </summary>
    public static readonly TimeSpan DefaultServerTimeout = TimeSpan.FromSeconds(5);

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

    /// <summary>
    /// Method used to write one control message on a connection
    /// </summary>
    /// <typeparam name="TMessage">Type of the control message</typeparam>
    /// <param name="connection">[Cannot be null] Connection to write to</param>
    /// <param name="codec">[Cannot be null] Codec of the link</param>
    /// <param name="message">[Cannot be null] Control message</param>
    /// <param name="cancellationToken">Token used to cancel the write</param>
    /// <returns>
    /// A task that completes once the message is written
    /// </returns>
    private static Task WriteAsync<TMessage>(ITransportConnection connection, FrameCodec codec, TMessage message, CancellationToken cancellationToken)
        where TMessage : ControlMessage
    {
        // Function's return
        return codec.WriteAsync(connection.Stream, new Frame(Frame.NoCorrelation, MessageSerializer.WriteControl(message)), cancellationToken);
    }

    /// <summary>
    /// Method used to read the next control message of a connection, refusing any other frame
    /// </summary>
    /// <param name="connection">[Cannot be null] Connection to read from</param>
    /// <param name="codec">[Cannot be null] Codec of the link</param>
    /// <param name="peer">[Cannot be null] Who sends the message, for the error messages ("the client", "the server")</param>
    /// <param name="cancellationToken">Token used to cancel the read</param>
    /// <returns>
    /// The control message read
    /// </returns>
    /// <exception cref="IOException">If the peer closes the connection before the message</exception>
    /// <exception cref="InvalidDataException">If the frame is not a control message, or the message is malformed</exception>
    private static async Task<ControlMessage> ReadAsync(ITransportConnection connection, FrameCodec codec, string peer, CancellationToken cancellationToken)
    {
        // Reading one frame, which must be on the reserved identifier
        Frame? frame = await codec.ReadAsync(connection.Stream, cancellationToken).ConfigureAwait(false);
        if (frame is null)
            throw new IOException($"{peer} closed the connection during the handshake");
        if (frame.CorrelationId != Frame.NoCorrelation)
            throw new InvalidDataException($"{peer} sent a request during the handshake");

        // Function's return
        return MessageSerializer.ReadControl(frame.Payload.Span);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to open a connection as its client: says hello and reads the answer of the server (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the answer is checked again here (defense in depth): a version outside the range of the client, another largest
    /// payload or a limit of requests below one is a protocol violation. The caller bounds the handshake with its token (the connect timeout
    /// of the link).</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection just opened; it stays owned by the caller</param>
    /// <param name="codec">[Cannot be null] Codec of the link</param>
    /// <param name="cancellationToken">Token used to stop the handshake</param>
    /// <returns>
    /// The agreement reached
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection or the codec is null</exception>
    /// <exception cref="RemoteErrorException">If the server refuses the hello; its code tells why</exception>
    /// <exception cref="InvalidDataException">If the server breaks the handshake</exception>
    /// <exception cref="IOException">If the stream fails, or the server closes the connection</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled</exception>
    public static async Task<HandshakeResult> RunClientAsync(ITransportConnection connection, FrameCodec codec, CancellationToken cancellationToken)
    {
        // Checks
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");

        // Saying hello, then reading the answer of the server
        await WriteAsync(connection, codec, new HelloMessage(MinVersion, MaxVersion, codec.MaxPayloadLength), cancellationToken).ConfigureAwait(false);
        ControlMessage answer = await ReadAsync(connection, codec, "The server", cancellationToken).ConfigureAwait(false);

        // A refusal is the answer of the server: its code tells the caller why
        if (answer is HelloRefusedMessage refused)
        {
            if (!ErrorCodes.IsWellFormed(refused.Code))
                throw new InvalidDataException("The server refused the hello with a malformed code");
            throw new RemoteErrorException(refused.Code);
        }

        // Checking the acceptance against what the client offered
        if (answer is not HelloAckMessage ack)
            throw new InvalidDataException("The server answered the hello with another control message");
        if (ack.Version < MinVersion || ack.Version > MaxVersion)
            throw new InvalidDataException($"The server chose the protocol version {ack.Version}, outside the versions {MinVersion} to {MaxVersion} of the client");
        if (ack.MaxPayloadLength != codec.MaxPayloadLength)
            throw new InvalidDataException("The server accepted the hello with another largest payload");
        if (ack.MaxConcurrentRequests < 1)
            throw new InvalidDataException("The server announced a limit of concurrent requests below one");

        // Function's return
        return new HandshakeResult(ack.Version, ack.MaxConcurrentRequests);
    }

    /// <summary>
    /// Method used to open a connection as its server: reads the hello of the client and answers it (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the whole handshake must end within the timeout. A hello that cannot be accepted is answered with a refusal, then
    /// the method throws, so that the caller closes the connection.</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection just accepted; it stays owned by the caller</param>
    /// <param name="codec">[Cannot be null] Codec of the link</param>
    /// <param name="maxConcurrentRequests">Number of requests of the connection handled at the same time, announced to the client; 1 or more</param>
    /// <param name="timeout">Time allowed for the whole handshake; greater than zero</param>
    /// <param name="cancellationToken">Token used to stop the handshake, for example when the tier stops</param>
    /// <returns>
    /// The agreement reached
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection or the codec is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the limit of requests or the timeout is out of its range</exception>
    /// <exception cref="TimeoutException">If the handshake does not end within the timeout</exception>
    /// <exception cref="InvalidDataException">If the client breaks the handshake, or its hello is refused</exception>
    /// <exception cref="IOException">If the stream fails, or the client closes the connection</exception>
    /// <exception cref="OperationCanceledException">If the handshake is canceled by the caller</exception>
    public static async Task<HandshakeResult> RunServerAsync(ITransportConnection connection, FrameCodec codec, int maxConcurrentRequests, TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        // Checks
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");
        if (maxConcurrentRequests < 1)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), maxConcurrentRequests, $"The value of the parameter: {nameof(maxConcurrentRequests)} must be greater than zero");
        if (timeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, $"The value of the parameter: {nameof(timeout)} must be greater than zero");

        // Combining the token of the caller with the deadline of the handshake
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        try
        {
            // Reading the hello, which must be the first message of the client
            ControlMessage first = await ReadAsync(connection, codec, "The client", deadline.Token).ConfigureAwait(false);
            if (first is not HelloMessage hello)
                throw new InvalidDataException("The first message of the client is not a hello");
            if (hello.MinVersion < 1 || hello.MinVersion > hello.MaxVersion)
                throw new InvalidDataException("The hello of the client announces an invalid range of versions");

            // Choosing the newest version both ends speak
            int version = Math.Min(hello.MaxVersion, MaxVersion);
            if (version < Math.Max(hello.MinVersion, MinVersion))
            {
                await WriteAsync(connection, codec, new HelloRefusedMessage(ErrorCodes.ProtocolVersionUnsupported), deadline.Token).ConfigureAwait(false);
                throw new InvalidDataException($"The client speaks the protocol versions {hello.MinVersion} to {hello.MaxVersion}, none of the versions {MinVersion} to {MaxVersion} of the server");
            }

            // Checking that both ends agree on the largest frame
            if (hello.MaxPayloadLength != codec.MaxPayloadLength)
            {
                await WriteAsync(connection, codec, new HelloRefusedMessage(ErrorCodes.ProtocolLimitsMismatch), deadline.Token).ConfigureAwait(false);
                throw new InvalidDataException($"The client uses a largest payload of {hello.MaxPayloadLength} bytes, the server {codec.MaxPayloadLength} bytes");
            }

            // Accepting
            await WriteAsync(connection, codec, new HelloAckMessage(version, codec.MaxPayloadLength, maxConcurrentRequests), deadline.Token).ConfigureAwait(false);

            // Function's return
            return new HandshakeResult(version, maxConcurrentRequests);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"The handshake of the client did not end within {timeout}", exception);
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
