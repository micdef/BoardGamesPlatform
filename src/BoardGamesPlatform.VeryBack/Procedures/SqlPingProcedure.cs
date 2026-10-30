/*
 *	@file	src/BoardGamesPlatform.VeryBack/Procedures/SqlPingProcedure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Calls the Ping stored procedure on SQL Server and reads its JSON result
 */

// Using
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Procedures;

/// <summary>
/// Call of [ServerCore].[SP_ServerCore_Server_Ping] on SQL Server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the end of the walking skeleton of 0.3.0: proves that the Very Back reaches the database with its own SQL user, through a
/// stored procedure only.</para>
/// <para><b>Behavior:</b> opens a connection with the main connection string, runs the procedure (schema-qualified) and reads its output
/// parameter, a JSON object <c>{"ServerTime":"…"}</c>. The result is read strictly by <see cref="ParseServerTime"/>: Contract as a Service, the Very
/// Back checks what the database returns instead of trusting it. The connection string is never logged and never put in an exception message:
/// it is checked at construction, so that the parsing error of the SQL client, which quotes the unknown keyword, can never be raised by a
/// call. The errors of SQL Server itself may still name the server, the database or the login.</para>
/// </remarks>
public sealed partial class SqlPingProcedure : IPingProcedure
{
    #region Members

    #region Constants

    /// <summary>
    /// Schema-qualified name of the stored procedure
    /// </summary>
    private const string ProcedureName = "[ServerCore].[SP_ServerCore_Server_Ping]";

    /// <summary>
    /// Name of the output parameter of the procedure
    /// </summary>
    private const string ResultParameterName = "@Result";

    /// <summary>
    /// Name of the only property of the result
    /// </summary>
    private const string ServerTimeProperty = "ServerTime";

    /// <summary>
    /// Format used to read the server time once its shape is checked: to the second, an optional fraction, then the offset (the shape check
    /// refuses a dot without digits, which this format alone would accept)
    /// </summary>
    private const string ServerTimeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.FFFFFFFK";

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

    /// <summary>
    /// Connection string of the main database; a secret, never logged
    /// </summary>
    private readonly string _connectionString;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="options">[Cannot be null] Connection strings of the Very Back, already validated at startup</param>
    /// <exception cref="ArgumentNullException">If the options are null</exception>
    /// <exception cref="InvalidOperationException">If the main connection string is missing, or the SQL client cannot read it</exception>
    public SqlPingProcedure(IOptions<ConnectionStringsOptions> options)
    {
        // Check
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");

        // Reading the connection string (a missing or unreadable one is a configuration error, named without its value nor the error of the
        // SQL client, which can quote a piece of it)
        string? connectionString = options.Value.Main;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is missing");
        if (!ConnectionStringsOptionsValidator.IsWellFormed(connectionString))
            throw new InvalidOperationException($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is not a connection string the SQL client can read");

        // Setting
        _connectionString = connectionString;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to get the pattern that checks the whole shape of a date text
    /// </summary>
    /// <returns>
    /// The compiled pattern: date, "T", time to the second, an optional dot followed by one to seven digits, then "Z" or a sign followed by
    /// hours and minutes; ASCII digits only, nothing before or after
    /// </returns>
    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]{1,7})?(Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex ServerTimePattern();

    #endregion

    #region Publics

    /// <summary>
    /// Method used to read the JSON result of the Ping procedure
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> accepts exactly one JSON object holding exactly one property, <c>ServerTime</c>, a string with a date, a time
    /// and an explicit offset (<c>Z</c> or <c>±hh:mm</c>; no fraction, or a dot followed by 1 to 7 digits). Anything else is refused.</para>
    /// </remarks>
    /// <param name="json">Output parameter of the procedure; null when the database returned NULL</param>
    /// <returns>
    /// The server time, with its offset
    /// </returns>
    /// <exception cref="InvalidDataException">If the result is not the documented JSON object</exception>
    public static DateTimeOffset ParseServerTime(string? json)
    {
        // Check
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidDataException("The Ping procedure returned no result");

        // Reading the JSON, a syntax error being a refusal like any other
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            // Exactly one object with exactly one property (a duplicated property makes two)
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 1)
                throw new InvalidDataException($"The result of the Ping procedure must be one object with the only property {ServerTimeProperty}");
            if (!root.TryGetProperty(ServerTimeProperty, out JsonElement serverTime) || serverTime.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"The property {ServerTimeProperty} of the Ping result must be a string");

            // A date with its time and an explicit offset (an offset-less date would mean another instant on another machine): the pattern
            // checks the shape, the parsing checks the ranges (month, hour, offset)
            string text = serverTime.GetString() ?? string.Empty;
            if (!ServerTimePattern().IsMatch(text)
                || !DateTimeOffset.TryParseExact(text, ServerTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset value))
            {
                throw new InvalidDataException($"The property {ServerTimeProperty} of the Ping result must be an ISO 8601 date with its time and an explicit offset");
            }

            // Function's return
            return value;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The result of the Ping procedure is not valid JSON", exception);
        }
    }

    /// <summary>
    /// Method used to get the current date and time of SQL Server (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token used to stop waiting for the database</param>
    /// <returns>
    /// The date and time of the server, with its offset
    /// </returns>
    /// <exception cref="SqlException">If the connection or the procedure fails</exception>
    /// <exception cref="InvalidDataException">If the database answers something else than the documented result</exception>
    /// <exception cref="OperationCanceledException">If the call is canceled</exception>
    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        // Opening the connection and calling the procedure
        await using SqlConnection connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = new SqlCommand(ProcedureName, connection) { CommandType = CommandType.StoredProcedure };
        SqlParameter result = new SqlParameter(ResultParameterName, SqlDbType.NVarChar, -1) { Direction = ParameterDirection.Output };
        command.Parameters.Add(result);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Function's return
        return ParseServerTime(result.Value as string);
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
