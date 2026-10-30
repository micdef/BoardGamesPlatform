/*
 *	@file	test/BoardGamesPlatform.Platform.Infrastructure.Tests/VeryBack/VeryBackClientTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the Very Back client against a fake Very Back on a loopback socket
 */

// Using
using System.Net.Sockets;
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts.Ping;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.Tests.VeryBack;

/// <summary>
/// Tests used to check the Very Back client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the Back relies on one shared connection that opens on demand, comes back after a loss and never retries by itself.</para>
/// <para><b>Behavior:</b> the tests run on a real loopback socket (port 0 only). They wait for events (a connection lost, a connection attempt
/// begun), never for a fixed delay; every test has a timeout, so that a hang fails instead of blocking the run.</para>
/// </remarks>
public sealed class VeryBackClientTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a network test, in milliseconds
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
    /// Method used to build a client for a port, with the real TCP connector
    /// </summary>
    /// <param name="port">Port of the Very Back</param>
    /// <param name="logger">[Cannot be null] Logger given to the client</param>
    /// <returns>
    /// The client
    /// </returns>
    private static VeryBackClient CreateClient(int port, SignalingLogger<VeryBackClient> logger)
    {
        // Function's return
        return new VeryBackClient(new VeryBackClientOptions { Port = port }, new TcpTransportConnector(), logger);
    }

    #endregion

    #region Publics

    #region Ping

    /// <summary>
    /// Ping returns the time answered by the Very Back
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingReturnsTheTimeOfTheVeryBackAsync()
    {
        // Pinging the fake Very Back
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        await using VeryBackClient client = CreateClient(fake.Port, new SignalingLogger<VeryBackClient>());
        PingResponse response = await client.PingAsync(token);

        // Check
        response.ServerTime.ShouldBe(FakeVeryBack.PingTime);
    }

    /// <summary>
    /// The connection opens on the first call, not when the client is built
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheConnectionOpensOnFirstUseAsync()
    {
        // Building the client, which must not connect
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        SignalingLogger<VeryBackClient> logger = new SignalingLogger<VeryBackClient>();
        await using VeryBackClient client = CreateClient(fake.Port, logger);
        fake.AcceptedConnections.ShouldBe(0);

        // First call
        await client.PingAsync(token);

        // Checks (the opening is logged, with the address only)
        fake.AcceptedConnections.ShouldBe(1);
        logger.Messages.ShouldContain(message => message.Contains("opened") && message.Contains("127.0.0.1:" + fake.Port));
    }

    /// <summary>
    /// Concurrent calls share one connection
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ConcurrentCallsShareOneConnectionAsync()
    {
        // Sending many pings at the same time
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        await using VeryBackClient client = CreateClient(fake.Port, new SignalingLogger<VeryBackClient>());
        PingResponse[] responses = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => client.PingAsync(token)));

        // Checks (the fake counts a connection when it accepts it, before it can answer)
        responses.ShouldAllBe(response => response.ServerTime == FakeVeryBack.PingTime);
        fake.AcceptedConnections.ShouldBe(1);
        fake.ReceivedRequests.ShouldBe(20);
    }

    /// <summary>
    /// Later calls reuse the connection that is still open
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task LaterCallsReuseTheOpenConnectionAsync()
    {
        // Pinging twice in a row
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        await using VeryBackClient client = CreateClient(fake.Port, new SignalingLogger<VeryBackClient>());
        await client.PingAsync(token);
        await client.PingAsync(token);

        // Check
        fake.AcceptedConnections.ShouldBe(1);
    }

    #endregion

    #region Connection lost

    /// <summary>
    /// When the Very Back closes the connection, the next call opens a new one and succeeds
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheNextCallReconnectsAfterTheVeryBackClosesTheConnectionAsync()
    {
        // Opening the connection, then dropping it on the server side
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        SignalingLogger<VeryBackClient> logger = new SignalingLogger<VeryBackClient>();
        await using VeryBackClient client = CreateClient(fake.Port, logger);
        await client.PingAsync(token);
        await fake.DropConnectionsAsync();

        // Waiting until the client has noticed the loss (it logs it), without any delay
        await logger.WaitForAsync("lost").WaitAsync(token);

        // Pinging again
        PingResponse response = await client.PingAsync(token);

        // Checks
        response.ServerTime.ShouldBe(FakeVeryBack.PingTime);
        fake.AcceptedConnections.ShouldBe(2);
    }

    /// <summary>
    /// A request that fails is not retried by the client: the caller sees the error, and no new connection opens
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailedRequestIsNotRetriedAsync()
    {
        // Making the Very Back fail on the request
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        fake.FailRequests();
        await using VeryBackClient client = CreateClient(fake.Port, new SignalingLogger<VeryBackClient>());

        // Pinging: the failing handler ends the service of the connection, so the connection closes before any response
        await Should.ThrowAsync<IOException>(() => client.PingAsync(token));

        // Checks
        fake.ReceivedRequests.ShouldBe(1);
        fake.AcceptedConnections.ShouldBe(1);
    }

    #endregion

    #region Connecting

    /// <summary>
    /// When nothing listens, the call fails with a socket error instead of hanging
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the port comes from a listener on port 0 that is stopped just before the call, so that it is a port that was
    /// free a moment ago and has no listener; nothing is reserved in advance.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task APingWhenNothingListensFailsWithASocketErrorAsync()
    {
        // Taking the port of a Very Back that stops at once
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeVeryBack fake = FakeVeryBack.Start();
        int port = fake.Port;
        await fake.DisposeAsync();
        await using VeryBackClient client = CreateClient(port, new SignalingLogger<VeryBackClient>());

        // Check
        await Should.ThrowAsync<SocketException>(() => client.PingAsync(token));
    }

    /// <summary>
    /// A connection that never completes fails with a timeout after the connect timeout
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> a peer that never completes the TCP handshake cannot be built on loopback (the system completes it for any
    /// listening port), so the connector is replaced by one that waits for its token: this proves that the client gives the deadline to the
    /// connector and turns its expiry into a <see cref="TimeoutException"/>.</para>
    /// </remarks>
    [Fact(Timeout = TestTimeout)]
    public async Task TheConnectTimeoutIsHonouredAsync()
    {
        // Pinging through a connector that never connects, with a short timeout
        CancellationToken token = TestContext.Current.CancellationToken;
        VeryBackClientOptions options = new VeryBackClientOptions { ConnectTimeout = TimeSpan.FromMilliseconds(200) };
        await using VeryBackClient client = new VeryBackClient(options, new HangingConnector(), new SignalingLogger<VeryBackClient>());

        // Check
        await Should.ThrowAsync<TimeoutException>(() => client.PingAsync(token));
    }

    /// <summary>
    /// A caller who cancels while the connection opens gets a cancellation, not a timeout
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheCancellationOfTheCallerStopsTheConnectionAsync()
    {
        // Starting a ping through a connector that never connects, with a long timeout
        using CancellationTokenSource canceling = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        HangingConnector connector = new HangingConnector();
        VeryBackClientOptions options = new VeryBackClientOptions { ConnectTimeout = TimeSpan.FromMinutes(1) };
        await using VeryBackClient client = new VeryBackClient(options, connector, new SignalingLogger<VeryBackClient>());
        Task<PingResponse> ping = client.PingAsync(canceling.Token);

        // Canceling once the attempt has begun
        await connector.Started.WaitAsync(TestContext.Current.CancellationToken);
        await canceling.CancelAsync();

        // Check (a canceled wait ends with the cancellation exception of the task, whatever its exact type)
        Exception exception = await Should.ThrowAsync<Exception>(() => ping);
        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }

    #endregion

    #region Disposal

    /// <summary>
    /// A call after the disposal throws an ObjectDisposedException, whether or not a connection was open
    /// </summary>
    /// <param name="connectFirst">Whether the client opens its connection before it is disposed</param>
    [Theory(Timeout = TestTimeout)]
    [InlineData(false)]
    [InlineData(true)]
    public async Task APingAfterDisposalThrowsAsync(bool connectFirst)
    {
        // Using then disposing the client
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        VeryBackClient client = CreateClient(fake.Port, new SignalingLogger<VeryBackClient>());
        if (connectFirst)
            await client.PingAsync(token);
        await client.DisposeAsync();

        // Checks (disposing twice is harmless)
        await Should.ThrowAsync<ObjectDisposedException>(() => client.PingAsync(token));
        await client.DisposeAsync();
    }

    /// <summary>
    /// Disposing the client closes its connection, and it is not reported as a loss
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task DisposingTheClientClosesTheConnectionWithoutReportingALossAsync()
    {
        // Opening the connection, then disposing the client
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeVeryBack fake = FakeVeryBack.Start();
        SignalingLogger<VeryBackClient> logger = new SignalingLogger<VeryBackClient>();
        VeryBackClient client = CreateClient(fake.Port, logger);
        await client.PingAsync(token);
        await client.DisposeAsync();

        // Check (the fake finishes serving the connection once the client has closed it; disposing the fake waits for that)
        await fake.DisposeAsync();
        logger.Messages.ShouldNotContain(message => message.Contains("lost"));
    }

    #endregion

    #region Construction

    /// <summary>
    /// The constructor refuses a missing dependency and invalid settings
    /// </summary>
    [Fact]
    public void TheConstructorChecksItsParameters()
    {
        // Checks
        SignalingLogger<VeryBackClient> logger = new SignalingLogger<VeryBackClient>();
        Should.Throw<ArgumentNullException>(() => new VeryBackClient(null!, new TcpTransportConnector(), logger));
        Should.Throw<ArgumentNullException>(() => new VeryBackClient(new VeryBackClientOptions(), null!, logger));
        Should.Throw<ArgumentNullException>(() => new VeryBackClient(new VeryBackClientOptions(), new TcpTransportConnector(), null!));
        Should.Throw<InvalidOperationException>(() => new VeryBackClient(new VeryBackClientOptions { Port = 0 }, new TcpTransportConnector(), logger));
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
