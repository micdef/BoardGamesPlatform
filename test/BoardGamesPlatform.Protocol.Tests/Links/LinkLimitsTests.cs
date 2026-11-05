/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkLimitsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the limits of the links: the request timeout of the client and the maximum of connections of the server
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Control;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
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
/// Tests used to check the limits of the link client and of the link server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a peer that never answers must not hold a caller forever, and a peer that opens connections without end must not
/// make a tier hold them all.</para>
/// <para><b>Behavior:</b> each test runs a link server on a test port of the block of this project, on the loopback address.</para>
/// </remarks>
public sealed class LinkLimitsTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 10000;

    /// <summary>
    /// Name of the peer in the logs
    /// </summary>
    private const string PeerName = "the Back";

    #endregion

    #region Variables

    /// <summary>
    /// Time the Ping of the tests answers
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.FromHours(1));

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
    /// Method used to start a link server on a test port, with a Ping handler and a maximum of connections
    /// </summary>
    /// <param name="handler">[Cannot be null] Handler of the Ping operation</param>
    /// <param name="maxConnections">Maximum of connections served at the same time</param>
    /// <param name="logger">[Cannot be null] Logger of the server</param>
    /// <param name="stoppingToken">Token that stops the server</param>
    /// <param name="port">Test port the server listens on</param>
    /// <returns>
    /// The task of the running server
    /// </returns>
    private static Task StartServer(Func<PingRequest, CancellationToken, Task<PingResponse>> handler, int maxConnections, ILogger logger,
        CancellationToken stoppingToken, out int port)
    {
        // Starting the listener and the server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register(BackOperations.Ping, handler);
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), PeerName, logger, TimeSpan.Zero,
            ProtocolHandshake.DefaultServerTimeout, maxConnections);

        // Function's return
        return server.RunAsync(stoppingToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// A request that gets no answer within the request timeout fails with the code "link.timeout", and its work stops on the peer
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ARequestWithoutAnswerTimesOutAndIsCanceledOnThePeerAsync()
    {
        // Starting a server whose handler waits until its request is canceled
        CancellationToken token = TestContext.Current.CancellationToken;
        TaskCompletionSource canceled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<PingResponse> WaitingAsync(PingRequest request, CancellationToken requestToken)
        {
            try
            {
                await Task.Delay(Timeout.Infinite, requestToken);
            }
            catch (OperationCanceledException)
            {
                canceled.TrySetResult();
                throw;
            }
            return new PingResponse(_pingTime);
        }
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer(WaitingAsync, LinkServer.DefaultMaxConnections, NullLogger.Instance, stopping.Token, out int port);
        try
        {
            // Sending a Ping with a short request timeout
            await using LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), TimeSpan.FromMilliseconds(300), PeerName,
                new TcpTransportConnector(), NullLogger.Instance);
            LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(
                () => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));

            // Checks
            failure.Code.ShouldBe(ErrorCodes.LinkTimeout);
            await canceled.Task.WaitAsync(token);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// The request timeout must be greater than zero and at most its maximum
    /// </summary>
    [Fact]
    public void TheRequestTimeoutIsChecked()
    {
        // Checks
        TcpTransportConnector connector = new TcpTransportConnector();
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient("127.0.0.1", 7821, TimeSpan.FromSeconds(5), TimeSpan.Zero, PeerName, connector, NullLogger.Instance));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkClient("127.0.0.1", 7821, TimeSpan.FromSeconds(5), LinkClient.MaxRequestTimeout + TimeSpan.FromTicks(1),
            PeerName, connector, NullLogger.Instance));
    }

    /// <summary>
    /// Beyond its maximum of connections, the server closes a new connection at once and logs it, while the connections it serves go on
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AConnectionBeyondTheMaximumIsRefusedAsync()
    {
        // Starting a server that serves one connection at most, and opening that connection
        CancellationToken token = TestContext.Current.CancellationToken;
        CapturingLogger<LinkLimitsTests> logger = new CapturingLogger<LinkLimitsTests>();
        using CancellationTokenSource stopping = new CancellationTokenSource();
        Task running = StartServer((request, requestToken) => Task.FromResult(new PingResponse(_pingTime)), 1, logger, stopping.Token, out int port);
        try
        {
            await using RequestClient first = await TestRequestClients.ConnectAsync("127.0.0.1", port, token);

            // Opening a second connection: it is closed before any byte
            await using ITransportConnection second = await new TcpTransportConnector().ConnectAsync("127.0.0.1", port, token);
            Frame? nothing = await new FrameCodec().ReadAsync(second.Stream, token);

            // Checks
            nothing.ShouldBeNull();
            logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("refused: 1 connections are already served"));
            (await first.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token)).ServerTime.ShouldBe(_pingTime);
        }
        finally
        {
            await stopping.CancelAsync();
            await running;
        }
    }

    /// <summary>
    /// The maximum of connections must be one or more
    /// </summary>
    [Fact]
    public void TheMaximumOfConnectionsIsChecked()
    {
        // Check
        FailingTransportListener listener = new FailingTransportListener(new IPEndPoint(IPAddress.Loopback, 7821), _ => false);
        RequestServer server = new RequestServer(new FrameCodec(), (request, requestToken) => Task.FromResult(request));
        Should.Throw<ArgumentOutOfRangeException>(() => new LinkServer(listener, server, PeerName, NullLogger.Instance, TimeSpan.Zero,
            ProtocolHandshake.DefaultServerTimeout, 0));
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
