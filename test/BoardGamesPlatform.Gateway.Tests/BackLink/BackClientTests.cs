/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/BackLink/BackClientTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the Back client of the Gateway against a fake Back on a loopback socket
 */

// Using
using System.Net.Sockets;
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Gateway.BackLink;
using BoardGamesPlatform.Gateway.Tests.Support;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.BackLink;

/// <summary>
/// Tests used to check <see cref="BackClient"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the client is the only door from the Gateway to the Back: its answer, its failure when the Back is down and its
/// disposal must not change by accident.</para>
/// <para><b>Behavior:</b> each test starts a fake Back on a real loopback socket (port chosen by the system) and uses the real client and the
/// real TCP connector.</para>
/// </remarks>
public sealed class BackClientTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest duration of a test, in milliseconds
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
    /// Method used to build a client for a Back on the loopback address
    /// </summary>
    /// <param name="port">Port of the Back</param>
    /// <returns>
    /// The client, not connected
    /// </returns>
    private static BackClient CreateClient(int port)
    {
        // Function's return
        return new BackClient(new BackClientOptions { Port = port }, new TcpTransportConnector(), new CapturingLogger<BackClient>());
    }

    #endregion

    #region Publics

    /// <summary>
    /// Ping returns the time of the Back with its offset
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingReturnsTheTimeOfTheBackWithItsOffsetAsync()
    {
        // Starting a fake Back and a client
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeBack back = FakeBack.Start();
        await using BackClient client = CreateClient(back.Port);

        // Calling Ping twice (the second call reuses the connection)
        PingResponse first = await client.PingAsync(token);
        PingResponse second = await client.PingAsync(token);

        // Checks
        first.ServerTime.ShouldBe(FakeBack.PingTime);
        first.ServerTime.Offset.ShouldBe(TimeSpan.FromHours(1));
        second.ServerTime.ShouldBe(FakeBack.PingTime);
    }

    /// <summary>
    /// Ping fails with a SocketException when nothing listens
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task PingFailsWithASocketExceptionWhenNothingListensAsync()
    {
        // Taking the port of a listener that no longer exists
        CancellationToken token = TestContext.Current.CancellationToken;
        int closedPort = await FakeBack.GetClosedPortAsync();
        await using BackClient client = CreateClient(closedPort);

        // Checks
        await Should.ThrowAsync<SocketException>(async () => await client.PingAsync(token));
    }

    /// <summary>
    /// After disposal, every call throws an ObjectDisposedException
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public async Task AfterDisposalPingThrowsAnObjectDisposedExceptionAsync()
    {
        // Starting a fake Back, using the client once, then disposing it
        CancellationToken token = TestContext.Current.CancellationToken;
        await using FakeBack back = FakeBack.Start();
        BackClient client = CreateClient(back.Port);
        (await client.PingAsync(token)).ServerTime.ShouldBe(FakeBack.PingTime);
        await client.DisposeAsync();

        // Checks
        await Should.ThrowAsync<ObjectDisposedException>(async () => await client.PingAsync(token));
    }

    /// <summary>
    /// A client built with an invalid setting is refused
    /// </summary>
    [Fact(Timeout = TestTimeout)]
    public void AClientWithAnInvalidSettingIsRefused()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        // Checks
        InvalidOperationException error = Should.Throw<InvalidOperationException>(() => CreateClient(0));
        error.Message.ShouldContain("Back:Port");
        Should.Throw<ArgumentNullException>(() => new BackClient(null!, new TcpTransportConnector(), new CapturingLogger<BackClient>()));
        Should.Throw<ArgumentNullException>(() => new BackClient(new BackClientOptions(), null!, new CapturingLogger<BackClient>()));
        Should.Throw<ArgumentNullException>(() => new BackClient(new BackClientOptions(), new TcpTransportConnector(), null!));
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
