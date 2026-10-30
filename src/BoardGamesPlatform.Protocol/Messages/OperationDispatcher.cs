/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/OperationDispatcher.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of the messages: routes each request to the handler of its operation
 */

// Using
using System.Text.Json;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Messages;

/// <summary>
/// Dispatcher used to route the requests of a link to the handler of their operation
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a tier declare one typed handler per operation ("Ping", later the account and game operations), and plug them all
/// into a <see cref="Correlation.RequestServer"/> through <see cref="HandleAsync"/>.</para>
/// <para><b>Behavior:</b> the operations are registered before the server starts and matched by exact name (ordinal, case-sensitive). A request
/// whose envelope or body is malformed, or whose operation is unknown, is a protocol violation: <see cref="HandleAsync"/> throws an
/// <see cref="InvalidDataException"/>, which ends the service of the connection. Registering is not thread-safe: it is done once, at startup.</para>
/// </remarks>
public sealed class OperationDispatcher
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Handler of each operation, by name; each one reads its body, runs the typed handler and writes the response envelope
    /// </summary>
    private readonly Dictionary<string, Func<JsonElement, CancellationToken, Task<byte[]>>> _handlers;

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
    public OperationDispatcher()
    {
        // Setting
        _handlers = new Dictionary<string, Func<JsonElement, CancellationToken, Task<byte[]>>>(StringComparer.Ordinal);
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
    /// Method used to declare the handler of an operation
    /// </summary>
    /// <typeparam name="TRequest">Type of the request body of the operation</typeparam>
    /// <typeparam name="TResponse">Type of the response body of the operation</typeparam>
    /// <param name="operation">[Cannot be null] Name of the operation, for example "Ping"</param>
    /// <param name="handler">[Cannot be null] Handler: receives the request body and the token of the service, returns the response body</param>
    /// <returns>
    /// The dispatcher itself, so that registrations can be chained
    /// </returns>
    /// <exception cref="ArgumentNullException">If the operation is null, empty or white spaces, or if the handler is null</exception>
    /// <exception cref="ArgumentException">If the operation is already registered</exception>
    public OperationDispatcher Register<TRequest, TResponse>(string operation, Func<TRequest, CancellationToken, Task<TResponse>> handler)
        where TRequest : class
        where TResponse : class
    {
        // Checks
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentNullException(nameof(operation), $"The value of the parameter: {nameof(operation)} cannot be null or empty");
        if (handler is null)
            throw new ArgumentNullException(nameof(handler), $"The value of the parameter: {nameof(handler)} cannot be null");
        if (_handlers.ContainsKey(operation))
            throw new ArgumentException($"The operation {operation} is already registered", nameof(operation));

        // Registering the typed handler behind the JSON reading and writing
        _handlers.Add(operation, async (body, cancellationToken) =>
        {
            TRequest request = MessageSerializer.ReadBody<TRequest>(body);
            TResponse response = await handler(request, cancellationToken).ConfigureAwait(false);
            return MessageSerializer.WriteResponse(response);
        });

        // Function's return
        return this;
    }

    /// <summary>
    /// Method used to answer one request: the handler given to <see cref="Correlation.RequestServer"/>
    /// </summary>
    /// <param name="request">UTF-8 JSON of the request envelope</param>
    /// <param name="cancellationToken">Token of the service of the connection</param>
    /// <returns>
    /// The UTF-8 JSON of the response envelope
    /// </returns>
    /// <exception cref="InvalidDataException">If the request is malformed or its operation is unknown</exception>
    public async Task<ReadOnlyMemory<byte>> HandleAsync(ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        // Reading the operation, then finding its handler
        (string operation, JsonElement body) = MessageSerializer.ReadRequest(request.Span);
        if (!_handlers.TryGetValue(operation, out Func<JsonElement, CancellationToken, Task<byte[]>>? handler))
            throw new InvalidDataException($"The operation {operation} is unknown");

        // Function's return
        return await handler(body, cancellationToken).ConfigureAwait(false);
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
