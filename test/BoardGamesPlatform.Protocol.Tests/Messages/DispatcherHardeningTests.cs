/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/DispatcherHardeningTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that no failure of one request can close the connection every caller shares, and that a peer cannot write into the logs
 */

// Using
using System.Text;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Tests used to check the hardening of the operation dispatcher and of the serializer, after the review of 0.4.0
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a connection is shared by every caller of a tier; a response too large, a body that cannot be written or a bad
/// request built by a caller must give one failed request, never a closed connection. And the text of a peer must never reach a log.</para>
/// </remarks>
public sealed class DispatcherHardeningTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Body written as a JSON array, which a request may not carry
    /// </summary>
    private static readonly string[] _notAnObject = new string[] { "not", "an", "object" };

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
    /// Method used to run one request through a dispatcher and return the response as text
    /// </summary>
    /// <param name="dispatcher">[Cannot be null] Dispatcher</param>
    /// <param name="json">[Cannot be null] JSON text of the request</param>
    /// <param name="cancellationToken">Token of the request</param>
    /// <returns>
    /// The JSON text of the response
    /// </returns>
    private static async Task<string> DispatchAsync(OperationDispatcher dispatcher, string json, CancellationToken cancellationToken)
    {
        // Running the request
        ReadOnlyMemory<byte> response = await dispatcher.HandleAsync(Encoding.UTF8.GetBytes(json), cancellationToken);

        // Function's return
        return Encoding.UTF8.GetString(response.Span);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A response larger than the link can carry is answered "internal" and logged, instead of closing the connection
    /// </summary>
    [Fact]
    public async Task AResponseTooLargeForTheLinkIsAnsweredInternalAsync()
    {
        // Building two dispatchers with the same handler: one for a link that carries its response (20 bytes), one for a link of 19 bytes
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<OperationDispatcher> logger = new CapturingLogger<OperationDispatcher>();
        OperationDispatcher fits = new OperationDispatcher(NullLogger.Instance, maxResponseLength: 20)
            .Register<NumberBody, NumberBody>("Any", (request, cancellationToken) => Task.FromResult(new NumberBody(1)));
        OperationDispatcher tooSmall = new OperationDispatcher(logger, maxResponseLength: 19)
            .Register<NumberBody, NumberBody>("Any", (request, cancellationToken) => Task.FromResult(new NumberBody(1)));

        // Checks: the response that fits passes, the other becomes "internal", the log names the sizes, and the limit must be positive
        (await DispatchAsync(fits, "{\"operation\":\"Any\",\"body\":{\"value\":1}}", token)).ShouldBe("{\"body\":{\"value\":1}}");
        (await DispatchAsync(tooSmall, "{\"operation\":\"Any\",\"body\":{\"value\":1}}", token)).ShouldBe("{\"error\":{\"code\":\"internal\"}}");
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error
            && entry.Message == "The response of Any is 20 bytes, more than the 19 bytes of the link; answered internal");
        Should.Throw<ArgumentOutOfRangeException>(() => new OperationDispatcher(NullLogger.Instance, maxResponseLength: 0));
    }

    /// <summary>
    /// A response body that cannot be written is answered "internal", like any failure of the handler
    /// </summary>
    [Fact]
    public async Task AResponseThatCannotBeWrittenIsAnsweredInternalAsync()
    {
        // Building a dispatcher whose response fails when it is written
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance)
            .Register<NumberBody, ThrowingBody>("Fail", (request, cancellationToken) => Task.FromResult(new ThrowingBody()));

        // Check
        (await DispatchAsync(dispatcher, "{\"operation\":\"Fail\",\"body\":{\"value\":1}}", TestContext.Current.CancellationToken))
            .ShouldBe("{\"error\":{\"code\":\"internal\"}}");
    }

    /// <summary>
    /// A code answered by a tier behind is not passed on, except "busy": the others become "internal", logged as a warning
    /// </summary>
    [Fact]
    public async Task ACodeOfATierBehindIsNotPassedOnExceptBusyAsync()
    {
        // Building a dispatcher whose handlers were answered a code by the tier behind them
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<OperationDispatcher> logger = new CapturingLogger<OperationDispatcher>();
        OperationDispatcher dispatcher = new OperationDispatcher(logger)
            .Register<NumberBody, NumberBody>("Busy", (request, cancellationToken) => throw new RemoteErrorException(ErrorCodes.ServerBusy))
            .Register<NumberBody, NumberBody>("Unknown", (request, cancellationToken) => throw new RemoteErrorException(ErrorCodes.RequestUnknownOperation));

        // Checks
        (await DispatchAsync(dispatcher, "{\"operation\":\"Busy\",\"body\":{\"value\":1}}", token)).ShouldBe("{\"error\":{\"code\":\"server.busy\"}}");
        (await DispatchAsync(dispatcher, "{\"operation\":\"Unknown\",\"body\":{\"value\":1}}", token)).ShouldBe("{\"error\":{\"code\":\"internal\"}}");
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning
            && entry.Message == "The handler of Unknown was answered request.unknownOperation by a tier behind; answered internal");
    }

    /// <summary>
    /// A handler that fails with another exception once its request is canceled (as a client library may do) is not answered either
    /// </summary>
    [Fact]
    public async Task AFailureAfterTheCancellationIsNotAnsweredAsync()
    {
        // Building a dispatcher whose handler reports the cancellation as an error of its own
        using CancellationTokenSource request = new CancellationTokenSource();
        await request.CancelAsync();
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance)
            .Register<NumberBody, NumberBody>("Query", (body, cancellationToken) => throw new InvalidOperationException("Operation cancelled by user"));

        // Check
        await Should.ThrowAsync<OperationCanceledException>(
            () => dispatcher.HandleAsync(Encoding.UTF8.GetBytes("{\"operation\":\"Query\",\"body\":{\"value\":1}}"), request.Token));
    }

    /// <summary>
    /// A request that the reader would refuse is refused before it is written, as a bug of the caller: a bad name, a body that is not an object
    /// </summary>
    /// <param name="operation">Operation name to write</param>
    [Theory]
    [InlineData("Bad Name")]
    [InlineData("A\nB")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void ARequestTheReaderWouldRefuseIsNeverWritten(string operation)
    {
        // Checks
        Should.Throw<ArgumentException>(() => MessageSerializer.WriteRequest(operation, new NumberBody(1)));
        Should.Throw<ArgumentException>(() => MessageSerializer.WriteRequest("Valid", _notAnObject));
        Encoding.UTF8.GetString(MessageSerializer.WriteRequest("Valid", new NumberBody(1))).ShouldBe("{\"operation\":\"Valid\",\"body\":{\"value\":1}}");
    }

    /// <summary>
    /// The error of a malformed message never copies the text of the peer, so that a peer cannot write lines or control characters into the
    /// logs, and keeps no inner exception that would copy it
    /// </summary>
    [Fact]
    public void AMalformedMessageNeverCopiesTheTextOfThePeer()
    {
        // Reading a body and an envelope that hold a forged log line in a property name
        string forged = "evil\\r\\n2026-11-02 FAKE LOG LINE\\u001b[31m";
        InvalidDataException body = Should.Throw<InvalidDataException>(() => MessageSerializer.ReadResponse<NumberBody>(
            Encoding.UTF8.GetBytes("{\"body\":{\"value\":1,\"" + forged + "\":true}}")));
        InvalidDataException control = Should.Throw<InvalidDataException>(() => MessageSerializer.ReadControl(
            Encoding.UTF8.GetBytes("{\"control\":\"cancel\",\"correlationId\":1,\"" + forged + "\":true}")));

        // Checks
        foreach (InvalidDataException refusal in new InvalidDataException[] { body, control })
        {
            refusal.Message.ShouldNotContain("evil");
            refusal.Message.ShouldNotContain("FAKE");
            refusal.InnerException.ShouldBeNull();
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
