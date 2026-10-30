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
/// <para><b>Behavior:</b> <see cref="Start"/> listens on the loopback address on the next fixed port of the block of this project
/// (<see cref="TestPorts"/>; never a port handed out by the system), serves every accepted connection with the real <see cref="LinkServer"/> and answers Ping with
/// <see cref="PingTime"/>. <see cref="ConnectionClosed"/> completes when a connection to it ends, so that a test waits for the event.
/// Disposing the fake Back stops the listener and waits for everything it started.</para>
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

    /// <summary>
    /// Completed when the link server reports a closed connection
    /// </summary>
    private readonly TaskCompletionSource _connectionClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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

    /// <summary>
    /// Task completed when a connection to the fake Back is closed
    /// </summary>
    public Task ConnectionClosed => _connectionClosed.Task;

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
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, TestPorts.Block.Next()));
        Port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher()
            .Register<PingRequest, PingResponse>(BackOperations.Ping, (_, _) => Task.FromResult(new PingResponse(PingTime)));
        RequestServer server = new RequestServer(new FrameCodec(), dispatcher.HandleAsync);
        LinkServer link = new LinkServer(
            listener, server, "the fake Back", new ConnectionSignalLogger(() => _connectionClosed.TrySetResult()), LinkServer.DefaultAcceptFailurePause);
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
    /// Method used to start a fake Back on the next fixed loopback port of the test block
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
