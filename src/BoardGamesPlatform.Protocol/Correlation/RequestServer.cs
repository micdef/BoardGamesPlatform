/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/RequestServer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of a link: answers the requests of one connection, several at a time
 */

// Using
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Correlation;

/// <summary>
/// Server used to answer the requests received on a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Back and the Very Back answer the requests of the tier in front of them, each response carrying the
/// correlation identifier of its request, so that the client can pair them whatever the order.</para>
/// <para><b>Behavior:</b> reads the requests one after the other and hands each one to the handler without waiting for the previous one, up to
/// a maximum number of requests handled at the same time per connection; beyond it, the server stops reading until a request ends, so that a
/// peer cannot make it start an unbounded amount of work. The responses are written one at a time.</para>
/// <list type="bullet">
/// <item><description>A frame with the reserved identifier <see cref="Frame.NoCorrelation"/> is a protocol violation.</description></item>
/// <item><description>A handler that throws ends the service of the connection: until the error codes of 0.4.0, a request has no other way to
/// report a failure, and a client must not wait for a response that will never come.</description></item>
/// </list>
/// </remarks>
public sealed class RequestServer
{
    #region Members

    #region Constants

    /// <summary>
    /// Number of requests of one connection handled at the same time when none is given
    /// </summary>
    public const int DefaultMaxConcurrentRequests = 64;

    #endregion

    #region Variables

    /// <summary>
    /// Codec used to read the requests and write the responses
    /// </summary>
    private readonly FrameCodec _codec;

    /// <summary>
    /// Handler that turns the content of a request into the content of its response
    /// </summary>
    private readonly Func<ReadOnlyMemory<byte>, CancellationToken, Task<ReadOnlyMemory<byte>>> _handler;

    /// <summary>
    /// Number of requests of one connection handled at the same time, at most
    /// </summary>
    private readonly int _maxConcurrentRequests;

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
    /// <param name="codec">[Cannot be null] Codec of the link; both ends use the same maximum length</param>
    /// <param name="handler">[Cannot be null] Handler of the requests: receives the content of a request and a token canceled when the service
    /// of the connection ends, and returns the content of the response. It may be called for several requests at the same time. It must
    /// observe the token: the service of a connection only ends once every handler has returned. Its response must not exceed the maximum
    /// length of the codec; a larger one is a handler failure.</param>
    /// <param name="maxConcurrentRequests">Number of requests of one connection handled at the same time, at most; 1 or more</param>
    /// <exception cref="ArgumentNullException">If the codec or the handler is null</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the maximum number of requests is zero or negative</exception>
    public RequestServer(FrameCodec codec, Func<ReadOnlyMemory<byte>, CancellationToken, Task<ReadOnlyMemory<byte>>> handler,
        int maxConcurrentRequests = DefaultMaxConcurrentRequests)
    {
        // Checks
        if (codec is null)
            throw new ArgumentNullException(nameof(codec), $"The value of the parameter: {nameof(codec)} cannot be null");
        if (handler is null)
            throw new ArgumentNullException(nameof(handler), $"The value of the parameter: {nameof(handler)} cannot be null");
        if (maxConcurrentRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrentRequests), maxConcurrentRequests, $"The value of the parameter: {nameof(maxConcurrentRequests)} must be greater than zero");

        // Setting
        _codec = codec;
        _handler = handler;
        _maxConcurrentRequests = maxConcurrentRequests;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to handle one request and write its response
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> never throws: a failure of the handler or of the write is recorded in <paramref name="service"/>, which ends the
    /// service of the connection. The slot of the request is always given back.</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection to answer on</param>
    /// <param name="request">[Cannot be null] Request to handle</param>
    /// <param name="service">[Cannot be null] State shared by the requests of the connection</param>
    /// <returns>
    /// A task that completes when the response is written or the request has failed
    /// </returns>
    private async Task HandleRequestAsync(ITransportConnection connection, Frame request, ServedConnectionState service)
    {
        try
        {
            // Leaving the read loop first: the handler then never delays the reading of the next requests, even before its first await
            await Task.Yield();

            // Running the handler, then writing the response under the identifier of the request
            ReadOnlyMemory<byte> response = await _handler(request.Payload, service.Token).ConfigureAwait(false);
            await service.WriteLock.WaitAsync(service.Token).ConfigureAwait(false);
            try
            {
                await _codec.WriteAsync(connection.Stream, new Frame(request.CorrelationId, response), service.Token).ConfigureAwait(false);
            }
            finally
            {
                service.WriteLock.Release();
            }
        }
        catch (Exception exception)
        {
            // Ending the service of the connection (a cancellation caused by an earlier failure keeps that failure as the reason)
            service.Fail(exception);
        }
        finally
        {
            service.Slots.Release();
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to answer the requests of a connection until it ends
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> returns when the peer closes the connection between two requests, once every request in progress is answered.
    /// When anything else ends the service (a protocol violation, a failure of the stream, a failing handler, the cancellation), the requests
    /// still in progress are canceled and awaited, then the cause is thrown. In every case, the caller then disposes the connection.</para>
    /// </remarks>
    /// <param name="connection">[Cannot be null] Connection to serve; it stays owned by the caller</param>
    /// <param name="cancellationToken">Token used to stop serving, for example when the tier stops</param>
    /// <returns>
    /// A task that completes when the connection is served to its end
    /// </returns>
    /// <exception cref="ArgumentNullException">If the connection is null</exception>
    /// <exception cref="InvalidDataException">If the peer breaks the protocol (a malformed frame, the reserved identifier)</exception>
    /// <exception cref="IOException">If the stream fails</exception>
    /// <exception cref="OperationCanceledException">If the service is canceled</exception>
    /// <exception cref="Exception">Whatever a handler throws</exception>
    public async Task ServeAsync(ITransportConnection connection, CancellationToken cancellationToken = default)
    {
        // Check
        if (connection is null)
            throw new ArgumentNullException(nameof(connection), $"The value of the parameter: {nameof(connection)} cannot be null");

        // Reading the requests, each one handled in its own task, until the peer closes or the service fails
        using ServedConnectionState service = new ServedConnectionState(_maxConcurrentRequests, cancellationToken);
        try
        {
            while (true)
            {
                // Taking a slot before reading: when every slot is busy, the server stops reading (backpressure)
                await service.Slots.WaitAsync(service.Token).ConfigureAwait(false);

                // The slot belongs to the read loop until a request task takes it; on every other path it is given back,
                // otherwise the final wait for the requests in progress would never end
                bool slotHandedOver = false;
                try
                {
                    Frame? request = await _codec.ReadAsync(connection.Stream, service.Token).ConfigureAwait(false);
                    if (request is null)
                        break;
                    if (request.CorrelationId == Frame.NoCorrelation)
                        throw new InvalidDataException("The peer sent a request with the reserved correlation identifier");
                    slotHandedOver = true;
                    _ = HandleRequestAsync(connection, request, service);
                }
                finally
                {
                    if (!slotHandedOver)
                        service.Slots.Release();
                }
            }
        }
        catch (Exception exception)
        {
            service.Fail(exception);
        }

        // Waiting for the requests in progress: each one gives its slot back when it ends
        for (int slot = 0; slot < _maxConcurrentRequests; slot++)
            await service.Slots.WaitAsync(CancellationToken.None).ConfigureAwait(false);

        // Reporting why the service ended, unless the peer simply closed the connection
        service.ThrowIfFailed();
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
