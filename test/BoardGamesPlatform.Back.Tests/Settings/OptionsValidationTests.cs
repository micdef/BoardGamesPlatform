/*
 *	@file	test/BoardGamesPlatform.Back.Tests/Settings/OptionsValidationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the validation of the settings of the Back
 */

// Using
using BoardGamesPlatform.Back.Settings;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests.Settings;

/// <summary>
/// Tests used to check <see cref="BackOptionsValidator"/> and <see cref="VeryBackClientOptionsValidator"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a Back with an unusable listen endpoint or an unusable address of the Very Back must refuse to start (fail
/// closed).</para>
/// <para><b>Behavior:</b> the validators are called directly; the startup of a whole host is proven by the tests of the host setup.</para>
/// </remarks>
public sealed class OptionsValidationTests
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

    #region Back

    /// <summary>
    /// By default, the Back listens on the loopback address and port 7821 (Gateway to Back)
    /// </summary>
    [Fact]
    public void TheDefaultsAreTheLoopbackAddressAndPort7821()
    {
        // Checks
        BackOptions options = new BackOptions();
        options.ListenAddress.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(7821);
        new BackOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// An address that is not an IP address, or a port outside 1-65535, is refused, and the message names the setting
    /// </summary>
    /// <param name="address">Listen address</param>
    /// <param name="port">Listen port</param>
    /// <param name="setting">Setting the message must name</param>
    [Theory]
    [InlineData("localhost", 7821, "Back:ListenAddress")]
    [InlineData("", 7821, "Back:ListenAddress")]
    [InlineData("127.0.0.1", 0, "Back:Port")]
    [InlineData("127.0.0.1", 65536, "Back:Port")]
    public void AnInvalidEndpointIsRefused(string address, int port, string setting)
    {
        // Check
        ValidateOptionsResult result = new BackOptionsValidator().Validate(null, new BackOptions { ListenAddress = address, Port = port });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(setting);
    }

    #endregion

    #region VeryBack

    /// <summary>
    /// The settings of the Very Back client are accepted by default
    /// </summary>
    [Fact]
    public void TheDefaultVeryBackSettingsAreAccepted()
    {
        // Check
        new VeryBackClientOptionsValidator().Validate(null, new VeryBackClientOptions()).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// An invalid setting of the Very Back client is a validation failure that names the setting, as the checks of the settings say it
    /// </summary>
    /// <param name="host">Host of the Very Back</param>
    /// <param name="port">Port of the Very Back</param>
    /// <param name="seconds">Connect timeout, in seconds</param>
    /// <param name="setting">Setting the message must name</param>
    [Theory]
    [InlineData("", 7822, 5, "VeryBack:Host")]
    [InlineData("127.0.0.1", 0, 5, "VeryBack:Port")]
    [InlineData("127.0.0.1", 7822, 0, "VeryBack:ConnectTimeout")]
    [InlineData("127.0.0.1", 7822, 3600, "VeryBack:ConnectTimeout")]
    public void AnInvalidVeryBackSettingIsRefused(string host, int port, int seconds, string setting)
    {
        // Check
        VeryBackClientOptions options = new VeryBackClientOptions { Host = host, Port = port, ConnectTimeout = TimeSpan.FromSeconds(seconds) };
        ValidateOptionsResult result = new VeryBackClientOptionsValidator().Validate(null, options);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(setting);
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
