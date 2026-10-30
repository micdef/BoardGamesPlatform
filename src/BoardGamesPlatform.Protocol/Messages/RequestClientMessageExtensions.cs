/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/RequestClientMessageExtensions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Client side of the messages: sends a typed request and reads its typed response
 */

// Using
using BoardGamesPlatform.Protocol.Correlation;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Messages;

/// <summary>
/// Extensions used to send typed requests with a <see cref="RequestClient"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Gateway and the Back call an operation of the next tier with its contract types, without handling JSON.</para>
/// <para><b>Behavior:</b> wraps the request in its envelope, sends it, then reads the response envelope. A malformed response is a protocol
/// violation: an <see cref="InvalidDataException"/> is thrown to the caller (the connection itself stays open: the client cannot know whether
/// the next responses are sound, so the caller should dispose it, following the connection rule).</para>
/// </remarks>
public static class RequestClientMessageExtensions
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
    /// Method used to call an operation of the next tier and wait for its response
    /// </summary>
    /// <typeparam name="TRequest">Type of the request body</typeparam>
    /// <typeparam name="TResponse">Type of the response body</typeparam>
    /// <param name="client">[Cannot be null] Request client of the link</param>
    /// <param name="operation">[Cannot be null] Name of the operation, for example "Ping"</param>
    /// <param name="request">[Cannot be null] Body of the request</param>
    /// <param name="cancellationToken">Token used to stop waiting</param>
    /// <returns>
    /// The body of the response
    /// </returns>
    /// <exception cref="ArgumentNullException">If the client, the operation or the request is null</exception>
    /// <exception cref="InvalidDataException">If the response is malformed</exception>
    /// <exception cref="IOException">If the connection is closed, or closes before the response arrives</exception>
    /// <exception cref="ObjectDisposedException">If the client is disposed</exception>
    /// <exception cref="OperationCanceledException">If the wait is canceled</exception>
    public static async Task<TResponse> SendAsync<TRequest, TResponse>(this RequestClient client, string operation, TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
        where TResponse : class
    {
        // Checks
        if (client is null)
            throw new ArgumentNullException(nameof(client), $"The value of the parameter: {nameof(client)} cannot be null");
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentNullException(nameof(operation), $"The value of the parameter: {nameof(operation)} cannot be null or empty");
        if (request is null)
            throw new ArgumentNullException(nameof(request), $"The value of the parameter: {nameof(request)} cannot be null");

        // Sending the envelope, then reading the response
        byte[] payload = MessageSerializer.WriteRequest(operation, request);
        ReadOnlyMemory<byte> response = await client.SendAsync(payload, cancellationToken).ConfigureAwait(false);

        // Function's return
        return MessageSerializer.ReadResponse<TResponse>(response.Span);
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
