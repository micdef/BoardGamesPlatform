/*
 *	@file	src/BoardGamesPlatform.Protocol/Messages/StrictDateTimeOffsetConverter.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Reads and writes the dates of the messages as ISO 8601 text that always carries its offset
 */

// Using
using System.Globalization;
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
/// JSON converter used for every <see cref="DateTimeOffset"/> of the messages
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the default reader accepts a date without offset and gives it the time zone of the machine that reads it, so the same
/// message would mean another instant on another server. A date of the platform must say which instant it is.</para>
/// <para><b>Behavior:</b> writes the round-trip format (<c>2026-10-30T09:15:00.1234567+01:00</c>). Reads only a JSON string with a date, a
/// time and an explicit offset (<c>Z</c> or <c>±hh:mm</c>); anything else is refused with a <see cref="JsonException"/>, which the serializer
/// turns into a protocol violation.</para>
/// </remarks>
internal sealed partial class StrictDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    #region Members

    #region Constants

    /// <summary>
    /// Formats accepted when reading: date and time to the second, optional fraction up to seven digits, then the offset
    /// </summary>
    private const string ReadFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

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
    /// Method used to get the pattern that checks that a date text ends with an explicit offset
    /// </summary>
    /// <returns>
    /// The compiled pattern: "Z", or a sign followed by hours and minutes, at the end of the text
    /// </returns>
    [GeneratedRegex(@"(Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitOffsetPattern();

    #endregion

    #region Publics

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <summary>
    /// Method used to read a date that carries its offset
    /// </summary>
    /// <param name="reader">Reader positioned on the value</param>
    /// <param name="typeToConvert">[Cannot be null] Type read (<see cref="DateTimeOffset"/>)</param>
    /// <param name="options">[Cannot be null] Options of the serializer</param>
    /// <returns>
    /// The date read
    /// </returns>
    /// <exception cref="JsonException">If the value is not a string, or not a date with a time and an explicit offset</exception>
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Check
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("A date must be a JSON string");

        // Reading the text: an explicit offset first, then the exact format
        string text = reader.GetString() ?? string.Empty;
        if (!ExplicitOffsetPattern().IsMatch(text)
            || !DateTimeOffset.TryParseExact(text, ReadFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value))
        {
            throw new JsonException("A date must be written as ISO 8601 with its time and an explicit offset, for example 2026-10-30T09:15:00+01:00");
        }

        // Function's return
        return value;
    }

    /// <summary>
    /// Method used to write a date in the round-trip format, with its offset
    /// </summary>
    /// <param name="writer">[Cannot be null] Writer of the message</param>
    /// <param name="value">Date to write</param>
    /// <param name="options">[Cannot be null] Options of the serializer</param>
    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        // Writing the round-trip format
        writer.WriteStringValue(value.ToString("O", CultureInfo.InvariantCulture));
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
