/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Hubs/PlatformHubBackLinkTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the hub of the Gateway with the real Back client, pointed at a fake Back
 */

// Using
using System.Globalization;
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using BoardGamesPlatform.Gateway.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Hubs;

/// <summary>
/// Tests used to check the path from a front to the Back through the hub
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the Gateway, as wired by its own startup (settings, client, hub), reaches a Back on a socket.</para>
/// <para><b>Behavior:</b> each test starts the real Gateway in process with its real Back client, configured by <c>Back:Port</c> to reach a fake
/// Back on a loopback socket, or a port where nothing listens.</para>
/// </remarks>
public sealed class PlatformHubBackLinkTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Message a front receives when the Ping method fails on the server, detailed errors being off (Microsoft Learn, SignalR hubs, handle errors)
    /// </summary>
    private const string GenericPingError = "An unexpected error occurred invoking 'Ping' on the server.";

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
    /// Ping through the hub returns the time of the fake Back, with its offset
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingThroughTheHubReturnsTheTimeOfTheBackAsync()
    {
        // Starting a fake Back, the Gateway pointed at it, and a front
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeBack back = FakeBack.Start();
        await using GatewayFactory factory = new GatewayFactory(backPort: back.Port.ToString(CultureInfo.InvariantCulture));
        await using HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token);

        // Calling Ping
        PingResponse response = await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);

        // Checks
        response.ServerTime.ShouldBe(FakeBack.PingTime);
        response.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(1));
    }

    /// <summary>
    /// When the Back does not answer, the front receives the generic SignalR error only, and its connection still works
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task WhenTheBackDoesNotAnswerTheFrontReceivesTheGenericErrorAsync()
    {
        // Starting the Gateway pointed at a port where nothing listens, and a front
        CancellationToken token = TestContext.Current.CancellationToken;
        int closedPort = await FakeBack.GetClosedPortAsync();
        await using GatewayFactory factory = new GatewayFactory(backPort: closedPort.ToString(CultureInfo.InvariantCulture));
        await using HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token);

        // Calling Ping
        HubException error = await Should.ThrowAsync<HubException>(
            async () => await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token));

        // Checks: the generic message only, nothing of the socket failure, and the connection survives
        error.Message.ShouldBe(GenericPingError);
        error.Message.ShouldNotContain(closedPort.ToString(CultureInfo.InvariantCulture));
        connection.State.ShouldBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// An invalid Back:Port stops the startup, and the failure names the setting
    /// </summary>
    [Theory(Timeout = TestTimeout)]
    [InlineData("0")]
    [InlineData("65536")]
    public void AnInvalidBackPortStopsTheStartup(string port)
    {
        // Starting the Gateway with an invalid port
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        using GatewayFactory factory = new GatewayFactory(backPort: port);

        // Checks: an options validation failure, found in the exception chain, that names the setting and not its value
        Exception error = Should.Throw<Exception>(() => factory.CreateClient());
        OptionsValidationException? validation = null;
        for (Exception? current = error; current is not null && validation is null; current = current.InnerException)
            validation = current as OptionsValidationException;
        validation.ShouldNotBeNull();
        validation.Message.ShouldContain("Back:Port");
        validation.Message.ShouldNotContain(port);
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
