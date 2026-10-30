/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Services/VeryBackListenerServiceTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the listener of the Very Back on real loopback sockets
 */

// Using
using System.Net.Sockets;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Services;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests.Services;

/// <summary>
/// Tests used to check <see cref="VeryBackListenerService"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves the Ping operation end to end up to the procedure (a fake here), and the rules of the accept loop: a failing
/// connection never stops the others, and stopping the service closes the listener.</para>
/// <para><b>Behavior:</b> every service binds port 0 on the loopback address and the client connects to the port given by the system. Each test
/// is limited in time, so that a missing response fails the test instead of blocking the run; nothing waits for a fixed delay: the tests wait
/// for the response, or for the close of the connection.</para>
/// </remarks>
public sealed class VeryBackListenerServiceTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a network test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Address used by the clients to reach the services of the tests
    /// </summary>
    private const string LoopbackHost = "127.0.0.1";

    #endregion

    #region Variables

    /// <summary>
    /// Time answered by the fake procedure
    /// </summary>
    private static readonly DateTimeOffset _fakeTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));

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
    /// Method used to create a service listening on port 0 of the loopback address, and start it
    /// </summary>
    /// <param name="procedure">[Cannot be null] Procedure the service calls</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The started service
    /// </returns>
    private static async Task<VeryBackListenerService> StartServiceAsync(IPingProcedure procedure, CancellationToken cancellationToken)
    {
        // Creating and starting the service
        VeryBackOptions options = new VeryBackOptions { ListenAddress = LoopbackHost, Port = 0 };
        VeryBackListenerService service = new VeryBackListenerService(Options.Create(options), procedure, NullLogger<VeryBackListenerService>.Instance);
        await service.StartAsync(cancellationToken);

        // Function's return
        return service;
    }

    /// <summary>
    /// Method used to connect a request client to a started service
    /// </summary>
    /// <param name="service">[Cannot be null] Started service</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The client, owned by the caller, who disposes it
    /// </returns>
    private static async Task<RequestClient> ConnectAsync(VeryBackListenerService service, CancellationToken cancellationToken)
    {
        // Connecting
        ITransportConnection connection = await new TcpTransportConnector().ConnectAsync(LoopbackHost, service.LocalEndPoint.Port, cancellationToken);

        // Function's return
        return new RequestClient(connection, new FrameCodec());
    }

    #endregion

    #region Publics

    /// <summary>
    /// A Ping request is answered with the time given by the procedure
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task APingRequestIsAnsweredWithTheTimeOfTheProcedureAsync()
    {
        // Starting the service, and sending a Ping
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(_ => _fakeTime);
        VeryBackListenerService service = await StartServiceAsync(procedure, cancellationToken);
        try
        {
            await using RequestClient client = await ConnectAsync(service, cancellationToken);
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);

            // Checks
            response.ServerTime.ShouldBe(_fakeTime);
            response.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(1));
            procedure.Calls.ShouldBe(1);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A failing procedure ends the service of that connection: the request fails instead of hanging
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingProcedureEndsTheConnectionAndTheRequestFailsAsync()
    {
        // Starting the service with a procedure that fails
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(_ => throw new InvalidDataException("The database answered nonsense"));
        VeryBackListenerService service = await StartServiceAsync(procedure, cancellationToken);
        try
        {
            await using RequestClient client = await ConnectAsync(service, cancellationToken);

            // The request fails because the server closes the connection, and the client sees the close
            await Should.ThrowAsync<IOException>(() => client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken));
            await client.Completion;
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// After a connection failed, the service keeps accepting and serving new connections
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheServiceKeepsAcceptingAfterAConnectionFailedAsync()
    {
        // Starting the service with a procedure that fails on its first call only
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(call => call == 1 ? throw new InvalidDataException("First call fails") : _fakeTime);
        VeryBackListenerService service = await StartServiceAsync(procedure, cancellationToken);
        try
        {
            // The first connection fails
            await using (RequestClient failing = await ConnectAsync(service, cancellationToken))
            {
                await Should.ThrowAsync<IOException>(() => failing.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken));
            }

            // A new connection is accepted and answered
            await using RequestClient healthy = await ConnectAsync(service, cancellationToken);
            PingResponse response = await healthy.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);
            response.ServerTime.ShouldBe(_fakeTime);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A connection that breaks the protocol does not disturb another connection
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AConnectionBreakingTheProtocolDoesNotDisturbAnotherAsync()
    {
        // Starting the service, with a healthy client already connected
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService service = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        try
        {
            await using RequestClient healthy = await ConnectAsync(service, cancellationToken);

            // A second client sends a request for an operation that does not exist: its connection is closed
            await using (RequestClient breaking = await ConnectAsync(service, cancellationToken))
            {
                await Should.ThrowAsync<IOException>(() => breaking.SendAsync<PingRequest, PingResponse>("Unknown", new PingRequest(), cancellationToken));
            }

            // The healthy client is still answered
            PingResponse response = await healthy.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);
            response.ServerTime.ShouldBe(_fakeTime);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Stopping the service disposes the listener and closes the open connections
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task StoppingTheServiceDisposesTheListenerAndClosesTheConnectionsAsync()
    {
        // Starting the service, with a client connected and served
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService service = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        int port = service.LocalEndPoint.Port;
        await using RequestClient client = await ConnectAsync(service, cancellationToken);
        await client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);

        // Stopping the service: it waits for the service of the open connection, which ends
        await service.StopAsync(cancellationToken);

        // Checks: the server closed the connection, and nothing listens on the port any more
        await client.Completion;
        await Should.ThrowAsync<SocketException>(() => new TcpTransportConnector().ConnectAsync(LoopbackHost, port, cancellationToken));
    }

    /// <summary>
    /// A service whose port is already used refuses to start, so that the host does not start
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AUsedPortStopsTheStartAsync()
    {
        // Starting a first service, then a second one on the same port
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService first = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        try
        {
            VeryBackOptions options = new VeryBackOptions { ListenAddress = LoopbackHost, Port = first.LocalEndPoint.Port };
            VeryBackListenerService second = new VeryBackListenerService(Options.Create(options), new FakePingProcedure(_ => _fakeTime), NullLogger<VeryBackListenerService>.Instance);

            // Check
            await Should.ThrowAsync<SocketException>(() => second.StartAsync(cancellationToken));
        }
        finally
        {
            await first.StopAsync(CancellationToken.None);
        }
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
