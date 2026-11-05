/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/OperationDispatcher.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Server side of the messages: routes each request to the handler of its operation
 */

// Using
using System.Text.Json;
using BoardGamesPlatform.Protocol.Errors;
using Microsoft.Extensions.Logging;

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
/// <para><b>Behavior:</b> the operations are registered before the server starts and matched by exact name (ordinal, case-sensitive).
/// Registering is not thread-safe: it is done once, at startup. A request that reached the right operation always gets an answer, so that the
/// connection, shared by every caller, survives the failure of one request:</para>
/// <list type="bullet">
/// <item><description>an unknown operation is answered <see cref="ErrorCodes.RequestUnknownOperation"/>, a body that does not match its
/// operation <see cref="ErrorCodes.RequestInvalid"/>;</description></item>
/// <item><description>a handler that fails because a tier behind it cannot be reached (<see cref="LinkFailureException"/>) is answered with
/// the code of that failure, any other failure with <see cref="ErrorCodes.Internal"/>;</description></item>
/// <item><description>a cancellation of the token of the request is not an answer: it is thrown to the request server, which then sends
/// nothing.</description></item>
/// </list>
/// <para>A malformed envelope is still a protocol violation: <see cref="HandleAsync"/> throws an <see cref="InvalidDataException"/>, which
/// ends the service of the connection. Every failure is logged with its exception, through the logger of the tier, with the event ids 9021 to
/// 9024, reserved for this class (see the coding conventions); the peer only receives the code.</para>
/// </remarks>
public sealed partial class OperationDispatcher
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

    /// <summary>
    /// Logger of the tier that answers
    /// </summary>
    private readonly ILogger _logger;

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
    /// <param name="logger">[Cannot be null] Logger of the tier that answers, which receives the details of every failure</param>
    /// <exception cref="ArgumentNullException">If the logger is null</exception>
    public OperationDispatcher(ILogger logger)
    {
        // Check
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting
        _handlers = new Dictionary<string, Func<JsonElement, CancellationToken, Task<byte[]>>>(StringComparer.Ordinal);
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log a request for an operation that is not registered
    /// </summary>
    /// <param name="operation">Name of the operation (checked by the serializer: a short ASCII identifier)</param>
    [LoggerMessage(EventId = 9021, Level = LogLevel.Warning, Message = "Request for the unknown operation {Operation}; answered request.unknownOperation")]
    private partial void LogUnknownOperation(string operation);

    /// <summary>
    /// Method used to log a request whose body does not match its operation
    /// </summary>
    /// <param name="exception">Why the body was refused</param>
    /// <param name="operation">Name of the operation</param>
    [LoggerMessage(EventId = 9022, Level = LogLevel.Warning, Message = "Request for {Operation} with an invalid body; answered request.invalid")]
    private partial void LogInvalidBody(Exception exception, string operation);

    /// <summary>
    /// Method used to log a handler that failed because a tier behind it cannot be reached
    /// </summary>
    /// <param name="exception">Failure of the link</param>
    /// <param name="operation">Name of the operation</param>
    /// <param name="code">Code answered</param>
    [LoggerMessage(EventId = 9023, Level = LogLevel.Warning, Message = "The handler of {Operation} could not reach a tier; answered {Code}")]
    private partial void LogLinkFailure(Exception exception, string operation, string code);

    /// <summary>
    /// Method used to log a handler that failed
    /// </summary>
    /// <param name="exception">Failure of the handler</param>
    /// <param name="operation">Name of the operation</param>
    [LoggerMessage(EventId = 9024, Level = LogLevel.Error, Message = "The handler of {Operation} failed; answered internal")]
    private partial void LogHandlerFailed(Exception exception, string operation);

    #endregion

    #region Publics

    /// <summary>
    /// Method used to declare the handler of an operation
    /// </summary>
    /// <typeparam name="TRequest">Type of the request body of the operation</typeparam>
    /// <typeparam name="TResponse">Type of the response body of the operation</typeparam>
    /// <param name="operation">[Cannot be null] Name of the operation, for example "Ping"</param>
    /// <param name="handler">[Cannot be null] Handler: receives the request body and the token of the request, returns the response body</param>
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

        // Registering the typed handler behind the JSON reading and writing, each failure becoming its code
        _handlers.Add(operation, async (body, cancellationToken) =>
        {
            // Reading the body with the type of the operation
            TRequest request;
            try
            {
                request = MessageSerializer.ReadBody<TRequest>(body);
            }
            catch (InvalidDataException exception)
            {
                LogInvalidBody(exception, operation);

                // Function's return
                return MessageSerializer.WriteError(ErrorCodes.RequestInvalid);
            }

            // Running the handler; the cancellation of the request is not answered
            TResponse response;
            try
            {
                response = await handler(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (LinkFailureException exception)
            {
                LogLinkFailure(exception, operation, exception.Code);

                // Function's return
                return MessageSerializer.WriteError(exception.Code);
            }
            catch (Exception exception)
            {
                LogHandlerFailed(exception, operation);

                // Function's return
                return MessageSerializer.WriteError(ErrorCodes.Internal);
            }

            // Function's return
            return MessageSerializer.WriteResponse(response);
        });

        // Function's return
        return this;
    }

    /// <summary>
    /// Method used to answer one request: the handler given to <see cref="Correlation.RequestServer"/>
    /// </summary>
    /// <param name="request">UTF-8 JSON of the request envelope</param>
    /// <param name="cancellationToken">Token of the request</param>
    /// <returns>
    /// The UTF-8 JSON of the response envelope: the body of the operation, or an error code
    /// </returns>
    /// <exception cref="InvalidDataException">If the envelope of the request is malformed</exception>
    /// <exception cref="OperationCanceledException">If the request is canceled</exception>
    public async Task<ReadOnlyMemory<byte>> HandleAsync(ReadOnlyMemory<byte> request, CancellationToken cancellationToken)
    {
        // Reading the operation, then finding its handler
        (string operation, JsonElement body) = MessageSerializer.ReadRequest(request.Span);
        if (!_handlers.TryGetValue(operation, out Func<JsonElement, CancellationToken, Task<byte[]>>? handler))
        {
            LogUnknownOperation(operation);

            // Function's return
            return MessageSerializer.WriteError(ErrorCodes.RequestUnknownOperation);
        }

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
