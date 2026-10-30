/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/BackLink/BackClientOptionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the settings of the link to the Back: defaults, and refusal of invalid values
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Gateway.BackLink;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.BackLink;

/// <summary>
/// Tests used to check the "Back" settings of the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the defaults are what a development machine relies on, and an invalid value must stop the Gateway without ever
/// echoing the value.</para>
/// <para><b>Behavior:</b> pure unit tests of the options and of their validator; no socket.</para>
/// </remarks>
public sealed class BackClientOptionsTests
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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// The defaults are the local development ones, and are valid
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheDefaultsAreTheDevelopmentOnesAndAreValid()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Reading the defaults
        BackClientOptions options = new BackClientOptions();

        // Checks
        options.Host.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(7821);
        options.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        options.FindProblem().ShouldBeNull();
        new BackClientOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// The limits of the port and of the connect timeout are accepted
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheLimitsAreAccepted()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Checks: the first and last port, the longest timeout, and the shortest positive one
        new BackClientOptions { Port = 1 }.FindProblem().ShouldBeNull();
        new BackClientOptions { Port = 65535 }.FindProblem().ShouldBeNull();
        new BackClientOptions { ConnectTimeout = BackClientOptions.MaxConnectTimeout }.FindProblem().ShouldBeNull();
        new BackClientOptions { ConnectTimeout = TimeSpan.FromTicks(1) }.FindProblem().ShouldBeNull();
    }

    /// <summary>
    /// A blank host is refused and the setting is named
    /// </summary>
    /// <param name="host">Invalid host</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankHostIsRefused(string host)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Validating
        ValidateOptionsResult result = new BackClientOptionsValidator().Validate(null, new BackClientOptions { Host = host });

        // Checks
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Back:Host");
    }

    /// <summary>
    /// A port out of range is refused, the setting is named and the value is not echoed
    /// </summary>
    /// <param name="port">Invalid port</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData(0)]
    [InlineData(-12345)]
    [InlineData(65536)]
    [InlineData(987654)]
    public void APortOutOfRangeIsRefused(int port)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Validating
        ValidateOptionsResult result = new BackClientOptionsValidator().Validate(null, new BackClientOptions { Port = port });

        // Checks
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Back:Port");
        result.FailureMessage.ShouldNotContain(port.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A connect timeout that is zero, negative or too long is refused and the setting is named
    /// </summary>
    /// <param name="seconds">Invalid timeout, in seconds</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(301)]
    [InlineData(86400)]
    public void AnInvalidConnectTimeoutIsRefused(int seconds)
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Validating
        ValidateOptionsResult result = new BackClientOptionsValidator().Validate(
            null, new BackClientOptions { ConnectTimeout = TimeSpan.FromSeconds(seconds) });

        // Checks
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Back:ConnectTimeout");
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
