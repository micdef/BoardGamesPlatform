/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/MessageSerializer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Turns the requests and responses of the links into UTF-8 JSON envelopes, and parses them back strictly
 */

// Using
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Messages;

/// <summary>
/// Serializer used to write and read the JSON envelopes carried by the frames of the links
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one single place that decides the JSON format of every message between the tiers, so that both ends of a link always
/// agree, and that a malformed message is refused the same way everywhere.</para>
/// <para><b>Behavior:</b> a request is <c>{"operation":"&lt;name&gt;","body":{…}}</c>, a response is <c>{"body":{…}}</c>, in UTF-8, with camelCase
/// property names. Reading is strict and refuses with an <see cref="InvalidDataException"/>, which the request client and the request server
/// treat as a protocol violation: malformed JSON, an unknown property, a property written twice or in another case, a missing envelope part
/// or constructor parameter, a null body, a number written as a string, a date without its offset, and a JSON nested deeper than
/// <see cref="MaxDepth"/> levels (the envelope counts as one level). Version and error code fields come with 0.4.0.</para>
/// <para>Known limits, for the bodies that come later: a lone UTF-16 surrogate in a string is replaced by U+FFFD instead of refused, and the
/// nullable annotations are not checked on the elements of a collection nor on enumeration values; a body with such members checks them itself.</para>
/// </remarks>
public static partial class MessageSerializer
{
    #region Members

    #region Constants

    /// <summary>
    /// Deepest JSON nesting accepted in a message, envelope included; the messages of the platform are shallow, a deeper one is refused
    /// </summary>
    public const int MaxDepth = 16;

    /// <summary>
    /// Longest operation name accepted in a request envelope
    /// </summary>
    public const int MaxOperationNameLength = 64;

    #endregion

    #region Variables

