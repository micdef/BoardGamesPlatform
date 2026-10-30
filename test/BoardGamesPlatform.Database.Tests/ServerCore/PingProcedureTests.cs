/*
 *	@file	test/BoardGamesPlatform.Database.Tests/ServerCore/PingProcedureTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the procedure ServerCore.SP_ServerCore_Server_Ping on a throwaway database
 */

// Using
using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.ServerCore;

/// <summary>
/// Tests used to check the Ping procedure, end of the walking skeleton of version 0.3.0
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the procedure answers and that its JSON result is what the Very Back reads: a date with an explicit
/// offset ("+hh:mm", "-hh:mm" or "Z").</para>
/// <para><b>Behavior:</b> skipped when no throwaway database is configured (see <see cref="TestDatabase"/>). The checks compare with the clock
/// and the offset of the SQL Server itself, read on the same connection, never with the clock of the test machine.</para>
/// </remarks>
public sealed class PingProcedureTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Pattern of an ISO 8601 date with its time, 0 to 7 fraction digits and an explicit offset, as FOR JSON writes it
    /// </summary>
    private const string ExplicitOffsetDatePattern = @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d{1,7})?(Z|[+-]\d{2}:\d{2})$";

    #endregion

    #region Variables

    /// <summary>
    /// The only property of the JSON result of the procedure
    /// </summary>
    private static readonly string[] _expectedProperties = new string[] { "ServerTime" };

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
    /// Method used to run a query that returns one value
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Query to run</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The first column of the first row
    /// </returns>
    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the query
        await using SqlCommand command = new SqlCommand(sql, connection);

        // Function's return
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// The procedure returns one JSON object holding the current time of the server, with the offset of the server
    /// </summary>
    [Fact]
    public async Task PingReturnsTheServerTimeWithItsOffsetAsync()
    {
        // Calling the procedure, schema-qualified, between two readings of the UTC clock of the server
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        DateTime before = (DateTime)(await ScalarAsync(connection, "SELECT SYSUTCDATETIME();", token))!;
        await using SqlCommand command = new SqlCommand("[ServerCore].[SP_ServerCore_Server_Ping]", connection) { CommandType = CommandType.StoredProcedure };
        SqlParameter result = command.Parameters.Add("@Result", SqlDbType.NVarChar, -1);
        result.Direction = ParameterDirection.Output;
        await command.ExecuteNonQueryAsync(token);
        DateTime after = (DateTime)(await ScalarAsync(connection, "SELECT SYSUTCDATETIME();", token))!;
        int serverOffsetMinutes = (int)(await ScalarAsync(connection, "SELECT DATEPART(TZOFFSET, SYSDATETIMEOFFSET());", token))!;

        // Reading the JSON: one object, one property, a date that carries an explicit offset
        string json = result.Value.ShouldBeOfType<string>();
        using JsonDocument document = JsonDocument.Parse(json);
        document.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        document.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(_expectedProperties);
        string serverTimeText = document.RootElement.GetProperty("ServerTime").GetString().ShouldNotBeNull();
        serverTimeText.ShouldMatch(ExplicitOffsetDatePattern);
        DateTimeOffset serverTime = DateTimeOffset.Parse(serverTimeText, CultureInfo.InvariantCulture);

        // Checks: the offset is the one of the server, and the instant falls between the two readings of its clock
        serverTime.Offset.ShouldBe(TimeSpan.FromMinutes(serverOffsetMinutes));
        serverTime.UtcDateTime.ShouldBeInRange(DateTime.SpecifyKind(before, DateTimeKind.Utc), DateTime.SpecifyKind(after, DateTimeKind.Utc));
    }

    /// <summary>
    /// A server on UTC writes its time with the offset "Z", and a zero fraction without digits: both still carry an explicit offset
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> most servers (Linux hosts, containers, Azure SQL) run on UTC; the form FOR JSON gives their time must stay readable
    /// by the Very Back, whatever the time zone of the machine that runs this test.</para>
    /// <para><b>Behavior:</b> writes fixed dates with the same FOR JSON PATH as the procedure, so that the result does not depend on any clock.</para>
    /// </remarks>
    [Fact]
    public async Task TheServerTimeFormatKeepsAnExplicitOffsetOnUtcAsync()
    {
        // Writing a UTC date with a fraction, and a date with a zero fraction, as the procedure does
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string utc = (string)(await ScalarAsync(connection,
            "SELECT (SELECT CAST('2026-10-30T08:15:00.1234567+00:00' AS DATETIMEOFFSET(7)) AS [ServerTime] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);", token))!;
        string zeroFraction = (string)(await ScalarAsync(connection,
            "SELECT (SELECT CAST('2026-10-30T09:15:00+01:00' AS DATETIMEOFFSET(7)) AS [ServerTime] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);", token))!;

        // Checks
        utc.ShouldBe("{\"ServerTime\":\"2026-10-30T08:15:00.1234567Z\"}");
        zeroFraction.ShouldBe("{\"ServerTime\":\"2026-10-30T09:15:00+01:00\"}");
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
