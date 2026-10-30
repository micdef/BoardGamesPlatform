/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/FakeBack.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Back listening on a loopback socket, answering the Ping operation
 */

// Using
using System.Net;
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// Fake Back used by the tests of the Back client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> gives the Gateway a real peer on a real socket, whose answer the test controls.</para>
/// <para><b>Behavior:</b> listens on the loopback address with port 0 (the system picks the port, so that tests never collide), serves every
/// accepted connection with the real <see cref="LinkServer"/> and answers Ping with <see cref="PingTime"/>. Disposing it stops the listener and
/// waits for everything it started.</para>
/// </remarks>
internal sealed class FakeBack : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Time answered to every Ping
    /// </summary>
    public static readonly DateTimeOffset PingTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, 123, TimeSpan.FromHours(1));

    /// <summary>
    /// Source canceled when the fake Back stops
    /// </summary>
    private readonly CancellationTokenSource _stopping;

    /// <summary>
    /// Task that accepts and serves the connections, until the fake Back stops
    /// </summary>
    private readonly Task _running;

    /// <summary>
    /// Whether <see cref="DisposeAsync"/> has been called (0: no, 1: yes)
    /// </summary>
    private int _isDisposed;

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

    /// <summary>
    /// Port the fake Back listens on
    /// </summary>
    public int Port { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; use <see cref="Start"/>
    /// </summary>
    private FakeBack()
    {
        // Listening, then serving with the real link server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, 0));
        Port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher()
            .Register<PingRequest, PingResponse>(BackOperations.Ping, (_, _) => Task.FromResult(new PingResponse(PingTime)));
        RequestServer server = new RequestServer(new FrameCodec(), dispatcher.HandleAsync);
        LinkServer link = new LinkServer(listener, server, "the fake Back", new CapturingLogger<FakeBack>(), LinkServer.DefaultAcceptFailurePause);
        _stopping = new CancellationTokenSource();
        _running = link.RunAsync(_stopping.Token);
    }

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
    /// Method used to start a fake Back on a loopback port chosen by the system
    /// </summary>
    /// <returns>
    /// The fake Back, already listening
    /// </returns>
    public static FakeBack Start()
    {
        // Function's return
        return new FakeBack();
    }

    /// <summary>
    /// Method used to get a loopback port where nothing listens
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> lets a test reach "a Back that is down" without guessing a free port in advance.</para>
    /// <para><b>Behavior:</b> starts a listener on port 0, keeps its port, then disposes it: the port was just freed by a real listener. A port
    /// could in theory be taken again by another process before the test uses it; the risk is accepted for loopback test ports.</para>
    /// </remarks>
    /// <returns>
    /// A task whose result is the port of a listener that no longer exists
    /// </returns>
    public static async Task<int> GetClosedPortAsync()
    {
        // Listening on a port chosen by the system, then closing the listener
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, 0));
        int port = listener.LocalEndPoint.Port;
        await listener.DisposeAsync().ConfigureAwait(false);

        // Function's return
        return port;
    }

    /// <summary>
    /// Method used to stop the fake Back and wait for everything it started
    /// </summary>
    /// <returns>
    /// A task that completes when the listener is stopped and every connection served
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Disposing once
        if (Interlocked.Exchange(ref _isDisposed, 1) == 1)
            return;

        // Stopping the link server, which closes the listener and the connections
        await _stopping.CancelAsync().ConfigureAwait(false);
        await _running.ConfigureAwait(false);
        _stopping.Dispose();
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
