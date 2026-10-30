/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Hubs/PlatformHubTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the SignalR hub of the Gateway, started in process, as a front calls it
 */

// Using
using System.Net;
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using BoardGamesPlatform.Gateway.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Hubs;

/// <summary>
/// Tests used to check the Ping method of the hub, its settings and the health endpoint of the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the hub is the only door of the fronts: its method name, its path, its answer and the absence of internal
/// messages in its errors must not change by accident.</para>
/// <para><b>Behavior:</b> each test starts the real Gateway in process (no network port), with a fake Back client, and calls it with a
/// SignalR client by long polling.</para>
/// </remarks>
public sealed class PlatformHubTests
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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// A front calling Ping receives the time of the Back, with its offset
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingReturnsTheTimeOfTheBackWithItsOffsetAsync()
    {
        // Starting the Gateway with a fake Back, and connecting a front
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, 123, TimeSpan.FromHours(1));
        FakeBackClient back = new FakeBackClient(serverTime);
        await using GatewayFactory factory = new GatewayFactory(back);
        await using HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token);

        // Calling Ping
        PingResponse response = await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);

        // Checks
        response.ServerTime.ShouldBe(serverTime);
        response.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(1));
        back.PingCalls.ShouldBe(1);
    }

    /// <summary>
    /// When the Back fails, the front receives an error that does not reveal the internal message
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingBackGivesTheFrontAnErrorWithoutTheInternalMessageAsync()
    {
        // Starting the Gateway with a Back that fails with a message to keep secret
        CancellationToken token = TestContext.Current.CancellationToken;
        const string InternalMessage = "secret internal detail of the Back link";
        await using GatewayFactory factory = new GatewayFactory(new FakeBackClient(new InvalidOperationException(InternalMessage)));
        await using HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token);

        // Calling Ping
        HubException error = await Should.ThrowAsync<HubException>(
            async () => await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token));

        // Checks
        error.Message.ShouldNotContain(InternalMessage);
        error.Message.ShouldNotContain(nameof(InvalidOperationException));
    }

    /// <summary>
    /// Without the real link to the Back, the default client makes Ping fail cleanly
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheDefaultBackClientMakesPingFailCleanlyAsync()
    {
        // Starting the Gateway as it is, and connecting a front
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayFactory factory = new GatewayFactory();
        await using HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token);

        // Calling Ping
        HubException error = await Should.ThrowAsync<HubException>(
            async () => await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token));

        // Checks (the connection still works after the failure)
        error.Message.ShouldNotContain("not wired");
        connection.State.ShouldBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// The path of the hub is set by the configuration, and the default path is then no longer served
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheHubPathSettingIsHonouredAsync()
    {
        // Starting the Gateway with another path
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);
        await using GatewayFactory factory = new GatewayFactory(new FakeBackClient(serverTime), "/custom/hub");

        // Calling Ping on the new path
        await using (HubConnection connection = factory.CreateHubConnection("/custom/hub"))
        {
            await connection.StartAsync(token);
            PingResponse response = await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);
            response.ServerTime.ShouldBe(serverTime);
        }

        // The default path is not served
        await using HubConnection other = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await Should.ThrowAsync<HttpRequestException>(async () => await other.StartAsync(token));
    }

    /// <summary>
    /// A hub path that does not start with a slash stops the startup
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void AHubPathWithoutALeadingSlashStopsTheStartup()
    {
        // Starting the Gateway with an invalid path
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using GatewayFactory factory = new GatewayFactory(new FakeBackClient(DateTimeOffset.UtcNow), "hubs/platform");

        // Checks (the exception names the setting)
        Exception error = Should.Throw<Exception>(() => factory.CreateClient());
        error.ToString().ShouldContain("Gateway:HubPath");
    }

    /// <summary>
    /// The health endpoint still answers
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheHealthEndpointStillAnswersAsync()
    {
        // Starting the Gateway and calling /health
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayFactory factory = new GatewayFactory();
        using HttpClient client = factory.CreateClient();
        using HttpResponseMessage response = await client.GetAsync("/health", token);

        // Checks
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
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
