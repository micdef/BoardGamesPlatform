/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Messages/InvalidUtf8Converter.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	JSON converter that writes a string of invalid UTF-8 without validation, used by the tests of the message writer
 */

// Using
using System.Text.Json;
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Messages;

/// <summary>
/// Converter that writes any text as a JSON string holding the invalid UTF-8 bytes C3 28, skipping the validation of the writer
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a converter of a body may write raw JSON without validation (<see cref="Utf8JsonWriter.WriteRawValue(ReadOnlySpan{byte}, bool)"/>);
/// the writer of the messages must still refuse what the reader of the peer refuses, invalid UTF-8 included.</para>
/// </remarks>
public sealed class InvalidUtf8Converter : JsonConverter<string>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// A JSON string whose content is the invalid UTF-8 sequence C3 28: a lead byte followed by a byte that cannot continue it
    /// </summary>
    private static readonly byte[] _invalidString = new byte[] { (byte)'"', 0xC3, 0x28, (byte)'"' };

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

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <inheritdoc/>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Function's return: the tests only write with this converter
        throw new NotSupportedException("This converter only writes");
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        // Writing the invalid string as it is, the validation of the writer skipped
        writer.WriteRawValue(_invalidString, skipInputValidation: true);
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
