/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkServerTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the server side of a link, independently of the tier that uses it
 */

// Using
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check <see cref="LinkServer"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the rules of the accept loop hold for any tier, under the name it gives: the tests use "the Back", a tier other than
/// the one the loop was first written for.</para>
/// <para><b>Behavior:</b> the failures of the listener are simulated by a fake listener; the round trip uses a real listener on a fixed test
/// port of the loopback address. Each test is limited in time and waits for events, never for a fixed delay.</para>
/// </remarks>
public sealed class LinkServerTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Name of the tier given to the server in the tests
    /// </summary>
    private const string TierName = "the Back";

    #endregion

    #region Variables

    /// <summary>
    /// Time answered by the Ping operation of the tests
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(1));

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
    /// Method used to build a request server that answers the Ping operation of the Back
    /// </summary>
    /// <returns>
    /// The request server
    /// </returns>
    private static RequestServer CreatePingServer()
    {
        // Routing the Ping operation
        OperationDispatcher dispatcher = new OperationDispatcher().Register<PingRequest, PingResponse>(BackOperations.Ping,
            (_, _) => Task.FromResult(new PingResponse(_pingTime)));

        // Function's return
        return new RequestServer(new FrameCodec(), dispatcher.HandleAsync);
    }

    /// <summary>
    /// Method used to build a server on a fake listener, with no pause after an accept failure
    /// </summary>
    /// <param name="listener">[Cannot be null] Fake listener</param>
    /// <param name="logger">[Cannot be null] Logger of the server</param>
    /// <returns>
    /// The server, not running
    /// </returns>
    private static LinkServer CreateServerOn(FailingTransportListener listener, CapturingLogger<LinkServerTests> logger)
    {
        // Function's return
        return new LinkServer(listener, CreatePingServer(), TierName, logger, TimeSpan.Zero);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A request sent to a running server is answered, and stopping the server closes its listener
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestIsAnsweredAndTheStopClosesTheListenerAsync()
    {
        // Running a server on a real listener, and sending a Ping
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        int port = listener.LocalEndPoint.Port;
        CapturingLogger<LinkServerTests> logger = new CapturingLogger<LinkServerTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = new LinkServer(listener, CreatePingServer(), TierName, logger, LinkServer.DefaultAcceptFailurePause).RunAsync(stopping.Token);
        await using (RequestClient client = new RequestClient(await new TcpTransportConnector().ConnectAsync("127.0.0.1", port, cancellationToken), new FrameCodec()))
        {
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken);
            response.ServerTime.ShouldBe(_pingTime);
        }

        // Stopping the server
        await stopping.CancelAsync();
        await running.WaitAsync(cancellationToken);

        // Checks: the stop is logged under the name of the tier, and nothing listens on the port any more
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message == "Listener of the Back stopped");
        await Should.ThrowAsync<SocketException>(() => new TcpTransportConnector().ConnectAsync("127.0.0.1", port, cancellationToken));
    }

    /// <summary>
    /// A listener lost without a stop request fails the run, under the name of the tier, and the listener is disposed
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AListenerLostWithoutAStopRequestFailsTheRunAsync()
    {
        // Running a server on a listener that is disposed under it
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => throw new ObjectDisposedException("listener"));
        CapturingLogger<LinkServerTests> logger = new CapturingLogger<LinkServerTests>();

        // Checks
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateServerOn(listener, logger).RunAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain(TierName);
        exception.InnerException.ShouldBeOfType<ObjectDisposedException>();
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Critical && entry.Message == "The listener of the Back was closed without a stop request; the Back stops");
        listener.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// Accept failures that repeat fail the run after the maximum number in a row, under the name of the tier
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task RepeatedAcceptFailuresFailTheRunAsync()
    {
        // Running a server on a listener whose accepts always fail
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => throw new SocketException((int)SocketError.TooManyOpenSockets));
        CapturingLogger<LinkServerTests> logger = new CapturingLogger<LinkServerTests>();

        // Checks
        InvalidOperationException exception = await Should.ThrowAsync<InvalidOperationException>(
            () => CreateServerOn(listener, logger).RunAsync(TestContext.Current.CancellationToken));
        exception.Message.ShouldContain(TierName);
        listener.Accepts.ShouldBe(LinkServer.MaxConsecutiveAcceptFailures);
        logger.Entries.Count(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("could not be accepted")).ShouldBe(LinkServer.MaxConsecutiveAcceptFailures - 1);
        logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Critical && entry.Message.Contains("the listener of the Back is broken; the Back stops"));
    }

    /// <summary>
    /// A server runs once only: a second run is refused
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ASecondRunIsRefusedAsync()
    {
        // Running then stopping a server on an idle listener
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => false);
        LinkServer server = CreateServerOn(listener, new CapturingLogger<LinkServerTests>());
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = server.RunAsync(stopping.Token);
        await listener.Waiting.WaitAsync(cancellationToken);
        await stopping.CancelAsync();
        await running.WaitAsync(cancellationToken);

        // Check
        await Should.ThrowAsync<InvalidOperationException>(() => server.RunAsync(cancellationToken));
    }

    /// <summary>
    /// The constructor refuses a missing dependency, a blank name and a negative pause
    /// </summary>
    [Fact]
    public void TheConstructorChecksItsParameters()
    {
        // Preparing valid parameters
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => false);
        RequestServer server = CreatePingServer();
        ILogger logger = NullLogger.Instance;

        // Checks
        Should.Throw<ArgumentNullException>(() => new LinkServer(null!, server, TierName, logger, TimeSpan.Zero));
        Should.Throw<ArgumentNullException>(() => new LinkServer(listener, null!, TierName, logger, TimeSpan.Zero));
        Should.Throw<ArgumentNullException>(() => new LinkServer(listener, server, null!, logger, TimeSpan.Zero));
        Should.Throw<ArgumentException>(() => new LinkServer(listener, server, "  ", logger, TimeSpan.Zero));
        Should.Throw<ArgumentNullException>(() => new LinkServer(listener, server, TierName, null!, TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServer(listener, server, TierName, logger, TimeSpan.FromMilliseconds(-1)));
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
