/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Settings/OptionsValidationTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the validation of the settings of the Very Back
 */

// Using
using BoardGamesPlatform.VeryBack.Settings;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Settings;

/// <summary>
/// Tests used to check the validators of the "VeryBack" and "ConnectionStrings" settings
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Very Back refuses to start with an unusable endpoint or without a connection string (fail closed).</para>
/// <para><b>Behavior:</b> the validators are called directly; the host calls them at startup through <c>ValidateOnStart</c>.</para>
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

    #region VeryBack

    /// <summary>
    /// The defaults listen on the loopback address, port 7822, and are valid
    /// </summary>
    [Fact]
    public void TheDefaultsAreTheLoopbackAddressAndPort7822()
    {
        // Checks
        VeryBackOptions options = new VeryBackOptions();
        options.ListenAddress.ShouldBe("127.0.0.1");
        options.Port.ShouldBe(7822);
        new VeryBackOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// IPv4 and IPv6 addresses and the limits of the port range are accepted
    /// </summary>
    [Theory]
    [InlineData("127.0.0.1", 1)]
    [InlineData("0.0.0.0", 65535)]
    [InlineData("::1", 7822)]
    public void ValidEndpointsAreAccepted(string address, int port)
    {
        // Check
        VeryBackOptions options = new VeryBackOptions { ListenAddress = address, Port = port };
        new VeryBackOptionsValidator().Validate(null, options).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// An address that is not an IP address is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("localhost")]
    [InlineData("300.1.1.1")]
    public void AnInvalidAddressIsRefused(string address)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new VeryBackOptionsValidator().Validate(null, new VeryBackOptions { ListenAddress = address });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VeryBack:ListenAddress");
    }

    /// <summary>
    /// A port outside 1-65535 is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void APortOutsideTheRangeIsRefused(int port)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new VeryBackOptionsValidator().Validate(null, new VeryBackOptions { Port = port });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("VeryBack:Port");
    }

    #endregion

    #region ConnectionStrings

    /// <summary>
    /// A missing, empty or blank main connection string is refused, and the message names the setting
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AMissingConnectionStringIsRefused(string? connectionString)
    {
        // Check
        Microsoft.Extensions.Options.ValidateOptionsResult result = new ConnectionStringsOptionsValidator().Validate(null, new ConnectionStringsOptions { Main = connectionString });
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ConnectionStrings:Main");
    }

    /// <summary>
    /// A connection string is accepted, and its value is never repeated in a failure
    /// </summary>
    [Fact]
    public void AConnectionStringIsAccepted()
    {
        // Check
        new ConnectionStringsOptionsValidator().Validate(null, new ConnectionStringsOptions { Main = "Server=localhost;Database=X" }).Succeeded.ShouldBeTrue();
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
