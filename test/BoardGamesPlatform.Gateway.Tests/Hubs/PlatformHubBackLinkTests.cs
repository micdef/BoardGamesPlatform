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
/// Back on a loopback socket, or a port where nothing listens. The cancellation test replaces the client by one that never answers, and reaches the hub over a WebSocket.</para>
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

    /// <summary>
    /// Longest duration of a test that waits for a refused connection, in milliseconds: on Windows a refusal on the loopback address takes about
    /// 2 seconds
    /// </summary>
    private const int RefusalTestTimeout = 30000;

    /// <summary>
    /// Longest duration of a test whose steps are bounded one by one, in milliseconds: above the sum of its steps, so that a step that hangs
    /// fails by its own name first
    /// </summary>
    private const int SteppedTestTimeout = 20000;

    #endregion

    #region Variables

    /// <summary>
    /// Longest wait of one step of a test, so that a step that hangs fails by its own name before the timeout of the test
    /// </summary>
    private static readonly TimeSpan _stepTimeout = TimeSpan.FromSeconds(3);

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
    [Fact(Timeout = RefusalTestTimeout)]
    public async Task WhenTheBackDoesNotAnswerTheFrontReceivesTheGenericErrorAsync()
    {
        // Starting the Gateway pointed at a port where nothing listens, and a front
        CancellationToken token = TestContext.Current.CancellationToken;
        int closedPort = TestPorts.Block.Next();
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
    /// An invalid Back setting stops the startup, and the failure names the setting without echoing the value
    /// </summary>
    /// <param name="setting">Key of the invalid setting</param>
    /// <param name="value">Invalid value; distinctive, so that its absence from the message proves it is not echoed</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData("Back:Port", "70123")]
    [InlineData("Back:Port", "-70123")]
    [InlineData("Back:Host", "   ")]
    [InlineData("Back:ConnectTimeout", "00:00:00")]
    [InlineData("Back:ConnectTimeout", "00:05:01")]
    public async Task AnInvalidBackSettingStopsTheStartupAsync(string setting, string value)
    {
        // Starting the Gateway with an invalid setting, as its Program does (see GatewayStartup for why not through the factory)
        Dictionary<string, string?> settings = new Dictionary<string, string?> { [setting] = value };
        OptionsValidationException validation = await GatewayStartup.FindStartupFailureAsync(settings, TestContext.Current.CancellationToken);

        // Checks: the failure names the setting and not its value
        validation.Message.ShouldContain(setting);
        if (value.Trim().Length > 0)
            validation.Message.ShouldNotContain(value);
    }

    /// <summary>
    /// When the front disconnects, the call to the Back client is canceled
    /// </summary>
    [Fact(Timeout = SteppedTestTimeout)]
    public async Task WhenTheFrontDisconnectsTheCallToTheBackClientIsCanceledAsync()
    {
        // Starting the Gateway with a Back client that never answers, and a front that asks Ping without waiting for the answer; over a
        // WebSocket, whose close ends the connection on the server at once (see CreateWebSocketHubConnection)
        CancellationToken token = TestContext.Current.CancellationToken;
        HangingBackClient back = new HangingBackClient();
        await using GatewayFactory factory = new GatewayFactory(back);
        await using HubConnection connection = factory.CreateWebSocketHubConnection(PlatformHubContract.DefaultPath);
        await connection.StartAsync(token).WaitAsync(_stepTimeout, token);
        Task<PingResponse> pending = connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);

        // Waiting until the Back client holds the call, then disconnecting the front; each step is bounded, so that a failure names it
        await back.CallReceived.WaitAsync(_stepTimeout, token);
        back.CallCanceled.IsCompleted.ShouldBeFalse();
        await connection.StopAsync(token).WaitAsync(_stepTimeout, token);

        // Checks: the token of the call is canceled, and the call of the front ends with an error
        await back.CallCanceled.WaitAsync(_stepTimeout, token);
        Exception error = await Should.ThrowAsync<Exception>(async () => await pending.WaitAsync(_stepTimeout, token));
        error.ShouldNotBeOfType<TimeoutException>();
    }

    /// <summary>
    /// Disposing the host disposes the Back client, which closes its connection to the Back
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingTheHostClosesTheConnectionToTheBackAsync()
    {
        // Starting a fake Back and the Gateway, and making one Ping so that the connection is open
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeBack back = FakeBack.Start();
        GatewayFactory factory = new GatewayFactory(backPort: back.Port.ToString(CultureInfo.InvariantCulture));
        await using (HubConnection connection = factory.CreateHubConnection(PlatformHubContract.DefaultPath))
        {
            await connection.StartAsync(token);
            PingResponse response = await connection.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);
            response.ServerTime.ShouldBe(FakeBack.PingTime);
        }
        back.ConnectionClosed.IsCompleted.ShouldBeFalse();

        // Disposing the host
        await factory.DisposeAsync();

        // Checks: the fake Back sees its connection close
        await back.ConnectionClosed.WaitAsync(token);
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
