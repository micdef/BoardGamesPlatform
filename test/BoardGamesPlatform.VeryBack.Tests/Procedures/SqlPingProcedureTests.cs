/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Procedures/SqlPingProcedureTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the reading of the Ping procedure result, and its call on a real database when one is configured
 */

// Using
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Procedures;

/// <summary>
/// Tests used to check <see cref="SqlPingProcedure"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Very Back must read the result of the database strictly: exactly the documented JSON, with an explicit offset.</para>
/// <para><b>Behavior:</b> the parsing tests need no database. The call test runs on the database named by the environment variable
/// <see cref="TestDatabaseVariable"/> (the same as the SQL tests), and is reported as skipped without one.</para>
/// </remarks>
public sealed class SqlPingProcedureTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Environment variable holding the connection string of the throwaway database, shared with the SQL tests
    /// </summary>
    private const string TestDatabaseVariable = "BOARDGAMESPLATFORM_TEST_DATABASE";

    /// <summary>
    /// Longest duration of the database test, in milliseconds
    /// </summary>
    private const int TestTimeout = 30000;

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

    // #N/A

    #endregion

    #region Publics

    #region Parsing

    /// <summary>
    /// A date with a positive offset, a fraction and a time is read with its offset
    /// </summary>
    [Fact]
    public void ADateWithAPositiveOffsetIsRead()
    {
        // Check
        DateTimeOffset serverTime = SqlPingProcedure.ParseServerTime("{\"ServerTime\":\"2026-10-30T09:15:00.1234567+01:00\"}");
        serverTime.Offset.ShouldBe(TimeSpan.FromHours(1));
        serverTime.UtcDateTime.ShouldBe(new DateTime(2026, 10, 30, 8, 15, 0, DateTimeKind.Utc).AddTicks(1234567));
    }

    /// <summary>
    /// A date written with Z (the server runs on UTC) is read with a zero offset
    /// </summary>
    [Fact]
    public void ADateWithZIsReadAsUtc()
    {
        // Check
        DateTimeOffset serverTime = SqlPingProcedure.ParseServerTime("{\"ServerTime\":\"2026-10-30T08:15:00.12Z\"}");
        serverTime.Offset.ShouldBe(TimeSpan.Zero);
        serverTime.UtcDateTime.ShouldBe(new DateTime(2026, 10, 30, 8, 15, 0, DateTimeKind.Utc).AddTicks(1200000));
    }

    /// <summary>
    /// A date without fraction (FOR JSON leaves out a zero fraction) and with a negative offset is read
    /// </summary>
    [Fact]
    public void ADateWithoutFractionAndANegativeOffsetIsRead()
    {
        // Check
        DateTimeOffset serverTime = SqlPingProcedure.ParseServerTime("{\"ServerTime\":\"2026-10-30T09:15:00-05:00\"}");
        serverTime.Offset.ShouldBe(TimeSpan.FromHours(-5));
        serverTime.UtcDateTime.ShouldBe(new DateTime(2026, 10, 30, 14, 15, 0, DateTimeKind.Utc));
    }

    /// <summary>
    /// Anything else than the documented result is refused with an InvalidDataException
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[{\"ServerTime\":\"2026-10-30T09:15:00Z\"}]")]
    [InlineData("not JSON")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00Z\"} trailing")]
    [InlineData("{}")]
    [InlineData("{\"Other\":\"2026-10-30T09:15:00Z\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00Z\",\"Extra\":1}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00Z\",\"ServerTime\":\"2026-10-30T09:15:00Z\"}")]
    [InlineData("{\"ServerTime\":null}")]
    [InlineData("{\"ServerTime\":1}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00+0100\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00.12345678Z\"}")]
    [InlineData("{\"ServerTime\":\"yesterday\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00.Z\"}")]
    [InlineData("{\"ServerTime\":\"2026-10-30T09:15:00.+01:00\"}")]
    [InlineData("{\"ServerTime\":\"٢026-10-30T09:15:00Z\"}")]
    [InlineData("{\"ServerTime\":\" 2026-10-30T09:15:00Z\"}")]
    public void AnythingElseIsRefused(string? json)
    {
        // Check
        Should.Throw<InvalidDataException>(() => SqlPingProcedure.ParseServerTime(json));
    }

    #endregion

    #region Constructor

    /// <summary>
    /// A missing connection string is refused when the procedure is created, without repeating any value
    /// </summary>
    [Fact]
    public void AMissingConnectionStringIsRefused()
    {
        // Check
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new SqlPingProcedure(Options.Create(new ConnectionStringsOptions())));
        exception.Message.ShouldContain("ConnectionStrings:Main");
    }

    /// <summary>
    /// A connection string the SQL client cannot read is refused when the procedure is created, without the error of the SQL client, which
    /// quotes a piece of the string
    /// </summary>
    [Fact]
    public void AnUnreadableConnectionStringIsRefusedWithoutRepeatingIt()
    {
        // Check
        ConnectionStringsOptions options = new ConnectionStringsOptions { Main = "Server=localhost;Password=ab;Kx9secret=cd" };
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(() => new SqlPingProcedure(Options.Create(options)));
        exception.Message.ShouldContain("ConnectionStrings:Main");
        exception.Message.ShouldNotContain("Kx9secret", Case.Insensitive);
        exception.InnerException.ShouldBeNull();
    }

    #endregion

    #region Database

    /// <summary>
    /// The real procedure answers the time of the server (skipped without a test database)
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheRealProcedureAnswersTheServerTimeAsync()
    {
        // Skipping the test when no database is configured
        string? connectionString = Environment.GetEnvironmentVariable(TestDatabaseVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString),
            $"No SQL test database: set {TestDatabaseVariable} after deploying one with tools/database/Deploy-TestDatabase.ps1");

        // Calling the procedure
        SqlPingProcedure procedure = new SqlPingProcedure(Options.Create(new ConnectionStringsOptions { Main = connectionString }));
        DateTimeOffset serverTime = await procedure.GetServerTimeAsync(TestContext.Current.CancellationToken);

        // Check: the clock of the server is that of this machine, within a day
        (serverTime - DateTimeOffset.UtcNow).Duration().ShouldBeLessThan(TimeSpan.FromDays(1));
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
