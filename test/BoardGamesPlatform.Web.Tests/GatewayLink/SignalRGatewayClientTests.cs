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

    /// <summary>
    /// Timeout of the client in the tests that do not test it: longer than a test, so that it never ends a call
    /// </summary>
    private static readonly TimeSpan _clientTimeout = TimeSpan.FromSeconds(60);

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
    /// Method used to run a call that is expected to fail and to get its exception
    /// </summary>
    /// <param name="call">[Cannot be null] Call that fails</param>
    /// <returns>
    /// The exception raised by the call
    /// </returns>
    private static async Task<Exception> CaptureAsync(Func<Task> call)
    {
        // Running the call and keeping its exception, whatever its type
        try
        {
            await call();
        }
        catch (Exception exception)
        {
            return exception;
        }

        // The call did not fail
        throw new InvalidOperationException("The call was expected to fail");
    }

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
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);
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
        await using SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath), _clientTimeout);

        // Calling Ping
        Exception error = await CaptureAsync(() => client.PingAsync(token));

        // Checks: the generic message of SignalR and nothing else
        error.ShouldBeOfType<HubException>();
        error.Message.ShouldBe(GenericPingError);
        error.Message.ShouldNotContain(InternalMessage);
    }

    /// <summary>
    /// When the hub is not served at the address, the call fails and the connection is left disconnected
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AnUnreachableHubMakesPingFailAndLeavesTheConnectionDisconnectedAsync()
    {
        // Building the client on a path the Gateway does not serve
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(DateTimeOffset.UtcNow));
        HubConnection connection = gateway.CreateHubConnection("/not/the/hub");
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);

        // Calling Ping: the connection cannot start
        Exception error = await CaptureAsync(() => client.PingAsync(token));

        // Checks: a failure, and the connection ready for a new try
        error.ShouldNotBeNull();
        connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    /// <summary>
    /// A first start that fails does not spoil the client: the next call connects and succeeds
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailedFirstStartIsFollowedByASuccessfulCallAsync()
    {
        // Starting the Gateway, with a first negotiation that fails
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);
        ConnectionProbe probe = new ConnectionProbe();
        probe.FailNext(1);
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(serverTime));
        HubConnection connection = gateway.CreateHubConnection(PlatformHubContract.DefaultPath, probe);
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);

        // First call: the start fails
        await CaptureAsync(() => client.PingAsync(token));
        connection.State.ShouldBe(HubConnectionState.Disconnected);

        // Second call: the connection starts again and the call succeeds
        PingResponse response = await client.PingAsync(token);

        // Checks
        response.ServerTime.ShouldBe(serverTime);
        probe.Negotiations.ShouldBe(2);
    }

    /// <summary>
    /// After the connection is lost, the next call starts it again and succeeds
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ALostConnectionIsStartedAgainByTheNextCallAsync()
    {
        // Connecting through a first call
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);
        ConnectionProbe probe = new ConnectionProbe();
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(serverTime));
        HubConnection connection = gateway.CreateHubConnection(PlatformHubContract.DefaultPath, probe);
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);
        await client.PingAsync(token);

        // Dropping the connection
        await connection.StopAsync(token);
        connection.State.ShouldBe(HubConnectionState.Disconnected);

        // Calling again
        PingResponse response = await client.PingAsync(token);

        // Checks: connected again, with a second start
        response.ServerTime.ShouldBe(serverTime);
        connection.State.ShouldBe(HubConnectionState.Connected);
        probe.Negotiations.ShouldBe(2);
    }

    /// <summary>
    /// Two calls made at the same time on a new client start the connection once
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TwoParallelFirstCallsStartTheConnectionOnceAsync()
    {
        // Building the client of a new connection
        CancellationToken token = TestContext.Current.CancellationToken;
        DateTimeOffset serverTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.Zero);
        ConnectionProbe probe = new ConnectionProbe();
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(serverTime));
        await using SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath, probe), _clientTimeout);

        // Calling twice at the same time
        PingResponse[] responses = await Task.WhenAll(client.PingAsync(token), client.PingAsync(token));

        // Checks: both calls succeed on one start
        responses.ShouldAllBe(response => response.ServerTime == serverTime);
        probe.Negotiations.ShouldBe(1);
    }

    /// <summary>
    /// Canceling while the connection is starting ends the call with a cancellation and leaves the connection disconnected
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task CancelingDuringTheStartEndsTheCallAsync()
    {
        // Holding the negotiation of the first start
        CancellationToken token = TestContext.Current.CancellationToken;
        ConnectionProbe probe = new ConnectionProbe();
        probe.HoldNext();
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(DateTimeOffset.UtcNow));
        HubConnection connection = gateway.CreateHubConnection(PlatformHubContract.DefaultPath, probe);
        await using SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<PingResponse> call = client.PingAsync(cancel.Token);
        await probe.Held.WaitAsync(token);

        // Canceling while the start is held
        await cancel.CancelAsync();

        // Checks
        Exception error = await CaptureAsync(() => call);
        error.ShouldBeAssignableTo<OperationCanceledException>();
        connection.State.ShouldBe(HubConnectionState.Disconnected);
    }

    /// <summary>
    /// Canceling while the Gateway holds the call ends the call with a cancellation
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task CancelingDuringTheCallEndsTheCallAsync()
    {
        // Starting a call that the Gateway holds open
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeBackClient back = new FakeBackClient();
        await using GatewayHost gateway = new GatewayHost(back);
        await using SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath), _clientTimeout);
        using CancellationTokenSource cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<PingResponse> call = client.PingAsync(cancel.Token);
        await back.Entered.WaitAsync(token);

        // Canceling while the Back holds the call
        await cancel.CancelAsync();

        // Checks
        Exception error = await CaptureAsync(() => call);
        error.ShouldBeAssignableTo<OperationCanceledException>();
    }

    /// <summary>
    /// A Gateway that never answers makes the call end by cancellation when the timeout elapses
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ACallThatNeverReturnsEndsAtTheTimeoutAsync()
    {
        // Starting a call that the Gateway holds open, with a short timeout
        CancellationToken token = TestContext.Current.CancellationToken;
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient());
        await using SignalRGatewayClient client = new SignalRGatewayClient(
            gateway.CreateHubConnection(PlatformHubContract.DefaultPath),
            TimeSpan.FromMilliseconds(500));

        // Calling Ping without any cancellation from the caller
        Exception error = await CaptureAsync(() => client.PingAsync(token));

        // Checks
        error.ShouldBeAssignableTo<OperationCanceledException>();
        token.IsCancellationRequested.ShouldBeFalse();
    }

    /// <summary>
    /// Disposing the client while the Gateway holds a call ends that call; it never hangs
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingDuringACallEndsTheCallAsync()
    {
        // Starting a call that the Gateway holds open
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeBackClient back = new FakeBackClient();
        await using GatewayHost gateway = new GatewayHost(back);
        SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath), _clientTimeout);
        Task<PingResponse> call = client.PingAsync(token);
        await back.Entered.WaitAsync(token);

        // Disposing, as the end of the circuit does
        await client.DisposeAsync();

        // Checks: the call ended by cancellation (or refusal), it is not left hanging
        Exception error = await CaptureAsync(() => call);
        (error is OperationCanceledException || error is ObjectDisposedException).ShouldBeTrue(error.GetType().Name);
    }

    /// <summary>
    /// Disposing the client ends a call that is waiting for another call to finish starting the connection
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingEndsACallWaitingForTheStartAsync()
    {
        // Holding the start of the first call, then making a second call that waits for it
        CancellationToken token = TestContext.Current.CancellationToken;
        ConnectionProbe probe = new ConnectionProbe();
        probe.HoldNext();
        await using GatewayHost gateway = new GatewayHost(new FakeBackClient(DateTimeOffset.UtcNow));
        SignalRGatewayClient client = new SignalRGatewayClient(gateway.CreateHubConnection(PlatformHubContract.DefaultPath, probe), _clientTimeout);
        Task<PingResponse> first = client.PingAsync(token);
        await probe.Held.WaitAsync(token);
        Task<PingResponse> second = client.PingAsync(token);

        // Disposing
        await client.DisposeAsync();

        // Checks: both calls ended, none is left hanging
        Exception firstError = await CaptureAsync(() => first);
        Exception secondError = await CaptureAsync(() => second);
        firstError.ShouldBeAssignableTo<OperationCanceledException>();
        secondError.ShouldBeAssignableTo<OperationCanceledException>();
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
        SignalRGatewayClient client = new SignalRGatewayClient(connection, _clientTimeout);
        await client.PingAsync(token);
        connection.State.ShouldBe(HubConnectionState.Connected);

        // Disposing, as the end of the circuit does
        await client.DisposeAsync();

        // Checks: the connection is closed, further calls are refused, and disposing again is harmless
        connection.State.ShouldBe(HubConnectionState.Disconnected);
        Exception error = await CaptureAsync(() => client.PingAsync(token));
        error.ShouldBeOfType<ObjectDisposedException>();
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
