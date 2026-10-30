/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/MessageTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the JSON envelopes of the messages, their strict reading and the routing of the operations
 */

// Using
using System.Net;
using System.Text;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Tests used to check the message serializer, the operation dispatcher and the typed requests of the request client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the messages come from another tier and must be read as untrusted input: anything outside the expected shape is
/// refused as a protocol violation, never guessed.</para>
/// <para><b>Behavior:</b> the wire format is pinned byte for byte; malformed inputs are written by hand; one test runs a typed request through a
/// real loopback connection.</para>
/// </remarks>
public sealed class MessageTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a network test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

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

    /// <summary>
    /// Method used to build a dispatcher with an "Add" operation that adds one to a number
    /// </summary>
    /// <returns>
    /// The dispatcher
    /// </returns>
    private static OperationDispatcher CreateDispatcher()
    {
        // Function's return
        return new OperationDispatcher()
            .Register<NumberBody, NumberBody>("Add", (request, cancellationToken) => Task.FromResult(new NumberBody(request.Value + 1)));
    }

    /// <summary>
    /// Method used to run a request through the dispatcher, written by hand as JSON text
    /// </summary>
    /// <param name="json">[Cannot be null] JSON text of the request</param>
    /// <returns>
    /// The JSON text of the response
    /// </returns>
    private static async Task<string> DispatchAsync(string json)
    {
        // Running the request
        ReadOnlyMemory<byte> response = await CreateDispatcher().HandleAsync(Encoding.UTF8.GetBytes(json), TestContext.Current.CancellationToken);

        // Function's return
        return Encoding.UTF8.GetString(response.Span);
    }

    #endregion

    #region Publics

    #region Wire format

    /// <summary>
    /// A request is written as an envelope holding the operation and the body, in camelCase
    /// </summary>
    [Fact]
    public void ARequestFollowsTheWireFormat()
    {
        // Writing a request
        string json = Encoding.UTF8.GetString(MessageSerializer.WriteRequest("Add", new NumberBody(41)));

        // Check
        json.ShouldBe("{\"operation\":\"Add\",\"body\":{\"value\":41}}");
    }

    /// <summary>
    /// A response is written as an envelope holding the body, and an empty body as an empty object
    /// </summary>
    [Fact]
    public void AResponseFollowsTheWireFormat()
    {
        // Writing two responses
        string numberJson = Encoding.UTF8.GetString(MessageSerializer.WriteResponse(new NumberBody(42)));
        string emptyJson = Encoding.UTF8.GetString(MessageSerializer.WriteResponse(new EmptyBody()));

        // Checks
        numberJson.ShouldBe("{\"body\":{\"value\":42}}");
        emptyJson.ShouldBe("{\"body\":{}}");
    }

    #endregion

    #region Dispatching

    /// <summary>
    /// The dispatcher runs the handler of the requested operation and returns its response in an envelope
    /// </summary>
    [Fact]
    public async Task TheDispatcherRunsTheHandlerOfTheOperationAsync()
    {
        // Check
        (await DispatchAsync("{\"operation\":\"Add\",\"body\":{\"value\":1}}")).ShouldBe("{\"body\":{\"value\":2}}");
    }

    /// <summary>
    /// A request that does not match the expected shape is refused as a protocol violation
    /// </summary>
    /// <param name="json">Malformed request</param>
    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\"}")]
    [InlineData("{\"operation\":\"\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":null}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":\"one\"}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1,\"extra\":true}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1},\"extra\":true}")]
    [InlineData("{\"operation\":\"Unknown\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"add\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add \",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"A\\nB\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\\u0000\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"operation\":\"Other\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1},\"body\":{\"value\":2}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1,\"value\":2}}")]
    [InlineData("{\"Operation\":\"Add\",\"body\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"BODY\":{\"value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"Value\":1}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":\"1\"}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":[]}")]
    [InlineData("{\"operation\":\"Add\",\"body\":\"x\"}")]
    [InlineData("{\"operation\":\"Add\",\"body\":1}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1}} x")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1}}{}")]
    [InlineData("{\"operation\":\"Add\",\"body\":{\"value\":1},}")]
    [InlineData("{\"operation\":\"Add\",/*c*/\"body\":{\"value\":1}}")]
    public async Task AMalformedRequestIsRefusedAsync(string json)
    {
        // Check
        await Should.ThrowAsync<InvalidDataException>(() => DispatchAsync(json));
    }

    /// <summary>
    /// Reading a request refuses at once an invalid operation name, without copying it into the error, and a body that is not an object
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> defense in depth: the dispatcher would refuse these requests later anyway, but the name must never reach a log
    /// (line breaks, escape sequences), and a caller of the reading alone must get an object body or an error.</para>
    /// </remarks>
    /// <param name="json">Request with an invalid operation name or body</param>
    [Theory]
    [InlineData("{\"operation\":\"A\\nINJECTED\",\"body\":{}}")]
    [InlineData("{\"operation\":\"\\u001b[31mINJECTED\",\"body\":{}}")]
    [InlineData("{\"operation\":\"INJECTED \",\"body\":{}}")]
    [InlineData("{\"operation\":\"Add\",\"body\":[]}")]
    [InlineData("{\"operation\":\"Add\",\"body\":null}")]
    [InlineData("{\"operation\":\"Add\",\"body\":\"INJECTED\"}")]
    public void ReadingARequestRefusesAnInvalidNameOrBody(string json)
    {
        // Check
        InvalidDataException exception = Should.Throw<InvalidDataException>(() => MessageSerializer.ReadRequest(Encoding.UTF8.GetBytes(json)));
        exception.Message.ShouldNotContain("INJECTED");
    }

    /// <summary>
    /// An operation name longer than the maximum is refused
    /// </summary>
    [Fact]
    public async Task ATooLongOperationNameIsRefusedAsync()
    {
        // Setting a name one character too long
        string name = new string('A', MessageSerializer.MaxOperationNameLength + 1);

        // Check
        await Should.ThrowAsync<InvalidDataException>(() => DispatchAsync("{\"operation\":\"" + name + "\",\"body\":{\"value\":1}}"));
    }

    /// <summary>
    /// Bytes that are not valid UTF-8, or that start with a byte order mark, are refused
    /// </summary>
    [Fact]
    public async Task InvalidBytesAreRefusedAsync()
    {
        // Setting a request with an invalid UTF-8 byte inside the body, and a valid request preceded by a byte order mark
        byte[] invalidUtf8 = Encoding.UTF8.GetBytes("{\"operation\":\"Add\",\"body\":{\"x\":\"?\"}}");
        invalidUtf8[Array.IndexOf(invalidUtf8, (byte)'?')] = 0xFF;
        byte[] withByteOrderMark = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"operation\":\"Add\",\"body\":{\"value\":1}}")).ToArray();
        OperationDispatcher dispatcher = CreateDispatcher();

        // Checks
        await Should.ThrowAsync<InvalidDataException>(() => dispatcher.HandleAsync(invalidUtf8, TestContext.Current.CancellationToken));
        await Should.ThrowAsync<InvalidDataException>(() => dispatcher.HandleAsync(withByteOrderMark, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// The nesting limit counts the envelope: a body nested up to the limit is accepted, one level more is refused
    /// </summary>
    [Fact]
    public void TheNestingLimitIsExact()
    {
        // Setting a body of 15 nested objects (16 levels with the envelope), and one of 16 nested objects (17 levels)
        static byte[] BuildRequest(int bodyLevels)
        {
            string body = string.Concat(Enumerable.Repeat("{\"a\":", bodyLevels - 1)) + "{}" + new string('}', bodyLevels - 1);
            return Encoding.UTF8.GetBytes("{\"operation\":\"Add\",\"body\":" + body + "}");
        }

        // Checks
        MessageSerializer.ReadRequest(BuildRequest(MessageSerializer.MaxDepth - 1)).Operation.ShouldBe("Add");
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadRequest(BuildRequest(MessageSerializer.MaxDepth)));
    }

    #endregion

    #region Dates

    /// <summary>
    /// A date with its offset is read as the same instant, and is written back in the round-trip format
    /// </summary>
    [Fact]
    public void ADateWithItsOffsetMakesARoundTrip()
    {
        // Writing and reading a date
        DateTimeOffset time = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1)).AddTicks(1234567);
        byte[] json = MessageSerializer.WriteResponse(new TimeBody(time));
        TimeBody read = MessageSerializer.ReadResponse<TimeBody>(json);

        // Checks (the default JSON encoder escapes "+" as +, a valid JSON escape that every reader turns back into "+")
        Encoding.UTF8.GetString(json).ShouldBe("{\"body\":{\"time\":\"2026-10-30T09:15:00.1234567\\u002B01:00\"}}");
        read.Time.ShouldBe(time);
        read.Time.Offset.ShouldBe(TimeSpan.FromHours(1));
        MessageSerializer.ReadResponse<TimeBody>(Encoding.UTF8.GetBytes("{\"body\":{\"time\":\"2026-10-30T08:15:00Z\"}}")).Time.ShouldBe(time.AddTicks(-1234567));
    }

    /// <summary>
    /// A date without its offset, or not written as ISO 8601 with a time, is refused instead of being given the time zone of the reader
    /// </summary>
    /// <param name="date">Invalid date, as JSON</param>
    [Theory]
    [InlineData("\"2026-10-30T09:15:00\"")]
    [InlineData("\"2026-10-30\"")]
    [InlineData("\"2026-10-30T09:15Z\"")]
    [InlineData("\"2026-10-30T09:15:00+02\"")]
    [InlineData("\"2026-10-30T09:15:00+0200\"")]
    [InlineData("\"2026-10-30T09:15:00.123456789Z\"")]
    [InlineData("\"2026-10-30 09:15:00Z\"")]
    [InlineData("\"30/10/2026 09:15:00 +01:00\"")]
    [InlineData("1793351700")]
    [InlineData("null")]
    public void AnInvalidDateIsRefused(string date)
    {
        // Check
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadResponse<TimeBody>(Encoding.UTF8.GetBytes("{\"body\":{\"time\":" + date + "}}")));
    }

    #endregion

    #region Contracts

    /// <summary>
    /// The Ping bodies of the contracts make a round trip, and a response of one tier is read by the contract of the next tier
    /// </summary>
    [Fact]
    public void ThePingContractsMakeARoundTrip()
    {
        // Writing the Ping request of the Back, and the Ping response of the Very Back
        DateTimeOffset time = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));
        byte[] request = MessageSerializer.WriteRequest(VeryBack.Contracts.VeryBackOperations.Ping, new VeryBack.Contracts.Ping.PingRequest());
        byte[] response = MessageSerializer.WriteResponse(new VeryBack.Contracts.Ping.PingResponse(time));

        // Checks
        Encoding.UTF8.GetString(request).ShouldBe("{\"operation\":\"Ping\",\"body\":{}}");
        MessageSerializer.ReadResponse<VeryBack.Contracts.Ping.PingResponse>(response).ServerTime.ShouldBe(time);
        MessageSerializer.ReadResponse<Back.Contracts.Ping.PingResponse>(response).ServerTime.ShouldBe(time);
        MessageSerializer.ReadResponse<Gateway.Contracts.Ping.PingResponse>(response).ServerTime.ShouldBe(time);
        Back.Contracts.BackOperations.Ping.ShouldBe(VeryBack.Contracts.VeryBackOperations.Ping);
    }

    #endregion

    #region Arguments

    /// <summary>
    /// Null arguments are refused before anything is written or sent
    /// </summary>
    [Fact]
    public async Task NullArgumentsAreRefusedAsync()
    {
        // Checks
        Should.Throw<ArgumentNullException>(() => MessageSerializer.WriteRequest<NumberBody>(" ", new NumberBody(1)));
        Should.Throw<ArgumentNullException>(() => MessageSerializer.WriteRequest<NumberBody>("Add", null!));
        Should.Throw<ArgumentNullException>(() => MessageSerializer.WriteResponse<NumberBody>(null!));
        ArgumentNullException exception = await Should.ThrowAsync<ArgumentNullException>(() =>
            RequestClientMessageExtensions.SendAsync<NumberBody, NumberBody>(null!, "Add", new NumberBody(1), TestContext.Current.CancellationToken));
        exception.ParamName.ShouldBe("client");
    }

    /// <summary>
    /// An operation cannot be registered twice, nor without a name or a handler
    /// </summary>
    [Fact]
    public void InvalidRegistrationsAreRefused()
    {
        // Checks
        Should.Throw<ArgumentException>(() => CreateDispatcher()
            .Register<NumberBody, NumberBody>("Add", (request, cancellationToken) => Task.FromResult(request)));
        Should.Throw<ArgumentNullException>(() => new OperationDispatcher()
            .Register<NumberBody, NumberBody>(" ", (request, cancellationToken) => Task.FromResult(request)));
        Should.Throw<ArgumentNullException>(() => new OperationDispatcher().Register<NumberBody, NumberBody>("Add", null!));
    }

    #endregion

    #region Typed requests

    /// <summary>
    /// A typed request crosses a real connection and comes back as a typed response
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ATypedRequestCrossesAConnectionAsync()
    {
        // Serving a loopback connection with the dispatcher
        CancellationToken token = TestContext.Current.CancellationToken;
        await using TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, 0));
        Task<ITransportConnection> accepting = listener.AcceptAsync(token);
        ITransportConnection clientEnd = await new TcpTransportConnector().ConnectAsync("127.0.0.1", listener.LocalEndPoint.Port, token);
        await using ITransportConnection serverEnd = await accepting;
        Task serving = new RequestServer(new FrameCodec(), CreateDispatcher().HandleAsync).ServeAsync(serverEnd, token);
        await using RequestClient client = new RequestClient(clientEnd, new FrameCodec());

        // Sending a typed request
        NumberBody response = await client.SendAsync<NumberBody, NumberBody>("Add", new NumberBody(41), token);

        // Checks (closing the client ends the service cleanly)
        response.Value.ShouldBe(42);
        await client.DisposeAsync();
        await serving;
    }

    /// <summary>
    /// A malformed response is refused as a protocol violation
    /// </summary>
    /// <param name="json">Malformed response</param>
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"body\":null}")]
    [InlineData("{\"body\":{\"value\":1},\"extra\":1}")]
    [InlineData("{\"value\":1}")]
    [InlineData("{\"body\":\"x\"}")]
    [InlineData("{\"body\":[]}")]
    [InlineData("{\"body\":{\"value\":1},\"body\":{\"value\":2}}")]
    [InlineData("{\"Body\":{\"value\":1}}")]
    public void AMalformedResponseIsRefused(string json)
    {
        // Check
        Should.Throw<InvalidDataException>(() => MessageSerializer.ReadResponse<NumberBody>(Encoding.UTF8.GetBytes(json)));
    }

    #endregion

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
