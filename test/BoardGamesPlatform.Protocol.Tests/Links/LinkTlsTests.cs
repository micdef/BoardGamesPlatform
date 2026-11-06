/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/LinkTlsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the links over mutual TLS: a call goes through, a refused peer is a code, and a silent peer never holds the accept loop
 */

// Using
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check <see cref="LinkServer"/> and <see cref="LinkClient"/> over mutual TLS
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> the server is the Back, the client the Gateway, both with certificates built at run time, on a test port of the
/// loopback address.</para>
/// </remarks>
public sealed class LinkTlsTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 15000;

    #endregion

    #region Variables

    /// <summary>
    /// Authority of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// Time answered by the Ping operation of the tests
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.FromHours(1));

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
    /// Method used to start the Back over TLS on a test port, answering the Ping operation
    /// </summary>
    /// <param name="handshakeTimeout">Time allowed to a client for its handshake</param>
    /// <param name="stopping">[Cannot be null] Token that stops the server</param>
    /// <param name="running">The task of the server</param>
    /// <returns>
    /// The port of the server
    /// </returns>
    private int StartBack(TimeSpan handshakeTimeout, CancellationToken stopping, out Task running)
    {
        // Listening over TLS, then running the link server
        TlsTransportListener listener = new TlsTransportListener(TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next())),
            _certificates.CredentialsFor(PlatformRole.Back), PlatformRole.Gateway, NullLogger.Instance);
        OperationDispatcher dispatcher = new OperationDispatcher(NullLogger.Instance).Register<PingRequest, PingResponse>(BackOperations.Ping,
            (_, _) => Task.FromResult(new PingResponse(_pingTime)));
        LinkServer server = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), "the Back", NullLogger.Instance, TimeSpan.Zero, handshakeTimeout,
            LinkServer.DefaultMaxConnections);
        running = server.RunAsync(stopping);

        // Function's return
        return listener.LocalEndPoint.Port;
    }

    #endregion

    #region Publics

    /// <summary>
    /// A call of the Gateway reaches the Back over mutual TLS
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ACallGoesThroughMutualTlsAsync()
    {
        // Running the Back, and calling it as the Gateway
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running);
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.Back, NullLogger.Instance);
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the Back", connector, NullLogger.Instance))
        {
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token);
            response.ServerTime.ShouldBe(_pingTime);
        }

        // Stopping the Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A client the Back refuses gets the code "link.unavailable", never a TLS exception
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ARefusedClientGetsACodeAsync()
    {
        // Running the Back, and calling it with the credentials of the Very Back, which is not its client
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartBack(TimeSpan.FromSeconds(5), stopping.Token, out Task running);
        TlsCredentials back = _certificates.CredentialsFor(PlatformRole.Back);
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), back, PlatformRole.Back, NullLogger.Instance);
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the Back", connector, NullLogger.Instance))
        {
            // Check
            LinkFailureException failure = await Should.ThrowAsync<LinkFailureException>(() => client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token));
            failure.Code.ShouldBe(ErrorCodes.LinkUnavailable);
        }

        // Stopping the Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// A peer that opens TCP and never starts TLS does not hold the accept loop: the Gateway is served meanwhile, and the silent peer is closed
    /// when the handshake timeout elapses
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task ASilentPeerNeverHoldsTheAcceptLoopAsync()
    {
        // Running the Back with a short handshake timeout, and opening a TCP connection that sends nothing
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartBack(TimeSpan.FromSeconds(2), stopping.Token, out Task running);
        using TcpClient silent = new TcpClient();
        await silent.ConnectAsync(IPAddress.Loopback, port, token);

        // Checks: the Gateway is served while the silent peer waits
        TlsTransportConnector connector = new TlsTransportConnector(new TcpTransportConnector(), _certificates.CredentialsFor(PlatformRole.Gateway), PlatformRole.Back, NullLogger.Instance);
        await using (LinkClient client = new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), "the Back", connector, NullLogger.Instance))
        {
            PingResponse response = await client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), token);
            response.ServerTime.ShouldBe(_pingTime);
        }

        // Check: the silent peer is closed by the server once its handshake timeout elapsed
        byte[] buffer = new byte[1];
        Exception? end = await Record.ExceptionAsync(async () =>
        {
            if (await silent.GetStream().ReadAsync(buffer, token) != 0)
                throw new InvalidOperationException("The server sent bytes to a peer that never started TLS");
        });
        (end is null or IOException).ShouldBeTrue(end?.ToString());

        // Stopping the Back
        await stopping.CancelAsync();
        await running;
    }

    /// <summary>
    /// Method used to release the certificates of the test
    /// </summary>
    public void Dispose()
    {
        // Releasing the authority and its certificates
        _certificates.Dispose();
    }

    /// <summary>
    /// A peer that starts TLS, then stops in the middle of its first message, is closed when the handshake timeout elapses: the timeout covers
    /// the TLS handshake itself
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task APeerSilentInTheMiddleOfTlsIsClosedAtTheTimeoutAsync()
    {
        // Running the Back with a short handshake timeout, and sending the start of a TLS record (a handshake of 80 bytes) without its end
        CancellationToken token = TestContext.Current.CancellationToken;
        using CancellationTokenSource stopping = CancellationTokenSource.CreateLinkedTokenSource(token);
        int port = StartBack(TimeSpan.FromSeconds(1), stopping.Token, out Task running);
        using TcpClient partial = new TcpClient();
        await partial.ConnectAsync(IPAddress.Loopback, port, token);
        NetworkStream stream = partial.GetStream();
        await stream.WriteAsync(new byte[] { 0x16, 0x03, 0x01, 0x00, 0x50, 0x01 }, token);

        // Check: the server closes it within a few seconds, without waiting for the rest
        byte[] buffer = new byte[1];
        Stopwatch watch = Stopwatch.StartNew();
        Exception? end = await Record.ExceptionAsync(async () =>
        {
            if (await stream.ReadAsync(buffer, token) != 0)
                throw new InvalidOperationException("The server answered a TLS record that never ended");
        });
        (end is null or IOException).ShouldBeTrue(end?.ToString());
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(8));

        // Stopping the Back
        await stopping.CancelAsync();
        await running;
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
