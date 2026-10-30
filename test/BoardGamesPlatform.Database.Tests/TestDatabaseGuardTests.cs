/*
 *	@file	test/BoardGamesPlatform.Database.Tests/TestDatabaseGuardTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the guard shared by every SQL test, without a server
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
using Guard = BoardGamesPlatform.TestKit.TestDatabase;

// Namespace
namespace BoardGamesPlatform.Database.Tests;

/// <summary>
/// Tests used to check the name rule of <see cref="Guard"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves the rule "the SQL tests only run on a TEST_ or DEV_ database", which protects every other database from the
/// tests that create, drop or call objects.</para>
/// <para><b>Behavior:</b> the name rule is a pure function, checked here on names; the refusal of a real connection to another database
/// (for example master) needs a server, and is proven by running the SQL tests with the variable pointing at it.</para>
/// </remarks>
public sealed class TestDatabaseGuardTests
{
    #region Members

    #region Constants

    // #N/A

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

    /// <summary>
    /// A name that starts with TEST_ or DEV_, in upper case, is a throwaway database
    /// </summary>
    /// <param name="name">Name of the database</param>
    [Theory]
    [InlineData("TEST_BoardGamesPlatform")]
    [InlineData("TEST_BoardGamesPlatformE2E")]
    [InlineData("DEV_BoardGamesPlatform")]
    [InlineData("TEST_")]
    public void ATestOrDevNameIsAccepted(string name)
    {
        // Check
        Guard.IsThrowawayName(name).ShouldBeTrue();
    }

    /// <summary>
    /// Any other name is refused: system databases, production names, wrong case, a prefix not at the start, blank
    /// </summary>
    /// <param name="name">Name of the database</param>
    [Theory]
    [InlineData("master")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    [InlineData("Prod_X")]
    [InlineData("BoardGamesPlatform")]
    [InlineData("test_BoardGamesPlatform")]
    [InlineData("Dev_BoardGamesPlatform")]
    [InlineData("TEST")]
    [InlineData("DEV")]
    [InlineData("PROD_TEST_X")]
    [InlineData(" TEST_X")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherNameIsRefused(string? name)
    {
        // Check
        Guard.IsThrowawayName(name).ShouldBeFalse();
    }

    /// <summary>
    /// A blank connection string is refused before any connection is tried
    /// </summary>
    /// <param name="connectionString">Blank connection string</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankConnectionStringIsRefusedAsync(string connectionString)
    {
        // Check
        await Should.ThrowAsync<ArgumentNullException>(() => Guard.OpenAsync(connectionString, TestContext.Current.CancellationToken));
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
