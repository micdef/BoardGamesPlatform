/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/VeryBackExtensionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the setup of the Very Back host and its exit code
 */

// Using
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.VeryBack.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests;

/// <summary>
/// Tests used to check <see cref="VeryBackExtensions"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the host, built as Program.cs builds it, refuses to start on invalid settings before anything listens,
/// and that a failed background service gives a non-zero exit code, so that a service manager restarts the Very Back.</para>
/// <para><b>Behavior:</b> each host is built from settings in memory; it is started only with invalid settings, so no port is bound, and no
/// connection to a database is ever opened.</para>
/// </remarks>
public sealed class VeryBackExtensionsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test that starts a host, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// A connection string the SQL client reads; no test opens it
    /// </summary>
    private const string ReadableConnectionString = "Server=localhost;Database=TEST_BoardGamesPlatform;Integrated Security=true";

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
    /// Method used to build a host of the Very Back from settings in memory, on the loopback address and the default port (no test starts a
    /// host with valid settings, so the port is never bound)
    /// </summary>
    /// <param name="connectionString">Main connection string; null to leave it out</param>
    /// <returns>
    /// The host, not started, owned by the caller
    /// </returns>
    private static IHost BuildHost(string? connectionString)
    {
        // Building the host with the same registrations as Program.cs
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VeryBack:ListenAddress"] = "127.0.0.1",
            ["VeryBack:Port"] = "7822",
            ["ConnectionStrings:Main"] = connectionString,
        });
        builder.Services.AddLogging();
        builder.AddPlatformVeryBack();

        // Function's return
        return builder.Build();
    }

    #endregion

    #region Publics

    #region Startup

    /// <summary>
    /// A missing or unreadable connection string stops the host before the listener starts, and the message repeats nothing of the string
    /// </summary>
    /// <param name="connectionString">Invalid main connection string</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=localhost;Password=ab;Kx9secret=cd")]
    public async Task AnInvalidConnectionStringStopsTheHostBeforeItListensAsync(string? connectionString)
    {
        // Starting the host
        using IHost host = BuildHost(connectionString);
        OptionsValidationException exception = await Should.ThrowAsync<OptionsValidationException>(() => host.StartAsync(TestContext.Current.CancellationToken));

        // Checks: the setting is named, its value is not, and the listener cannot even be built with these settings, so nothing listens
        exception.Message.ShouldContain("ConnectionStrings:Main");
        exception.Message.ShouldNotContain("Kx9secret", Case.Insensitive);
        Should.Throw<OptionsValidationException>(() => host.Services.GetServices<IHostedService>().OfType<VeryBackListenerService>().ToArray());
    }

    /// <summary>
    /// With valid settings, the services of the Very Back are registered as Program.cs needs them: the listener of the Back, built with them
    /// </summary>
    [Fact]
    public void ValidSettingsRegisterTheListener()
    {
        // Building the host and its hosted services, without starting it (it would bind the configured port)
        using IHost host = BuildHost(ReadableConnectionString);

        // Check
        host.Services.GetServices<IHostedService>().OfType<VeryBackListenerService>().Count().ShouldBe(1);
    }

    #endregion

    #region Exit code

    /// <summary>
    /// A background service that failed gives the failure exit code; other services do not change the code
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailedBackgroundServiceGivesTheFailureExitCodeAsync()
    {
        // Running a background service that fails, next to one that is not a background service
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        using FailingBackgroundService failing = new FailingBackgroundService();
        await failing.StartAsync(cancellationToken);
        await Should.ThrowAsync<InvalidOperationException>(() => failing.ExecuteTask.ShouldNotBeNull().WaitAsync(cancellationToken));

        // Checks
        HostExitCode.FromHostedServices(new IHostedService[] { new IdleHostedService(), failing }).ShouldBe(HostExitCode.Failure);
        HostExitCode.FromHostedServices(new IHostedService[] { new IdleHostedService() }).ShouldBe(HostExitCode.Success);
        HostExitCode.FromHostedServices(Array.Empty<IHostedService>()).ShouldBe(HostExitCode.Success);
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