    /// <summary>
    /// JSON options shared by every message: camelCase names, strict reading
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the web defaults give the camelCase names, but three of their reading rules are tolerant and are turned off here:
    /// names are matched case-sensitively, a property may appear only once (otherwise two readers could see different values), and a number
    /// must be a JSON number, never a string. Dates must carry their offset (<see cref="StrictDateTimeOffsetConverter"/>).</para>
    /// </remarks>
    private static readonly JsonSerializerOptions _options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        MaxDepth = MaxDepth,
        PropertyNameCaseInsensitive = false,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new StrictDateTimeOffsetConverter() }
    };

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
    /// Method used to get the pattern of a valid operation name
    /// </summary>
    /// <returns>
    /// The compiled pattern: 1 to 64 characters among A-Z, a-z, 0-9, '.', '_' and '-'
    /// </returns>
    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex OperationNamePattern();

    /// <summary>
    /// Method used to parse JSON strictly, turning any parsing failure into a protocol violation
    /// </summary>
    /// <typeparam name="T">Type to read</typeparam>
    /// <param name="json">UTF-8 JSON to read</param>
    /// <param name="what">[Cannot be null] What is read, for the error message</param>
    /// <returns>
    /// The value read
    /// </returns>
    /// <exception cref="InvalidDataException">If the JSON is malformed, does not match the type, or is the JSON value null</exception>
    private static T Read<T>(ReadOnlySpan<byte> json, string what)
    {
        // Reading, the parsing errors becoming protocol violations
        T? value;
        try
        {
            value = JsonSerializer.Deserialize<T>(json, _options);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new InvalidDataException($"The {what} is not valid: {exception.Message}", exception);
        }

        // Check
        if (value is null)
            throw new InvalidDataException($"The {what} is the JSON value null");

        // Function's return
        return value;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to write a request envelope
    /// </summary>
    /// <typeparam name="TRequest">Type of the request body</typeparam>
    /// <param name="operation">[Cannot be null] Name of the operation, for example "Ping"</param>
    /// <param name="body">[Cannot be null] Body of the request</param>
    /// <returns>
    /// The UTF-8 JSON of the envelope
    /// </returns>
    /// <exception cref="ArgumentNullException">If the operation is null, empty or white spaces, or if the body is null</exception>
    public static byte[] WriteRequest<TRequest>(string operation, TRequest body)
        where TRequest : class
    {
        // Checks
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentNullException(nameof(operation), $"The value of the parameter: {nameof(operation)} cannot be null or empty");
        if (body is null)
            throw new ArgumentNullException(nameof(body), $"The value of the parameter: {nameof(body)} cannot be null");

        // Function's return
        return JsonSerializer.SerializeToUtf8Bytes(new RequestEnvelope<TRequest>(operation, body), _options);
    }

    /// <summary>
    /// Method used to read the operation name of a request, before its body is read with the type of that operation
    /// </summary>
    /// <param name="request">UTF-8 JSON of the request envelope</param>
    /// <returns>
    /// The name of the operation and the raw body, to be read by <see cref="ReadBody{T}"/>
    /// </returns>
    /// <exception cref="InvalidDataException">If the envelope is malformed or its operation is missing</exception>
    public static (string Operation, JsonElement Body) ReadRequest(ReadOnlySpan<byte> request)
    {
        // Reading the envelope, the body kept raw
        RequestEnvelope<JsonElement> envelope = Read<RequestEnvelope<JsonElement>>(request, "request envelope");

        // Checking the operation name before it is used or logged: a short ASCII identifier only (no control or invisible character)
        if (envelope.Operation is null || !OperationNamePattern().IsMatch(envelope.Operation))
            throw new InvalidDataException($"The operation name of the request envelope is not a valid name (1 to {MaxOperationNameLength} characters among A-Z, a-z, 0-9, '.', '_', '-')");

        // Checking the body: a JSON object, whose text is valid UTF-8 (otherwise reading it later would fail with another exception type)
        if (envelope.Body.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("The body of the request envelope is not a JSON object");
        try
        {
            _ = envelope.Body.GetRawText();
        }
        catch (InvalidOperationException exception)
        {
            throw new InvalidDataException("The body of the request envelope is not valid UTF-8", exception);
        }

        // Function's return
        return (envelope.Operation, envelope.Body);
    }

    /// <summary>
    /// Method used to read a raw body with the type of its operation
    /// </summary>
    /// <typeparam name="T">Type of the body</typeparam>
    /// <param name="body">Raw body of a request or a response</param>
    /// <returns>
    /// The body read
    /// </returns>
    /// <exception cref="InvalidDataException">If the body does not match the type, or is the JSON value null</exception>
    public static T ReadBody<T>(JsonElement body)
        where T : class
    {
        // Reading, the parsing errors becoming protocol violations
        T? value;
        try
        {
            value = body.Deserialize<T>(_options);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new InvalidDataException($"The message body is not valid: {exception.Message}", exception);
        }

        // Check
        if (value is null)
            throw new InvalidDataException("The message body is the JSON value null");

        // Function's return
        return value;
    }

    /// <summary>
    /// Method used to write a response envelope
    /// </summary>
    /// <typeparam name="TResponse">Type of the response body</typeparam>
    /// <param name="body">[Cannot be null] Body of the response</param>
    /// <returns>
    /// The UTF-8 JSON of the envelope
    /// </returns>
    /// <exception cref="ArgumentNullException">If the body is null</exception>
    public static byte[] WriteResponse<TResponse>(TResponse body)
        where TResponse : class
    {
        // Check
        if (body is null)
            throw new ArgumentNullException(nameof(body), $"The value of the parameter: {nameof(body)} cannot be null");

        // Function's return
        return JsonSerializer.SerializeToUtf8Bytes(new ResponseEnvelope<TResponse>(body), _options);
    }

    /// <summary>
    /// Method used to read a response envelope and its body
    /// </summary>
    /// <typeparam name="TResponse">Type of the response body</typeparam>
    /// <param name="response">UTF-8 JSON of the response envelope</param>
    /// <returns>
    /// The body of the response
    /// </returns>
    /// <exception cref="InvalidDataException">If the envelope or its body is malformed</exception>
    public static TResponse ReadResponse<TResponse>(ReadOnlySpan<byte> response)
        where TResponse : class
    {
        // Reading the envelope; a null body is refused explicitly, since the nullable annotations are not checked on a generic body
        TResponse? body = Read<ResponseEnvelope<TResponse?>>(response, "response envelope").Body;
        if (body is null)
            throw new InvalidDataException("The body of the response envelope is the JSON value null");

        // Function's return
        return body;
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
