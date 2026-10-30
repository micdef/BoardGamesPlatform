/*
 *	@file	test/BoardGamesPlatform.Web.Tests/GatewayLink/SignalRGatewayClientTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the SignalR client of the website against the real Gateway started in process
 */

// Using
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using BoardGamesPlatform.Web.GatewayLink;
using BoardGamesPlatform.Web.Tests.Support;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.GatewayLink;

/// <summary>
/// Tests used to check the client that the website uses to call the hub of the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the website is the only SignalR client of the Gateway: the contract (method name, answer, error) must hold
/// from end to end.</para>
/// <para><b>Behavior:</b> each test starts the real Gateway in process (in-memory test server, no socket, no port) with a fake Back
/// client, and calls it with the real client of the website.</para>
/// </remarks>
public sealed class SignalRGatewayClientTests
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
    /// The client connects on its first call, and returns the time of the Back with its offset
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingConnectsOnFirstUseAndReturnsTheTimeWithItsOffsetAsync()
    {
        // Starting the Gateway with a fake Back, and building the client of the website: nothing is connected yet
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, 123, TimeSpan.FromHours(1));
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(serverTime));
        HubConnection connection = gateway.CreateHubConnection(PlatformHubContract.DefaultPath);
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection);
        connection.State.ShouldBe(HubConnectionState.Disconnected);

        // Calling Ping
        PingResponse response = await client.PingAsync(token);

        // Checks: the time of the fake Back with its offset, and the connection is now open
        response.ServerTime.ShouldBe(serverTime);
        response.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(1));
        connection.State.ShouldBe(HubConnectionState.Connected);
    }

    /// <summary>
    /// When the Back fails, the client raises the generic error of the hub, without the internal message
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingBackGivesTheClientAnErrorWithoutTheInternalMessageAsync()
    {
        // Starting the Gateway with a Back that fails with a message to keep secret
        CancellationToken token = TestContext.Current.CancellationToken;
        const string InternalMessage = "secret internal detail of the Back link";
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(new InvalidOperationException(InternalMessage)));
        await using SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath));

        // Calling Ping
        HubException error = await Should.ThrowAsync<HubException>(async () => await client.PingAsync(token));

        // Checks: the generic message of SignalR and nothing else
        error.Message.ShouldBe(GenericPingError);
        error.Message.ShouldNotContain(InternalMessage);
    }

    /// <summary>
    /// When the Gateway cannot be reached, the client fails, and works again on a later call
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AnUnreachableHubMakesPingFailAsync()
    {
        // Building the client on a path the Gateway does not serve
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(DateTimeOffset.UtcNow));
        HubConnection connection = gateway.CreateHubConnection("/not/the/hub");
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection);

        // Calling Ping: the connection cannot start
        await Should.ThrowAsync<HttpRequestException>(async () => await client.PingAsync(token));

        // Checks: the connection is left disconnected, ready for a new try
        connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    /// <summary>
    /// Disposing the client closes its connection, and the client then refuses new calls
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingTheClientClosesItsConnectionAsync()
    {
        // Starting the Gateway and connecting through a first call
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(DateTimeOffset.UtcNow));
        HubConnection connection = gateway.CreateHubConnection(PlatformHubContract.DefaultPath);
        SignalRGatewayClient client = new SignalRGatewayClient(connection);
        await client.PingAsync(token);
        connection.State.ShouldBe(HubConnectionState.Connected);

        // Disposing, as the end of the circuit does
        await client.DisposeAsync();

        // Checks: the connection is closed, further calls are refused, and disposing again is harmless
        connection.State.ShouldBe(HubConnectionState.Disconnected);
        await Should.ThrowAsync<ObjectDisposedException>(async () => await client.PingAsync(token));
        await client.DisposeAsync();
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
