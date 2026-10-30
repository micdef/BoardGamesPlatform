/*
 *	@file	test/BoardGamesPlatform.Web.Tests/GatewayLink/GatewayClientOptionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "Gateway" settings of the website: default value, accepted values and refusal at startup
 */

// Using
using System.Text.Json;
using BoardGamesPlatform.TestKit;
using BoardGamesPlatform.Web.GatewayLink;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.GatewayLink;

/// <summary>
/// Tests used to check the settings the website reads to reach the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a wrong address must stop the website at startup with a message that names the setting, not fail at the first click
/// of a player; and the default address must stay the one the Gateway listens on in development.</para>
/// <para><b>Behavior:</b> each test builds a host of the website's setup on the in-memory test server (no socket, no port).</para>
/// </remarks>
public sealed class GatewayClientOptionsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
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

    /// <summary>
    /// Method used to build the host of the website's gateway setup, on the in-memory test server
    /// </summary>
    /// <param name="hubUrl">[Can be null] Value of the Gateway:HubUrl setting; null to leave the setting out</param>
    /// <param name="requestTimeout">[Can be null] Value of the Gateway:RequestTimeout setting; null to leave the setting out</param>
    /// <returns>
    /// The built application, not started
    /// </returns>
    private static WebApplication BuildHost(string? hubUrl, string? requestTimeout = null)
    {
        // Building the host with the in-memory server and the setting
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        // Ignoring the environment variables of the machine (such as Gateway__HubUrl), which could change the result
        foreach (IConfigurationSource source in builder.Configuration.Sources.OfType<EnvironmentVariablesConfigurationSource>().ToList())
            builder.Configuration.Sources.Remove(source);
        if (hubUrl is not null)
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:HubUrl"] = hubUrl });
        if (requestTimeout is not null)
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Gateway:RequestTimeout"] = requestTimeout });

        // Registering the settings and the client of the Gateway
        builder.AddGatewayClient();

        // Function's return
        return builder.Build();
    }

    #endregion

    #region Publics

    /// <summary>
    /// Without a setting, the hub address is the HTTP address of the Gateway in development
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheDefaultHubUrlIsTheDevelopmentAddressOfTheGatewayAsync()
    {
        // Starting the host without the setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(null);
        await app.StartAsync(token);

        // Checks
        app.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value.HubUrl.ShouldBe("http://localhost:5273/hubs/platform");
    }

    /// <summary>
    /// The default hub address follows the http profile of the launch settings of the Gateway
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void TheDefaultHubUrlFollowsTheLaunchSettingsOfTheGateway()
    {
        // Reading the HTTP address of the Gateway in its launch settings
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        string path = Path.Combine(RepositoryPaths.GetSourceFolder(), "BoardGamesPlatform.Gateway", "Properties", "launchSettings.json");
        using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(path));
        string applicationUrl = settings.RootElement.GetProperty("profiles").GetProperty("http").GetProperty("applicationUrl").GetString()!;

        // Checks
        GatewayClientOptions.DefaultHubUrl.ShouldBe(applicationUrl + "/hubs/platform");
    }

    /// <summary>
    /// A hub address set by the configuration is used
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("http://gateway.internal:5273/hubs/platform")]
    [InlineData("https://gateway.example.org/hubs/platform")]
    public async Task AValidHubUrlIsUsedAsync(string hubUrl)
    {
        // Starting the host with the setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(hubUrl);
        await app.StartAsync(token);

        // Checks
        app.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value.HubUrl.ShouldBe(hubUrl);
    }

    /// <summary>
    /// An invalid hub address stops the startup; the message names the setting and never repeats its value
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("not-an-address-SECRET")]
    [InlineData("/hubs/platform/SECRET")]
    [InlineData("ftp://gateway.internal/SECRET")]
    [InlineData("   ")]
    public async Task AnInvalidHubUrlStopsTheStartupAsync(string hubUrl)
    {
        // Building the host with an invalid setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(hubUrl);

        // Starting it: the validation at startup refuses
        OptionsValidationException error = await Should.ThrowAsync<OptionsValidationException>(async () => await app.StartAsync(token));

        // Checks: the setting is named, its value is not
        error.Message.ShouldContain("Gateway:HubUrl");
        error.Message.ShouldNotContain("SECRET");
    }


    /// <summary>
    /// Without a setting, the request timeout is ten seconds
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheDefaultRequestTimeoutIsTenSecondsAsync()
    {
        // Starting the host without the setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(null);
        await app.StartAsync(token);

        // Checks
        app.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// A request timeout outside ]0, 1 minute] stops the startup and the message names the setting
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("00:00:00")]
    [InlineData("-00:00:05")]
    [InlineData("00:01:01")]
    public async Task AnInvalidRequestTimeoutStopsTheStartupAsync(string requestTimeout)
    {
        // Building the host with an invalid setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(null, requestTimeout);

        // Starting it: the validation at startup refuses
        OptionsValidationException error = await Should.ThrowAsync<OptionsValidationException>(async () => await app.StartAsync(token));

        // Checks
        error.Message.ShouldContain("Gateway:RequestTimeout");
    }

    /// <summary>
    /// A request timeout inside the range is used
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AValidRequestTimeoutIsUsedAsync()
    {
        // Starting the host with the setting
        CancellationToken token = TestContext.Current.CancellationToken;
        await using WebApplication app = BuildHost(null, "00:01:00");
        await app.StartAsync(token);

        // Checks
        app.Services.GetRequiredService<IOptions<GatewayClientOptions>>().Value.RequestTimeout.ShouldBe(TimeSpan.FromMinutes(1));
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
