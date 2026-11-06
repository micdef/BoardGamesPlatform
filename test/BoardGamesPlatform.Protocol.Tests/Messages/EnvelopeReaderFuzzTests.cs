/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/EnvelopeReaderFuzzTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the properties of the request and response envelope readers on random and mutated JSON, with fixed seeds so that every failure can be replayed
 */

// Using
using System.Text;
using System.Text.Json;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Messages;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Property-based and fuzz tests used to check the request and response envelope readers of the socket protocol
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the readers parse JSON sent by a peer that cannot be trusted. Fixed examples prove the cases the authors thought of;
/// random and mutated inputs look for the others: whatever bytes arrive, a reader must return a value, throw a
/// <see cref="RemoteErrorException"/> (a response that carries an error code) or throw <see cref="InvalidDataException"/>, never anything
/// else, and never copy a text of the peer into an error message.</para>
/// <para><b>Behavior:</b> every random value comes from a <see cref="Random"/> built with a fixed seed, so a run is always the same. Every
/// check prints its seed or its mutation in its failure message: building a <see cref="Random"/> with that seed replays the failing case.
/// The helpers are in <see cref="JsonFuzzKit"/>. A request is read with <see cref="MessageSerializer.ReadRequest"/> then
/// <see cref="MessageSerializer.ReadBody{T}"/>, a response with <see cref="MessageSerializer.ReadResponse{TResponse}"/>.</para>
/// </remarks>
public sealed class EnvelopeReaderFuzzTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
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
    /// Method used to read a request the way the dispatcher does: the envelope, then the body
    /// </summary>
    /// <param name="request">[Cannot be null] Bytes of the request</param>
    private static void ReadNumberRequest(byte[] request)
    {
        // Reading
        (string _, JsonElement body) = MessageSerializer.ReadRequest(request);
        _ = MessageSerializer.ReadBody<NumberBody>(body);
    }

    /// <summary>
    /// Method used to read a request whose body holds a time
    /// </summary>
    /// <param name="request">[Cannot be null] Bytes of the request</param>
    private static void ReadTimeRequest(byte[] request)
    {
        // Reading
        (string _, JsonElement body) = MessageSerializer.ReadRequest(request);
        _ = MessageSerializer.ReadBody<TimeBody>(body);
    }

    /// <summary>
    /// Method used to read a response whose body holds a number
    /// </summary>
    /// <param name="response">[Cannot be null] Bytes of the response</param>
    private static void ReadNumberResponse(byte[] response)
    {
        // Reading
        _ = MessageSerializer.ReadResponse<NumberBody>(response);
    }

    /// <summary>
    /// Method used to read a response whose body holds a time
    /// </summary>
    /// <param name="response">[Cannot be null] Bytes of the response</param>
    private static void ReadTimeResponse(byte[] response)
    {
        // Reading
        _ = MessageSerializer.ReadResponse<TimeBody>(response);
    }

    /// <summary>
    /// Method used to list valid messages of every shape the readers know, with the reader of each
    /// </summary>
    /// <param name="withError">When false, the response that carries an error is left out (it is not a result but a <see cref="RemoteErrorException"/>)</param>
    /// <returns>
    /// The samples: name, valid bytes, reader, and whether the reader may throw a <see cref="RemoteErrorException"/>
    /// </returns>
    private static List<(string Name, byte[] Bytes, Action<byte[]> Reader, bool IsResponse)> Samples(bool withError)
    {
        // Writing the messages
        DateTimeOffset time = new DateTimeOffset(2026, 11, 5, 10, 30, 15, TimeSpan.FromHours(1));
        List<(string Name, byte[] Bytes, Action<byte[]> Reader, bool IsResponse)> samples = new List<(string Name, byte[] Bytes, Action<byte[]> Reader, bool IsResponse)>
        {
            ("number request", MessageSerializer.WriteRequest("Op.Number", new NumberBody(42)), ReadNumberRequest, false),
            ("time request", MessageSerializer.WriteRequest("Op.Time", new TimeBody(time)), ReadTimeRequest, false),
            ("number response", MessageSerializer.WriteResponse(new NumberBody(42)), ReadNumberResponse, true),
            ("time response", MessageSerializer.WriteResponse(new TimeBody(time)), ReadTimeResponse, true)
        };
        if (withError)
            samples.Add(("error response", MessageSerializer.WriteError("protocol.failed"), ReadNumberResponse, true));

        // Function's return
        return samples;
    }

    /// <summary>
    /// Method used to build a request whose body holds arrays nested to a given depth
    /// </summary>
    /// <param name="depth">Number of nested arrays inside the body</param>
    /// <returns>
    /// The request; its deepest level is <paramref name="depth"/> plus 2 (the envelope, the body)
    /// </returns>
    private static byte[] BuildNestedRequest(int depth)
    {
        // Function's return
        return Encoding.UTF8.GetBytes("{\"operation\":\"Op\",\"body\":{\"a\":" + new string('[', depth) + new string(']', depth) + "}}");
    }

    #endregion

    #region Publics

    #region Random bytes

    /// <summary>
    /// Random bytes, or valid messages with some bytes changed, only ever give a value, a remote error or an <see cref="InvalidDataException"/>
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a hostile peer sends anything; any other exception type would escape the handling of the connection.</para>
    /// <para><b>Behavior:</b> each seed gives random bytes to every reader, then a mutation of every valid sample to its own reader.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void RandomBytesAndMutatedMessagesOnlyGiveResultsOrInvalidDataExceptions()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        List<(string Name, byte[] Bytes, Action<byte[]> Reader, bool IsResponse)> samples = Samples(true);
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            // Random bytes, given to every reader
            Random random = new Random(seed);
            byte[] bytes = JsonFuzzKit.NextRandomBytes(random);
            foreach ((string name, byte[] _, Action<byte[]> reader, bool isResponse) in samples)
                JsonFuzzKit.AssertOnlyExpectedOutcomes(() => reader(bytes), $"seed {seed}, {name}, random bytes", isResponse);

            // Mutated messages
            foreach ((string name, byte[] valid, Action<byte[]> reader, bool isResponse) in samples)
            {
                byte[] mutated = JsonFuzzKit.NextMutation(random, valid);
                JsonFuzzKit.AssertOnlyExpectedOutcomes(() => reader(mutated), $"seed {seed}, {name}, mutated message {Encoding.UTF8.GetString(mutated)}", isResponse);
            }
        }
    }

    /// <summary>
    /// A message with one bit flipped, at every offset, only ever gives a value, a remote error or an <see cref="InvalidDataException"/>
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a corrupted byte can land anywhere (a name, a digit, a quote, a multi-byte sequence); the outcome may be a
    /// valid message with another value, but never another exception type.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AMessageWithOneBitFlippedOnlyGivesResultsOrInvalidDataExceptions()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid, Action<byte[]> reader, bool isResponse) in Samples(true))
        {
            foreach ((string flip, byte[] flipped) in JsonFuzzKit.BitFlips(new Random(1), valid))
                JsonFuzzKit.AssertOnlyExpectedOutcomes(() => reader(flipped), $"{name}, {flip}", isResponse);
        }
    }

    #endregion

    #region Round trips

    /// <summary>
    /// A request written by the serializer is read back with the same operation and body
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the writer and the reader of a request always agree, for the longest and the shortest operation names and the
    /// edges of the numbers and of the dates.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ARequestSurvivesARoundTrip()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            // Writing a random request, with a number or a time
            Random random = new Random(seed);
            string operation = JsonFuzzKit.NextOperationName(random);
            int number = JsonFuzzKit.NextInt(random);
            long ticks = random.NextInt64(TimeSpan.TicksPerDay * 366, DateTime.MaxValue.Ticks - (TimeSpan.TicksPerDay * 366));
            DateTimeOffset time = new DateTimeOffset(new DateTime(ticks), TimeSpan.FromMinutes(random.Next(-840, 841)));
            string context = $"seed {seed}, operation {operation}";

            // Reading it back
            (string actualOperation, JsonElement body) = MessageSerializer.ReadRequest(MessageSerializer.WriteRequest(operation, new NumberBody(number)));
            actualOperation.ShouldBe(operation, context);
            MessageSerializer.ReadBody<NumberBody>(body).Value.ShouldBe(number, context);
            (actualOperation, body) = MessageSerializer.ReadRequest(MessageSerializer.WriteRequest(operation, new TimeBody(time)));
            actualOperation.ShouldBe(operation, context);
            TimeBody actualTime = MessageSerializer.ReadBody<TimeBody>(body);
            actualTime.Time.Ticks.ShouldBe(time.Ticks, context);
            actualTime.Time.Offset.ShouldBe(time.Offset, context);
        }
    }

    /// <summary>
    /// A response, or an error answer, written by the serializer is read back identical
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a body comes back with the same values, and an error code comes back as the same <see cref="RemoteErrorException"/>.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AResponseAndAnErrorSurviveARoundTrip()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            // Writing and reading back a body
            Random random = new Random(seed);
            int number = JsonFuzzKit.NextInt(random);
            string code = JsonFuzzKit.NextErrorCode(random);
            string context = $"seed {seed}, number {number}, code {code}";
            MessageSerializer.ReadResponse<NumberBody>(MessageSerializer.WriteResponse(new NumberBody(number))).Value.ShouldBe(number, context);

            // Writing and reading back an error
            RemoteErrorException exception = Should.Throw<RemoteErrorException>(() => MessageSerializer.ReadResponse<NumberBody>(MessageSerializer.WriteError(code)), context);
            exception.Code.ShouldBe(code, context);
        }
    }

    #endregion

    #region Mutations

    /// <summary>
    /// A message cut at any offset is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a connection cut in the middle of a payload, or a peer that sends half a message, never gives a message: no
    /// proper prefix of a JSON object is a JSON document.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ATruncatedMessageIsRefusedAtEveryOffset()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid, Action<byte[]> reader, bool _) in Samples(true))
        {
            int offset = 0;
            foreach (byte[] prefix in JsonFuzzKit.Truncations(valid))
            {
                JsonFuzzKit.AssertRefused(() => reader(prefix), $"{name}, cut at {offset} of {valid.Length}");
                offset++;
            }
        }
    }

    /// <summary>
    /// A message whose structure is broken in one place is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the reader is strict: a property deleted, renamed, written in another case or twice, or given a value of the
    /// wrong kind (a number as a string, a string as a number, null, an object where a value is expected…) is never tolerated.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AMessageWithABrokenStructureIsRefused()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid, Action<byte[]> reader, bool _) in Samples(true))
        {
            foreach ((string mutation, byte[] mutated) in JsonFuzzKit.StructuralMutations(valid))
                JsonFuzzKit.AssertRefused(() => reader(mutated), $"{name}, {mutation}: {Encoding.UTF8.GetString(mutated)}");
        }
    }

    /// <summary>
    /// An input that is not a JSON object holding a message is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> empty input, other JSON values, two messages in one payload, trailing text, a byte order mark, comments and
    /// a trailing comma are not messages.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AnInputThatIsNotAMessageIsRefused()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid, Action<byte[]> reader, bool _) in Samples(true))
        {
            foreach ((string document, byte[] bytes) in JsonFuzzKit.OddDocuments(valid))
                JsonFuzzKit.AssertRefused(() => reader(bytes), $"{name}, {document}");
        }
    }

    /// <summary>
    /// A JSON nested deeper than the maximum is refused, up to a depth that would overflow a recursive parser
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a hostile peer must not exhaust the stack of the process with brackets. The envelope counts as one level: a body
    /// can go to <see cref="MessageSerializer.MaxDepth"/> levels in total and not one more.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ATooDeepMessageIsRefusedAndTheMaximumDepthIsAccepted()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Check: the maximum depth is accepted, one more is refused (the envelope and the body are two levels)
        MessageSerializer.ReadRequest(BuildNestedRequest(MessageSerializer.MaxDepth - 2)).Operation.ShouldBe("Op");
        JsonFuzzKit.AssertRefused(() => MessageSerializer.ReadRequest(BuildNestedRequest(MessageSerializer.MaxDepth - 1)), "one level over the maximum");

        // Check: far deeper messages, each reader
        foreach (int depth in new[] { 40, 1000, 100000 })
            JsonFuzzKit.AssertRefused(() => MessageSerializer.ReadRequest(BuildNestedRequest(depth)), $"{depth} nested arrays in a request body");
        foreach ((string name, byte[] valid, Action<byte[]> reader, bool _) in Samples(true))
        {
            foreach ((string document, byte[] bytes) in JsonFuzzKit.TooDeepDocuments(valid))
                JsonFuzzKit.AssertRefused(() => reader(bytes), $"{name}, {document}");
        }
    }

    #endregion

    #region Peer text

    /// <summary>
    /// A refusal never contains a text of the input, whatever the place of that text in the message
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the protocol promises that the error messages reach the logs without any text of the peer, so that a peer cannot
    /// write fake log lines or control characters.</para>
    /// <para><b>Behavior:</b> a random marker (see <see cref="JsonFuzzKit.NextMarker"/>) is put in a property name or in a value of
    /// requests and responses; every refusal is searched for the marker, including its inner exception and its stack trace.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ARefusalNeverCopiesTheTextOfThePeer()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // The messages, the marker standing for @M@
        string[] requests =
        [
            """{@M@:1,"operation":"Op","body":{"value":1}}""",
            """{"operation":"Op","body":{@M@:1}}""",
            """{"operation":@M@,"body":{"value":1}}""",
            """{"operation":"Op","body":{"value":@M@}}""",
            """{"operation":"Op","body":@M@}""",
            """{"operation":"Op","body":{"value":1},@M@:@M@}"""
        ];
        string[] responses =
        [
            """{"body":{"value":1},@M@:2}""",
            """{"body":{@M@:1}}""",
            """{"body":{"value":@M@}}""",
            """{"body":@M@}""",
            """{"error":{"code":@M@}}""",
            """{"error":{"code":"a.b",@M@:1}}""",
            """{"error":{@M@:"a.b"}}""",
            """{@M@:{"code":"a.b"}}"""
        ];

        // Running the cases
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            Random random = new Random(seed);
            string marker = JsonFuzzKit.NextMarker(random);
            string quoted = JsonFuzzKit.Quote(marker);
            foreach (string template in requests)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(template.Replace("@M@", quoted, StringComparison.Ordinal));
                JsonFuzzKit.AssertNoPeerText(() => ReadNumberRequest(bytes), marker, $"seed {seed}, request {template}", true);
            }
            foreach (string template in responses)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(template.Replace("@M@", quoted, StringComparison.Ordinal));
                JsonFuzzKit.AssertNoPeerText(() => ReadNumberResponse(bytes), marker, $"seed {seed}, response {template}", true);
            }
        }
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
