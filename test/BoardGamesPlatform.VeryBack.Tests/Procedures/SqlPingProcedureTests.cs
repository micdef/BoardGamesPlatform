/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Procedures/SqlPingProcedureTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the reading of the Ping procedure result, and its call on a real database when one is configured
 */

// Using
using BoardGamesPlatform.TestKit;
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Data.SqlClient;
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
/// <see cref="TestDatabase.ConnectionStringVariable"/> (the same as the SQL tests, with the same guard: only a TEST_ or DEV_ database), and is
/// reported as skipped without one.</para>
/// </remarks>
public sealed class SqlPingProcedureTests
{
    #region Members

    #region Constants

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

    #region Application name

    /// <summary>
    /// A connection string without Application Name gets the name of the Very Back, and keeps its other keywords
    /// </summary>
    /// <param name="connectionString">Connection string without Application Name</param>
    [Theory]
    [InlineData("Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=true")]
    [InlineData("Server=localhost;Max Pool Size=7;Pooling=true")]
    [InlineData("Server=localhost;Application Name=")]
    [InlineData("Server=localhost;App=")]
    public void TheDefaultApplicationNameIsAddedWhenAbsent(string connectionString)
    {
        // Check
        SqlConnectionStringBuilder before = new SqlConnectionStringBuilder(connectionString);
        SqlConnectionStringBuilder after = new SqlConnectionStringBuilder(SqlPingProcedure.WithApplicationName(connectionString));
        after.ApplicationName.ShouldBe(SqlPingProcedure.DefaultApplicationName);
        after.DataSource.ShouldBe(before.DataSource);
        after.MaxPoolSize.ShouldBe(before.MaxPoolSize);
        after.Pooling.ShouldBe(before.Pooling);
    }

    /// <summary>
    /// An Application Name set by the administrator, under either of its two keywords (in any case) and whatever its value, is kept untouched
    /// </summary>
    /// <param name="connectionString">Connection string that sets the application name</param>
    /// <param name="expected">Application name the administrator set</param>
    [Theory]
    [InlineData("Server=localhost;Application Name=Admin.Choice", "Admin.Choice")]
    [InlineData("Server=localhost;App=Admin.Choice", "Admin.Choice")]
    [InlineData("Server=localhost;APP=Admin.Choice", "Admin.Choice")]
    [InlineData("Server=localhost;application name=x", "x")]
    [InlineData("Server=localhost;Application Name=.Net SqlClient Data Provider", ".Net SqlClient Data Provider")]
    public void AnApplicationNameSetByTheAdministratorIsKept(string connectionString, string expected)
    {
        // Check
        string result = SqlPingProcedure.WithApplicationName(connectionString);
        result.ShouldBe(connectionString);
        new SqlConnectionStringBuilder(result).ApplicationName.ShouldBe(expected);
    }

    #endregion

    #region Disposal

    /// <summary>
    /// A call after the disposal is refused, so that it never opens a new pool (and a new session) once the Very Back is stopped; disposing
    /// twice is harmless
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ACallAfterTheDisposalIsRefusedAsync()
    {
        // Disposing a procedure that never opened a connection (nothing reaches the network)
        SqlPingProcedure procedure = new SqlPingProcedure(Options.Create(new ConnectionStringsOptions { Main = "Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=true" }));
        procedure.Dispose();
        procedure.Dispose();

        // Check
        await Should.ThrowAsync<ObjectDisposedException>(() => procedure.GetServerTimeAsync(TestContext.Current.CancellationToken));
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
        CancellationToken token = TestContext.Current.CancellationToken;
        string? connectionString = TestDatabase.ReadConnectionString();
        if (connectionString is null)
            Assert.Skip(TestDatabase.SkipReason);

        // Refusing any database that is not a throwaway one (the connection only proves the name of the database)
        await using (await TestDatabase.OpenAsync(connectionString, token))
        {
        }

        // Calling the procedure
        SqlPingProcedure procedure = new SqlPingProcedure(Options.Create(new ConnectionStringsOptions { Main = connectionString }));
        DateTimeOffset serverTime = await procedure.GetServerTimeAsync(token);

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
