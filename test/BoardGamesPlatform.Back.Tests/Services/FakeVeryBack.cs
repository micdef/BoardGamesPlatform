/*
 *	@file	test/BoardGamesPlatform.Back.Tests/Services/FakeVeryBack.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Very Back on a loopback socket, used to test the Back without a database
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Correlation;
using BoardGamesPlatform.Protocol.Framing;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Messages;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.VeryBack.Contracts;
using BoardGamesPlatform.VeryBack.Contracts.Ping;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Tests.Services;

/// <summary>
/// Fake Very Back answering the Ping operation with a fixed time, or never answering it
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the tests of the Back prove the path Gateway → Back → Very Back without a database.</para>
/// <para><b>Behavior:</b> a real link server on port 0 of the loopback address, so that the Back speaks to it exactly as to the real Very
/// Back. A fake that does not answer waits until it is disposed, like a Very Back stuck on its database. Disposing the fake stops it and closes
/// its connections.</para>
/// </remarks>
internal sealed class FakeVeryBack : IAsyncDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Time answered by the Ping operation, with an offset that is not the one of the test machine
    /// </summary>
    public static readonly DateTimeOffset PingTime = new DateTimeOffset(2026, 10, 30, 9, 15, 0, TimeSpan.FromHours(-5));

    /// <summary>
    /// Stops the link server when the fake is disposed
    /// </summary>
    private readonly CancellationTokenSource _stopping;

    /// <summary>
    /// Task of the running link server
    /// </summary>
    private readonly Task _running;

    /// <summary>
    /// Completed when a Ping request has been received
    /// </summary>
    private readonly TaskCompletionSource _received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Port given by the system
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Task completed when a Ping request has been received
    /// </summary>
    public Task Received => _received.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor; starts listening at once
    /// </summary>
    /// <param name="answers">Whether the Ping is answered; when false, it waits until the fake is disposed</param>
    public FakeVeryBack(bool answers = true)
    {
        // Starting the listener and the link server
        TcpTransportListener listener = TcpTransportListener.Start(new IPEndPoint(IPAddress.Loopback, 0));
        Port = listener.LocalEndPoint.Port;
        OperationDispatcher dispatcher = new OperationDispatcher().Register<PingRequest, PingResponse>(VeryBackOperations.Ping,
            async (_, cancellationToken) =>
            {
                // Telling the test, then answering or waiting until the fake stops
                _received.TrySetResult();
                if (!answers)
                    await Task.Delay(Timeout.Infinite, cancellationToken);

                // Function's return
                return new PingResponse(PingTime);
            });
        _stopping = new CancellationTokenSource();
        _running = new LinkServer(listener, new RequestServer(new FrameCodec(), dispatcher.HandleAsync), "the fake Very Back", NullLogger.Instance,
            LinkServer.DefaultAcceptFailurePause).RunAsync(_stopping.Token);
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
    /// Method used to stop the fake and close its connections
    /// </summary>
    /// <returns>
    /// A task that completes once the fake is stopped
    /// </returns>
    public async ValueTask DisposeAsync()
    {
        // Stopping the link server, then waiting for its end
        await _stopping.CancelAsync();
        await _running;
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
