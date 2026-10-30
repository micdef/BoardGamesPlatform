/*
 *	@file	test/BoardGamesPlatform.Back.Tests/BackExtensionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the setup of the Back host
 */

// Using
using BoardGamesPlatform.Back.Services;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests;

/// <summary>
/// Tests used to check <see cref="BackExtensions"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the host, built as Program.cs builds it, refuses to start on invalid settings before anything listens,
/// and registers what the listener of the Gateway needs.</para>
/// <para><b>Behavior:</b> each host is built from settings in memory; it is started only with invalid settings, so no port is bound and no
/// connection is opened.</para>
/// </remarks>
public sealed class BackExtensionsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test that starts a host, in milliseconds
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

    /// <summary>
    /// Method used to build a host of the Back from settings in memory
    /// </summary>
    /// <param name="backPort">Port the Back listens on</param>
    /// <param name="veryBackPort">Port of the Very Back</param>
    /// <returns>
    /// The host, not started, owned by the caller
    /// </returns>
    private static IHost BuildHost(string backPort, string veryBackPort)
    {
        // Building the host with the same registrations as Program.cs
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Back:ListenAddress"] = "127.0.0.1",
            ["Back:Port"] = backPort,
            ["VeryBack:Host"] = "127.0.0.1",
            ["VeryBack:Port"] = veryBackPort,
        });
        builder.Services.AddLogging();
        builder.AddPlatformBack();

        // Function's return
        return builder.Build();
    }

    #endregion

    #region Publics

    /// <summary>
    /// An invalid port of the Back or of the Very Back stops the host before the listener starts, and the message names the setting
    /// </summary>
    /// <param name="backPort">Port the Back listens on</param>
    /// <param name="veryBackPort">Port of the Very Back</param>
    /// <param name="setting">Setting the message must name</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData("0", "7822", "Back:Port")]
    [InlineData("7821", "70000", "VeryBack:Port")]
    public async Task AnInvalidSettingStopsTheHostBeforeItListensAsync(string backPort, string veryBackPort, string setting)
    {
        // Starting the host
        using IHost host = BuildHost(backPort, veryBackPort);
        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // Checks: the setting is named, and the listener cannot even be built with these settings, so nothing listens
        exception.Message.ShouldContain(setting);
        Should.Throw<OptionsValidationException>(() => host.Services.GetServices<IHostedService>().OfType<BackListenerService>().ToArray());
    }

    /// <summary>
    /// With valid settings, the host registers one listener of the Gateway and one shared client of the Very Back
    /// </summary>
    [Fact]
    public void ValidSettingsRegisterTheListenerAndTheSharedClient()
    {
        // Building the host and its services, without starting it (it would bind the configured port)
        using IHost host = BuildHost("7821", "7822");

        // Checks
        host.Services.GetServices<IHostedService>().OfType<BackListenerService>().Count().ShouldBe(1);
        host.Services.GetRequiredService<VeryBackClient>().ShouldBeSameAs(host.Services.GetRequiredService<VeryBackClient>());
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
