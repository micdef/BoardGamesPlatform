/*
 *	@file	test/BoardGamesPlatform.Platform.Infrastructure.Tests/VeryBack/VeryBackClientOptionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the defaults and the validation of the settings of the Very Back client
 */

// Using
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.Tests.VeryBack;

/// <summary>
/// Tests used to check the settings of the Very Back client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Back must refuse to start on an invalid setting (fail closed), and the defaults must suit a development machine.</para>
/// </remarks>
public sealed class VeryBackClientOptionsTests
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
    /// The defaults point to the loopback address, port 7822, with a 5 second connect timeout, and are valid
    /// </summary>
    [Fact]
    public void TheDefaultsAreValid()
    {
        // Reading the defaults
        VeryBackClientOptions options = new VeryBackClientOptions();

        // Checks
        options.Host.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(7822);
        options.ConnectTimeout.ShouldBe(TimeSpan.FromSeconds(5));
        Should.NotThrow(options.Validate);
    }

    /// <summary>
    /// The limits of the port are accepted
    /// </summary>
    /// <param name="port">Port at the limit of the valid range</param>
    [Theory]
    [InlineData(1)]
    [InlineData(65535)]
    public void ThePortLimitsAreAccepted(int port)
    {
        // Check
        Should.NotThrow(new VeryBackClientOptions { Port = port }.Validate);
    }

    /// <summary>
    /// A port outside 1 to 65535 is refused, and the message names the setting
    /// </summary>
    /// <param name="port">Invalid port</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void AnInvalidPortIsRefused(int port)
    {
        // Check
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(new VeryBackClientOptions { Port = port }.Validate);
        exception.Message.ShouldContain("VeryBack:Port");
    }

    /// <summary>
    /// An empty host is refused, and the message names the setting
    /// </summary>
    /// <param name="host">Invalid host</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyHostIsRefused(string host)
    {
        // Check
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(new VeryBackClientOptions { Host = host }.Validate);
        exception.Message.ShouldContain("VeryBack:Host");
    }

    /// <summary>
    /// A connect timeout of zero or less is refused, and the message names the setting
    /// </summary>
    /// <param name="seconds">Invalid timeout, in seconds</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ANonPositiveTimeoutIsRefused(int seconds)
    {
        // Check
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            new VeryBackClientOptions { ConnectTimeout = TimeSpan.FromSeconds(seconds) }.Validate);
        exception.Message.ShouldContain("VeryBack:ConnectTimeout");
    }

    /// <summary>
    /// A connect timeout above the maximum is refused at startup, instead of making every call fail when the deadline is set
    /// </summary>
    /// <param name="minutes">Too long timeout, in minutes (about 49.7 days is the limit of a cancellation deadline)</param>
    [Theory]
    [InlineData(6)]
    [InlineData(100000)]
    public void ATooLongTimeoutIsRefused(int minutes)
    {
        // Check
        InvalidOperationException exception = Should.Throw<InvalidOperationException>(
            new VeryBackClientOptions { ConnectTimeout = TimeSpan.FromMinutes(minutes) }.Validate);
        exception.Message.ShouldContain("VeryBack:ConnectTimeout");
    }

    /// <summary>
    /// The maximum connect timeout itself is accepted
    /// </summary>
    [Fact]
    public void TheMaximumTimeoutIsAccepted()
    {
        // Check
        new VeryBackClientOptions { ConnectTimeout = VeryBackClientOptions.MaxConnectTimeout }.Validate();
    }

    /// <summary>
    /// Valid settings, the defaults and the limits included, have no problem
    /// </summary>
    [Fact]
    public void ValidSettingsHaveNoProblem()
    {
        // Checks
        new VeryBackClientOptions().FindProblem().ShouldBeNull();
        new VeryBackClientOptions { Port = 1 }.FindProblem().ShouldBeNull();
        new VeryBackClientOptions { Port = 65535 }.FindProblem().ShouldBeNull();
        new VeryBackClientOptions { ConnectTimeout = VeryBackClientOptions.MaxConnectTimeout }.FindProblem().ShouldBeNull();
    }

    /// <summary>
    /// Each invalid setting is reported by a message that names it
    /// </summary>
    /// <param name="host">Host to check</param>
    /// <param name="port">Port to check</param>
    /// <param name="seconds">Connect timeout to check, in seconds</param>
    /// <param name="setting">Setting expected in the message</param>
    [Theory]
    [InlineData("", 7822, 5, "VeryBack:Host")]
    [InlineData("   ", 7822, 5, "VeryBack:Host")]
    [InlineData("127.0.0.1", 0, 5, "VeryBack:Port")]
    [InlineData("127.0.0.1", 65536, 5, "VeryBack:Port")]
    [InlineData("127.0.0.1", 7822, 0, "VeryBack:ConnectTimeout")]
    [InlineData("127.0.0.1", 7822, -1, "VeryBack:ConnectTimeout")]
    [InlineData("127.0.0.1", 7822, 3600, "VeryBack:ConnectTimeout")]
    public void AnInvalidSettingIsReportedByName(string host, int port, int seconds, string setting)
    {
        // Reading the problem
        string? problem = new VeryBackClientOptions { Host = host, Port = port, ConnectTimeout = TimeSpan.FromSeconds(seconds) }.FindProblem();

        // Checks
        problem.ShouldNotBeNull();
        problem.ShouldContain(setting);
    }

    /// <summary>
    /// The exception of <see cref="VeryBackClientOptions.Validate"/> carries the message of <see cref="VeryBackClientOptions.FindProblem"/>
    /// </summary>
    [Fact]
    public void TheExceptionCarriesTheProblem()
    {
        // Reading both
        VeryBackClientOptions options = new VeryBackClientOptions { Port = 0 };

        // Check
        Should.Throw<InvalidOperationException>(options.Validate).Message.ShouldBe(options.FindProblem());
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
