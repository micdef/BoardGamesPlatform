/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Control/ControlReaderFuzzTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the properties of the control message reader on random and mutated JSON, with fixed seeds so that every failure can be replayed
 */

// Using
using System.Text;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Tests.Messages;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Control;

/// <summary>
/// Property-based and fuzz tests used to check the control message reader of the socket protocol
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the first message of a connection, and every control message after it, is read from bytes sent by a peer that
/// cannot be trusted: whatever arrives, <see cref="MessageSerializer.ReadControl"/> must return a message or throw
/// <see cref="InvalidDataException"/>, never anything else, and never copy a text of the peer into an error message.</para>
/// <para><b>Behavior:</b> every random value comes from a <see cref="Random"/> built with a fixed seed, so a run is always the same. Every
/// check prints its seed or its mutation in its failure message: building a <see cref="Random"/> with that seed replays the failing case.
/// The helpers are in <see cref="JsonFuzzKit"/>.</para>
/// </remarks>
public sealed class ControlReaderFuzzTests
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
    /// Method used to read a control message and drop it
    /// </summary>
    /// <param name="payload">[Cannot be null] Bytes of the payload</param>
    private static void Read(byte[] payload)
    {
        // Reading
        _ = MessageSerializer.ReadControl(payload);
    }

    /// <summary>
    /// Method used to list valid control messages, one of each kind
    /// </summary>
    /// <returns>
    /// The name and the bytes of each message
    /// </returns>
    private static List<(string Name, byte[] Bytes)> Samples()
    {
        // Function's return
        return new List<(string Name, byte[] Bytes)>
        {
            ("hello", MessageSerializer.WriteControl(new HelloMessage(1, 2, 1048576))),
            ("helloAck", MessageSerializer.WriteControl(new HelloAckMessage(1, 1048576, 64))),
            ("helloRefused", MessageSerializer.WriteControl(new HelloRefusedMessage("protocol.versionUnsupported"))),
            ("cancel", MessageSerializer.WriteControl(new CancelMessage(42))),
            ("ping", MessageSerializer.WriteControl(HeartbeatMessage.Ping)),
            ("pong", MessageSerializer.WriteControl(HeartbeatMessage.Pong))
        };
    }

    #endregion

    #region Publics

    #region Random bytes

    /// <summary>
    /// Random bytes, or valid control messages with some bytes changed, only ever give a message or an <see cref="InvalidDataException"/>
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a hostile peer sends anything, from its very first bytes; any other exception type would escape the handling
    /// of the connection.</para>
    /// <para><b>Behavior:</b> each seed gives random bytes, then a mutation of every valid sample.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void RandomBytesAndMutatedMessagesOnlyGiveMessagesOrInvalidDataExceptions()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        List<(string Name, byte[] Bytes)> samples = Samples();
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            // Random bytes
            Random random = new Random(seed);
            byte[] bytes = JsonFuzzKit.NextRandomBytes(random);
            JsonFuzzKit.AssertOnlyExpectedOutcomes(() => Read(bytes), $"seed {seed}, random bytes", false);

            // Mutated messages
            foreach ((string name, byte[] valid) in samples)
            {
                byte[] mutated = JsonFuzzKit.NextMutation(random, valid);
                JsonFuzzKit.AssertOnlyExpectedOutcomes(() => Read(mutated), $"seed {seed}, {name}, mutated message {Encoding.UTF8.GetString(mutated)}", false);
            }
        }
    }

    /// <summary>
    /// A control message with one bit flipped, at every offset, only ever gives a message or an <see cref="InvalidDataException"/>
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a corrupted byte can turn a kind into another one, a number into another number or break a name; the outcome
    /// may be a valid message, but never another exception type.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AMessageWithOneBitFlippedOnlyGivesMessagesOrInvalidDataExceptions()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid) in Samples())
        {
            foreach ((string flip, byte[] flipped) in JsonFuzzKit.BitFlips(new Random(1), valid))
                JsonFuzzKit.AssertOnlyExpectedOutcomes(() => Read(flipped), $"{name}, {flip}", false);
        }
    }

    #endregion

    #region Round trips

    /// <summary>
    /// A control message written by the serializer is read back with the same type and values
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> both ends of a link always agree on the six kinds, whatever the numbers they carry (zero, the edges, random).</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AControlMessageSurvivesARoundTrip()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            // Building random messages
            Random random = new Random(seed);
            int first = JsonFuzzKit.NextInt(random);
            int second = JsonFuzzKit.NextInt(random);
            int third = JsonFuzzKit.NextInt(random);
            ulong correlationId = JsonFuzzKit.NextUlong(random);
            string code = JsonFuzzKit.NextErrorCode(random);
            string context = $"seed {seed}, numbers {first} {second} {third}, correlation identifier {correlationId}, code {code}";

            // Reading them back
            HelloMessage hello = MessageSerializer.ReadControl(MessageSerializer.WriteControl(new HelloMessage(first, second, third))).ShouldBeOfType<HelloMessage>(context);
            hello.Control.ShouldBe(ControlKinds.Hello, context);
            (hello.MinVersion, hello.MaxVersion, hello.MaxPayloadLength).ShouldBe((first, second, third), context);
            HelloAckMessage ack = MessageSerializer.ReadControl(MessageSerializer.WriteControl(new HelloAckMessage(first, second, third))).ShouldBeOfType<HelloAckMessage>(context);
            ack.Control.ShouldBe(ControlKinds.HelloAck, context);
            (ack.Version, ack.MaxPayloadLength, ack.MaxConcurrentRequests).ShouldBe((first, second, third), context);
            HelloRefusedMessage refused = MessageSerializer.ReadControl(MessageSerializer.WriteControl(new HelloRefusedMessage(code))).ShouldBeOfType<HelloRefusedMessage>(context);
            refused.Control.ShouldBe(ControlKinds.HelloRefused, context);
            refused.Code.ShouldBe(code, context);
            CancelMessage cancel = MessageSerializer.ReadControl(MessageSerializer.WriteControl(new CancelMessage(correlationId))).ShouldBeOfType<CancelMessage>(context);
            cancel.Control.ShouldBe(ControlKinds.Cancel, context);
            cancel.CorrelationId.ShouldBe(correlationId, context);
            MessageSerializer.ReadControl(MessageSerializer.WriteControl(HeartbeatMessage.Ping)).ShouldBeOfType<HeartbeatMessage>(context).Control.ShouldBe(ControlKinds.Ping, context);
            MessageSerializer.ReadControl(MessageSerializer.WriteControl(HeartbeatMessage.Pong)).ShouldBeOfType<HeartbeatMessage>(context).Control.ShouldBe(ControlKinds.Pong, context);
        }
    }

    #endregion

    #region Mutations

    /// <summary>
    /// A control message cut at any offset is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> half a message is never a message: no proper prefix of a JSON object is a JSON document.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ATruncatedMessageIsRefusedAtEveryOffset()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid) in Samples())
        {
            int offset = 0;
            foreach (byte[] prefix in JsonFuzzKit.Truncations(valid))
            {
                JsonFuzzKit.AssertRefused(() => Read(prefix), $"{name}, cut at {offset} of {valid.Length}");
                offset++;
            }
        }
    }

    /// <summary>
    /// A control message whose structure is broken in one place is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the reader is strict: a property deleted, renamed, written in another case or twice, or given a value of the
    /// wrong kind (a number as a string, null…) is never tolerated; this includes the <c>control</c> property itself.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AMessageWithABrokenStructureIsRefused()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid) in Samples())
        {
            foreach ((string mutation, byte[] mutated) in JsonFuzzKit.StructuralMutations(valid))
                JsonFuzzKit.AssertRefused(() => Read(mutated), $"{name}, {mutation}: {Encoding.UTF8.GetString(mutated)}");
        }
    }

    /// <summary>
    /// An input that is not a JSON object holding a control message is refused
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> empty input, other JSON values, two messages in one payload, trailing text, a byte order mark, comments, a
    /// trailing comma, and a message whose <c>control</c> property is not first are not control messages.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void AnInputThatIsNotAControlMessageIsRefused()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid) in Samples())
        {
            foreach ((string document, byte[] bytes) in JsonFuzzKit.OddDocuments(valid))
                JsonFuzzKit.AssertRefused(() => Read(bytes), $"{name}, {document}");
        }

        // Check: the kind must come first
        JsonFuzzKit.AssertRefused(() => Read(Encoding.UTF8.GetBytes("""{"correlationId":1,"control":"cancel"}""")), "the kind is not the first property");
    }

    /// <summary>
    /// A JSON nested deeper than the maximum is refused, up to a depth that would overflow a recursive parser
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a hostile peer must not exhaust the stack of the process with brackets, even in its very first message.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ATooDeepMessageIsRefused()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // Running the cases
        foreach ((string name, byte[] valid) in Samples())
        {
            foreach ((string document, byte[] bytes) in JsonFuzzKit.TooDeepDocuments(valid))
                JsonFuzzKit.AssertRefused(() => Read(bytes), $"{name}, {document}");
        }

        // Check: nesting in the very first value
        foreach (int depth in new[] { 17, 100000 })
            JsonFuzzKit.AssertRefused(() => Read(Encoding.UTF8.GetBytes("{\"control\":" + new string('[', depth) + new string(']', depth) + "}")), $"{depth} nested arrays as the kind");
    }

    #endregion

    #region Peer text

    /// <summary>
    /// A refusal never contains a text of the input, whatever the place of that text in the message
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the protocol promises that the error messages reach the logs without any text of the peer, so that a peer cannot
    /// write fake log lines or control characters.</para>
    /// <para><b>Behavior:</b> a random marker (see <see cref="JsonFuzzKit.NextMarker"/>) is put in a property name or in a value of every
    /// kind of message; every refusal is searched for the marker, including its inner exception and its stack trace. A refusal of the
    /// code of a <c>helloRefused</c> is not the job of the reader (the handshake checks it), so that one may be accepted.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public void ARefusalNeverCopiesTheTextOfThePeer()
    {
        // Check: the test is still allowed to run (the timeout cancels this token)
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();

        // The messages, the marker standing for @M@
        string[] templates =
        [
            """{"control":@M@}""",
            """{"control":"ping",@M@:1}""",
            """{"control":"pong",@M@:@M@}""",
            """{@M@:"ping"}""",
            """{@M@:@M@}""",
            """{"control":"hello","minVersion":@M@,"maxVersion":1,"maxPayloadLength":1}""",
            """{"control":"hello","minVersion":1,"maxVersion":1,"maxPayloadLength":1,@M@:2}""",
            """{"control":"helloAck","version":1,"maxPayloadLength":1,"maxConcurrentRequests":@M@}""",
            """{"control":"helloAck",@M@:1}""",
            """{"control":"helloRefused","code":"a",@M@:1}""",
            """{"control":"cancel","correlationId":@M@}""",
            """{"control":"cancel","correlationId":1,@M@:1}"""
        ];

        // Running the cases
        for (int seed = 1; seed <= JsonFuzzKit.SeedCount; seed++)
        {
            Random random = new Random(seed);
            string marker = JsonFuzzKit.NextMarker(random);
            string quoted = JsonFuzzKit.Quote(marker);
            foreach (string template in templates)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(template.Replace("@M@", quoted, StringComparison.Ordinal));
                JsonFuzzKit.AssertNoPeerText(() => Read(bytes), marker, $"seed {seed}, message {template}", true);
            }

            // The code of a refusal, which the reader does not check
            byte[] refusal = Encoding.UTF8.GetBytes("{\"control\":\"helloRefused\",\"code\":" + quoted + "}");
            JsonFuzzKit.AssertNoPeerText(() => Read(refusal), marker, $"seed {seed}, refusal code", false);
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
