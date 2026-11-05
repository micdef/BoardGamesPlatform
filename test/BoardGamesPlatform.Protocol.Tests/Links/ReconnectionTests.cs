/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Links/ReconnectionTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the link client survives a peer that goes down in the middle of a session and comes back (done criterion of 0.4.0)
 */

// Using
using BoardGamesPlatform.Back.Contracts;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Protocol.Errors;
using BoardGamesPlatform.Protocol.Links;
using BoardGamesPlatform.Protocol.Transport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Links;

/// <summary>
/// Tests used to check the reconnection of the link client and its back-off
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of 0.4.0: the peer is stopped in the middle of a session; the requests in flight fail cleanly with
/// a code, the calls made while it is down fail at once without flooding it, and the first call once it is back succeeds.</para>
/// <para><b>Behavior:</b> the peer is a <see cref="KillablePeer"/>: it keeps its listening port, so that coming back never depends on the
/// system releasing a port, and it closes every connection while it is down. The back-off runs on a fake clock moved by the test.</para>
/// </remarks>
public sealed class ReconnectionTests
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
    private const string PeerName = "the Very Back";

    #endregion

    #region Variables

    /// <summary>
    /// Time the Ping of the peer answers
    /// </summary>
    private static readonly DateTimeOffset _pingTime = new DateTimeOffset(2026, 11, 3, 1, 0, 0, TimeSpan.FromHours(1));

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
    /// Method used to build a link client in front of the peer, on the fake clock
    /// </summary>
    /// <param name="port">Port of the peer</param>
    /// <param name="clock">[Cannot be null] Fake clock of the back-off</param>
    /// <returns>
    /// The link client, owned by the caller
    /// </returns>
    private static LinkClient CreateClient(int port, FakeTimeProvider clock)
    {
        // Function's return
        return new LinkClient("127.0.0.1", port, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), PeerName, new TcpTransportConnector(), NullLogger.Instance, clock);
    }

    /// <summary>
    /// Method used to send a Ping through the link client
    /// </summary>
    /// <param name="client">[Cannot be null] Link client</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The response of the peer
    /// </returns>
    private static Task<PingResponse> PingAsync(LinkClient client, CancellationToken cancellationToken)
    {
        // Function's return
        return client.SendAsync<PingRequest, PingResponse>(BackOperations.Ping, new PingRequest(), cancellationToken);
    }

    #endregion

    #region Publics

    /// <summary>
    /// The peer is stopped in the middle of a session: the request in flight fails with "link.unavailable", the calls while it is down fail
    /// without flooding it, and the first call once it is back and the delay has passed succeeds
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ThePeerGoesDownInTheMiddleOfASessionAndComesBackAsync()
    {
        // Starting the peer and a client; a first Ping proves the session, a second one stays in flight
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        await using KillablePeer peer = KillablePeer.Start(_pingTime);
        await using LinkClient client = CreateClient(peer.Port, clock);
        (await PingAsync(client, token)).ServerTime.ShouldBe(_pingTime);
        peer.HoldNextPing();
        Task<PingResponse> inFlight = PingAsync(client, token);
        await peer.PingHeld.WaitAsync(token);

        // Stopping the peer in the middle of the session: the request in flight fails cleanly, with the code
        await peer.GoDownAsync();
        LinkFailureException lost = await Should.ThrowAsync<LinkFailureException>(() => inFlight);
        lost.Code.ShouldBe(ErrorCodes.LinkUnavailable);

        // While the peer is down: one attempt fails, then the calls fail at once without reaching the peer
        int acceptedBefore = peer.Accepted;
        (await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token))).Code.ShouldBe(ErrorCodes.LinkUnavailable);
        (await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token))).Code.ShouldBe(ErrorCodes.LinkUnavailable);
        (await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token))).Code.ShouldBe(ErrorCodes.LinkUnavailable);
        peer.Accepted.ShouldBe(acceptedBefore + 1);

        // The peer comes back; once the back-off delay has passed, the first call succeeds
        peer.ComeBack();
        clock.Advance(LinkClient.FirstBackOff * 1.3);
        (await PingAsync(client, token)).ServerTime.ShouldBe(_pingTime);
    }

    /// <summary>
    /// The back-off doubles after each failure in a row: after a second failure, the client waits longer than after the first one
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task TheBackOffDoublesAfterEachFailureAsync()
    {
        // Starting a peer that is down, and a client
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        await using KillablePeer peer = KillablePeer.Start(_pingTime);
        await peer.GoDownAsync();
        await using LinkClient client = CreateClient(peer.Port, clock);

        // First failure, then a second one once the first delay (at most 0.6 s) has passed
        await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token));
        clock.Advance(LinkClient.FirstBackOff * 1.3);
        await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token));
        peer.Accepted.ShouldBe(2);

        // The second delay is at least 0.8 s: after 0.7 s, a call still does not reach the peer; after 1.3 s in all, it does
        clock.Advance(LinkClient.FirstBackOff * 1.4);
        await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token));
        peer.Accepted.ShouldBe(2);
        clock.Advance(LinkClient.FirstBackOff * 1.2);
        await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token));
        peer.Accepted.ShouldBe(3);
    }

    /// <summary>
    /// A connection that opens resets the back-off: after a success, a new loss is reconnected at once
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task ASuccessResetsTheBackOffAsync()
    {
        // Starting the peer down and a client that fails once, then the peer comes back and the client reconnects
        CancellationToken token = TestContext.Current.CancellationToken;
        FakeTimeProvider clock = new FakeTimeProvider();
        await using KillablePeer peer = KillablePeer.Start(_pingTime);
        await peer.GoDownAsync();
        await using LinkClient client = CreateClient(peer.Port, clock);
        await Should.ThrowAsync<LinkFailureException>(() => PingAsync(client, token));
        peer.ComeBack();
        clock.Advance(LinkClient.FirstBackOff * 1.3);
        (await PingAsync(client, token)).ServerTime.ShouldBe(_pingTime);

        // Losing the connection again (the peer drops it but stays up): a call reconnects without the clock moving. The first call may still
        // find the lost connection before the client sees it close (it then fails like any request on a lost connection, which is no failure
        // to open and adds no delay), so three calls are allowed; with a wrong back-off, all three would fail at once
        await peer.DropConnectionsAsync();
        PingResponse? response = null;
        for (int attempt = 1; attempt <= 3 && response is null; attempt++)
        {
            try
            {
                response = await PingAsync(client, token);
            }
            catch (LinkFailureException)
            {
                // The lost connection was found first
            }
        }

        // Check
        response.ShouldNotBeNull().ServerTime.ShouldBe(_pingTime);
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
