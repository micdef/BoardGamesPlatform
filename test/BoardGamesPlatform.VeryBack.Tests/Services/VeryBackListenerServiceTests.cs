/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/Services/VeryBackListenerServiceTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the listener of the Very Back on real loopback sockets
 */

// Using
using System.Net;
using System.Net.Sockets;
using System.Text;
using BoardGamesPlatform.Observability;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using BoardGamesPlatform.VeryBack.Procedures;
using BoardGamesPlatform.VeryBack.Services;
using BoardGamesPlatform.VeryBack.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
/// <para><b>Behavior:</b> every service binds a fixed test port of the block of this project (see <see cref="TestPorts"/>) on the loopback
/// address. Each test is limited in time, so that a missing response fails the test instead of blocking the run; nothing waits for a fixed
/// delay: the tests wait for the response, or for the close of the connection.</para>
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
    /// Method used to create a service listening on a test port of the loopback address, and start it
    /// </summary>
    /// <param name="procedure">[Cannot be null] Procedure the service calls</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The started service
    /// </returns>
    private static async Task<VeryBackListenerService> StartServiceAsync(IPingProcedure procedure, CancellationToken cancellationToken)
    {
        // Creating and starting the service
        VeryBackOptions options = new VeryBackOptions { ListenAddress = LoopbackHost, Port = TestPorts.Block.Next() };
        VeryBackListenerService service = new VeryBackListenerService(Options.Create(options), procedure, NullLogger<VeryBackListenerService>.Instance,
            endPoint => TcpTransportListener.Start(endPoint), LinkServer.DefaultAcceptFailurePause);
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
        // Function's return: connected and agreed on the protocol, like the Back
        return await TestRequestClients.ConnectAsync(LoopbackHost, service.LocalEndPoint.Port, new TcpTransportConnector(), VeryBackOperations.LinkMaxPayloadLength,
            cancellationToken);
    }

    /// <summary>
    /// Method used to create a service on a fake listener, with no pause after an accept failure
    /// </summary>
    /// <param name="listener">[Cannot be null] Listener the service starts</param>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <returns>
    /// The service, not started
    /// </returns>
    private static VeryBackListenerService CreateServiceOn(FailingTransportListener listener, CapturingLogger<VeryBackListenerService> logger)
    {
        // Function's return
        return new VeryBackListenerService(Options.Create(new VeryBackOptions()), new FakePingProcedure(_ => _fakeTime), logger, _ => listener,
            TimeSpan.Zero);
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
    /// A failing procedure is answered with the code "internal", without its message, and the connection stays open for the next requests
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingProcedureIsAnsweredInternalAndTheConnectionStaysOpenAsync()
    {
        // Starting the service with a procedure that fails on its first call only
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(call => call == 1 ? throw new InvalidDataException("The database answered nonsense") : _fakeTime);
        VeryBackListenerService service = await StartServiceAsync(procedure, cancellationToken);
        try
        {
            await using RequestClient client = await ConnectAsync(service, cancellationToken);

            // The first request is answered with the code only
            RemoteErrorException error = await Should.ThrowAsync<RemoteErrorException>(
                () => client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken));
            error.Code.ShouldBe(ErrorCodes.Internal);
            error.Message.ShouldNotContain("nonsense");

            // The same connection answers the next request
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);
            response.ServerTime.ShouldBe(_fakeTime);
            client.Completion.IsCompleted.ShouldBeFalse();
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
        // Starting the service
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService service = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        try
        {
            // The first connection fails: its request is not even a JSON envelope, and the server closes it
            await using (RequestClient failing = await ConnectAsync(service, cancellationToken))
            {
                await Should.ThrowAsync<IOException>(() => failing.SendAsync(Encoding.UTF8.GetBytes("not json"), cancellationToken));
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

            // A second client sends a request that is not a JSON envelope: its connection is closed
            await using (RequestClient breaking = await ConnectAsync(service, cancellationToken))
            {
                await Should.ThrowAsync<IOException>(() => breaking.SendAsync(Encoding.UTF8.GetBytes("not json"), cancellationToken));
            }

            // An operation that does not exist is answered with its code, on a connection that stays open
            RemoteErrorException unknown = await Should.ThrowAsync<RemoteErrorException>(
                () => healthy.SendAsync<PingRequest, PingResponse>("Unknown", new PingRequest(), cancellationToken));
            unknown.Code.ShouldBe(ErrorCodes.RequestUnknownOperation);

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
            using VeryBackListenerService second = new VeryBackListenerService(Options.Create(options), new FakePingProcedure(_ => _fakeTime), NullLogger<VeryBackListenerService>.Instance,
                endPoint => TcpTransportListener.Start(endPoint), LinkServer.DefaultAcceptFailurePause);

            // Check
            await Should.ThrowAsync<SocketException>(() => second.StartAsync(cancellationToken));
        }
        finally
        {
            await first.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// A service started twice refuses the second start
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AServiceStartedTwiceRefusesTheSecondStartAsync()
    {
        // Starting the service once
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService service = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        try
        {
            // Check
            await Should.ThrowAsync<InvalidOperationException>(() => service.StartAsync(cancellationToken));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    /// <summary>
    /// Stopping the service during a request in progress cancels the call of the procedure: the stop completes and the client sees the close
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task StoppingDuringARequestCancelsTheCallOfTheProcedureAsync()
    {
        // Starting the service with a procedure that waits for its token, and sending a Ping that stays in progress
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        WaitingPingProcedure procedure = new WaitingPingProcedure();
        VeryBackListenerService service = await StartServiceAsync(procedure, cancellationToken);
        await using RequestClient client = await ConnectAsync(service, cancellationToken);
        Task<PingResponse> ping = client.SendAsync<PingRequest, PingResponse>(VeryBackOperations.Ping, new PingRequest(), cancellationToken);
        await procedure.Started.WaitAsync(cancellationToken);

        // Stopping the service (the test timeout fails it if the stop waits for the procedure)
        await service.StopAsync(cancellationToken);

        // Checks: the request in progress ends with the close of the connection
        await Should.ThrowAsync<IOException>(() => ping);
        await client.Completion;
    }

    /// <summary>
    /// A listener lost without a stop request fails the service, so that the host stops instead of running without listening
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AListenerLostWithoutAStopRequestFailsTheServiceAsync()
    {
        // Starting the service on a listener that is disposed under it
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7822),
            _ => throw new ObjectDisposedException("listener"));
        CapturingLogger<VeryBackListenerService> logger = new CapturingLogger<VeryBackListenerService>();
        using VeryBackListenerService service = CreateServiceOn(listener, logger);
        await service.StartAsync(cancellationToken);

        // Checks: the service fails, logs it as critical, and still disposes the listener
        Task execution = service.ExecuteTask.ShouldNotBeNull();
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => execution.WaitAsync(cancellationToken));
        exception.InnerException.ShouldBeOfType<ObjectDisposedException>();
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Critical && entry.Message.Contains("without a stop request"));
        listener.Disposals.ShouldBeGreaterThan(0);
        HostExitCode.FromHostedServices(new IHostedService[] { service }).ShouldBe(HostExitCode.Failure);
    }

    /// <summary>
    /// An accept failure is logged and the loop keeps accepting
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AnAcceptFailureIsLoggedAndTheLoopKeepsAcceptingAsync()
    {
        // Starting the service on a listener whose first accept fails, then waits
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7822),
            call => call == 1 ? throw new SocketException((int)SocketError.ConnectionReset) : false);
        CapturingLogger<VeryBackListenerService> logger = new CapturingLogger<VeryBackListenerService>();
        using VeryBackListenerService service = CreateServiceOn(listener, logger);
        await service.StartAsync(cancellationToken);

        // Waiting for the second accept
        await listener.Waiting.WaitAsync(cancellationToken);

        // Checks: one warning, the loop runs, and a stop ends it normally
        logger.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not be accepted")).ShouldBe(1);
        listener.Accepts.ShouldBe(2);
        service.ExecuteTask.ShouldNotBeNull().IsCompleted.ShouldBeFalse();
        await service.StopAsync(cancellationToken);
        service.ExecuteTask.ShouldNotBeNull().IsCompletedSuccessfully.ShouldBeTrue();
        HostExitCode.FromHostedServices(new IHostedService[] { service }).ShouldBe(HostExitCode.Success);
    }

    /// <summary>
    /// Accept failures that repeat fail the service after the maximum number in a row
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task RepeatedAcceptFailuresFailTheServiceAsync()
    {
        // Starting the service on a listener whose accepts always fail
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7822),
            _ => throw new SocketException((int)SocketError.TooManyOpenSockets));
        CapturingLogger<VeryBackListenerService> logger = new CapturingLogger<VeryBackListenerService>();
        using VeryBackListenerService service = CreateServiceOn(listener, logger);
        await service.StartAsync(cancellationToken);

        // Checks: the service fails after exactly the maximum number of accepts, with a critical log
        Task execution = service.ExecuteTask.ShouldNotBeNull();
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(() => execution.WaitAsync(cancellationToken));
        exception.InnerException.ShouldBeOfType<SocketException>();
        listener.Accepts.ShouldBe(VeryBackListenerService.MaxConsecutiveAcceptFailures);
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Critical && entry.Message.Contains("accept failures in a row"));
    }

    /// <summary>
    /// A listen address that other machines can reach is accepted, and logged; the loopback address is not
    /// </summary>
    /// <param name="address">Address the listener claims to listen on</param>
    /// <param name="warned">Whether the log is expected</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory(Timeout = TestTimeout)]
    [InlineData("0.0.0.0", true)]
    [InlineData("192.168.1.10", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("::1", false)]
    public async Task AnAddressOtherThanLoopbackIsLoggedAsync(string address, bool warned)
    {
        // Starting the service on an idle listener with that address
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Parse(address), 7822), _ => false);
        CapturingLogger<VeryBackListenerService> logger = new CapturingLogger<VeryBackListenerService>();
        using VeryBackListenerService service = CreateServiceOn(listener, logger);
        await service.StartAsync(cancellationToken);
        await service.StopAsync(cancellationToken);

        // Check
        logger.Entries.Any(entry => entry.Level == LogLevel.Information && entry.Message.Contains("not a loopback address")).ShouldBe(warned);
    }

    /// <summary>
    /// The link has a largest payload of its own (M3 of the security review of 0.4.0): a client that offers the 1 MiB of the protocol is
    /// refused in the handshake, and a frame larger than the link allows closes the connection before its payload is held
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task TheLinkHasALargestPayloadOfItsOwnAsync()
    {
        // Starting the service
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        VeryBackListenerService service = await StartServiceAsync(new FakePingProcedure(_ => _fakeTime), cancellationToken);
        try
        {
            // Check: a hello with the largest payload of the protocol is refused, the code telling why
            RemoteErrorException refused = await Should.ThrowAsync<RemoteErrorException>(() => TestRequestClients.ConnectAsync(LoopbackHost, service.LocalEndPoint.Port,
                cancellationToken));
            refused.Code.ShouldBe(ErrorCodes.ProtocolLimitsMismatch);

            // A client of the link announces a frame one byte larger than the link allows (the server may close before the whole frame is written)
            await using ITransportConnection connection = await new TcpTransportConnector().ConnectAsync(LoopbackHost, service.LocalEndPoint.Port, cancellationToken);
            FrameCodec codec = new FrameCodec(VeryBackOperations.LinkMaxPayloadLength);
            await ProtocolHandshake.RunClientAsync(connection, codec, cancellationToken);
            Exception? writeFailure = await Record.ExceptionAsync(() => new FrameCodec().WriteAsync(connection.Stream, new Frame(1, new byte[VeryBackOperations.LinkMaxPayloadLength + 1]),
                cancellationToken));
            Frame? answer = null;
            Exception? readFailure = await Record.ExceptionAsync(async () => answer = await codec.ReadAsync(connection.Stream, cancellationToken));

            // Checks: the connection is closed without an answer
            (writeFailure is null or IOException).ShouldBeTrue(writeFailure?.ToString());
            (readFailure is null or IOException).ShouldBeTrue(readFailure?.ToString());
            answer.ShouldBeNull();
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
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
