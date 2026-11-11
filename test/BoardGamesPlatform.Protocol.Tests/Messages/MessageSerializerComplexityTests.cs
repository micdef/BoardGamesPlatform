/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/MessageSerializerComplexityTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the memory a request costs to write and to read: in proportion to the request, and with no second copy of its body
 */

// Using
using System.Text;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Tests used to check the space complexity of the envelopes of the requests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> every call between two tiers writes a request on one end and reads it on the other: a body written twice, or
/// copied once more only to be checked, doubles the memory of every call.</para>
/// <para><b>Behavior:</b> a request is measured with a short and a long body (<see cref="AllocationMeter"/>); the difference, divided by
/// the difference of the sizes, is the memory paid per byte of request. Writing must allocate the request once, reading must keep the body
/// once: at most <see cref="MaxBytesPerByte"/> per byte, where writing twice or copying the body as text costs 2 or more.</para>
/// </remarks>
public sealed class MessageSerializerComplexityTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Characters of the text of the short body
    /// </summary>
    private const int ShortText = 16;

    /// <summary>
    /// Characters of the text of the long body
    /// </summary>
    private const int LongText = 16 * 1024;

    /// <summary>
    /// Bytes allocated per byte of request, at most: one copy, and a margin for the growth of the buffers below one copy
    /// </summary>
    private const double MaxBytesPerByte = 1.5;

    /// <summary>
    /// Bytes allocated per byte of response read, at most: the text of the body is a .NET string, of 2 bytes per character, which is the
    /// result itself, and a margin
    /// </summary>
    private const double MaxBytesPerByteOfTextRead = 2.5;

    /// <summary>
    /// Operation of the requests
    /// </summary>
    private const string Operation = "test.text";

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
    /// Method used to build a body whose text has the given number of characters, all ASCII, so that one character is one byte
    /// </summary>
    /// <param name="length">Characters of the text</param>
    /// <returns>
    /// The body
    /// </returns>
    private static TextBody Body(int length)
    {
        // Function's return
        return new TextBody(new string('a', length));
    }

    /// <summary>
    /// Method used to measure the bytes allocated by a synchronous operation of the serializer
    /// </summary>
    /// <param name="operation">[Cannot be null] Operation, whose result is dropped</param>
    /// <returns>
    /// The bytes allocated by one run
    /// </returns>
    private static long Measure(Func<object> operation)
    {
        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            _ = operation();

            // Function's return
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the write of one request
    /// </summary>
    /// <param name="body">[Cannot be null] Body of the request</param>
    /// <returns>
    /// The bytes allocated by one write
    /// </returns>
    private static long MeasureWrite(TextBody body)
    {
        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            _ = MessageSerializer.WriteRequest(Operation, body);

            // Function's return
            return ValueTask.CompletedTask;
        });
    }

    /// <summary>
    /// Method used to measure the bytes allocated by the read of one request
    /// </summary>
    /// <param name="request">[Cannot be null] Bytes of the request</param>
    /// <returns>
    /// The bytes allocated by one read
    /// </returns>
    private static long MeasureRead(byte[] request)
    {
        // Function's return
        return AllocationMeter.PerRun(() =>
        {
            _ = MessageSerializer.ReadRequest(request);

            // Function's return
            return ValueTask.CompletedTask;
        });
    }

    #endregion

    #region Publics

    /// <summary>
    /// A request is written once: its memory grows by about one byte per byte of request, never by a body written twice (space O(n))
    /// </summary>
    [Fact]
    public void ARequestIsWrittenInOnePass()
    {
        // Arranging
        TextBody shortBody = Body(ShortText);
        TextBody longBody = Body(LongText);
        int growth = MessageSerializer.WriteRequest(Operation, longBody).Length - MessageSerializer.WriteRequest(Operation, shortBody).Length;

        // Acting
        long allocated = MeasureWrite(longBody) - MeasureWrite(shortBody);

        // Check
        ((double)allocated / growth).ShouldBeLessThan(MaxBytesPerByte);
    }

    /// <summary>
    /// A request is read without copying its body again to check it: its memory grows by about one byte per byte of request (space O(n))
    /// </summary>
    [Fact]
    public void ARequestIsReadWithoutASecondCopyOfItsBody()
    {
        // Arranging
        byte[] shortRequest = MessageSerializer.WriteRequest(Operation, Body(ShortText));
        byte[] longRequest = MessageSerializer.WriteRequest(Operation, Body(LongText));
        int growth = longRequest.Length - shortRequest.Length;

        // Acting
        long allocated = MeasureRead(longRequest) - MeasureRead(shortRequest);

        // Check
        ((double)allocated / growth).ShouldBeLessThan(MaxBytesPerByte);
    }

    /// <summary>
    /// The request written in one pass holds the same bytes as before: the operation, then the body as a JSON object
    /// </summary>
    [Fact]
    public void ARequestWrittenInOnePassKeepsItsForm()
    {
        // Acting
        string json = Encoding.UTF8.GetString(MessageSerializer.WriteRequest(Operation, new TextBody("hi")));

        // Check
        json.ShouldBe("""{"operation":"test.text","body":{"text":"hi"}}""");
    }

    /// <summary>
    /// A request written in one pass holds the same bytes as one written through a JSON element first, whatever its text: accents, an emoji,
    /// characters the JSON writer escapes
    /// </summary>
    [Fact]
    public void BothWaysOfWritingARequestGiveTheSameBytes()
    {
        // Arranging the same body as an object (written in one pass) and as a dictionary (written through a JSON element)
        const string Text = "Çà et là, <b>\"gras\"</b> + 1,10 € ☃ \U0001F600";
        TextBody asObject = new TextBody(Text);
        Dictionary<string, string> asDictionary = new Dictionary<string, string> { ["text"] = Text };

        // Acting
        byte[] onePass = MessageSerializer.WriteRequest(Operation, asObject);
        byte[] throughElement = MessageSerializer.WriteRequest(Operation, asDictionary);

        // Check
        onePass.ShouldBe(throughElement);
    }

    /// <summary>
    /// A response is written once: its memory grows by about one byte per byte of response (space O(n))
    /// </summary>
    [Fact]
    public void AResponseIsWrittenInOnePass()
    {
        // Arranging
        TextBody shortBody = Body(ShortText);
        TextBody longBody = Body(LongText);
        int growth = MessageSerializer.WriteResponse(longBody).Length - MessageSerializer.WriteResponse(shortBody).Length;

        // Acting
        long allocated = Measure(() => MessageSerializer.WriteResponse(longBody)) - Measure(() => MessageSerializer.WriteResponse(shortBody));

        // Check
        ((double)allocated / growth).ShouldBeLessThan(MaxBytesPerByte);
    }

    /// <summary>
    /// A response is read into its body without any other copy: its memory grows by the text of the body, a .NET string of 2 bytes per
    /// character, and no more (space O(n))
    /// </summary>
    [Fact]
    public void AResponseIsReadIntoItsBodyOnly()
    {
        // Arranging
        byte[] shortResponse = MessageSerializer.WriteResponse(Body(ShortText));
        byte[] longResponse = MessageSerializer.WriteResponse(Body(LongText));
        int growth = longResponse.Length - shortResponse.Length;

        // Acting
        long allocated = Measure(() => MessageSerializer.ReadResponse<TextBody>(longResponse)) - Measure(() => MessageSerializer.ReadResponse<TextBody>(shortResponse));

        // Check
        ((double)allocated / growth).ShouldBeLessThan(MaxBytesPerByteOfTextRead);
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
