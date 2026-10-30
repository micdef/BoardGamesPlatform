/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Ping/FakeProcedurePingTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Proves the Ping through the five tiers, with a fake procedure instead of SQL Server
 */

// Using
using BoardGamesPlatform.EndToEnd.Tests.Support;
using BoardGamesPlatform.Gateway.Contracts;
using BoardGamesPlatform.Gateway.Contracts.Ping;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Ping;

/// <summary>
/// Tests used to check the chain front, Gateway, Back, Very Back, with a fake Ping procedure
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of version 0.3.0 in the CI: a headless front obtains the time of the database through the real
/// code of every tier, the database being replaced by a fake procedure (the variant on the real SQL Server is in
/// <see cref="SqlPingTests"/>).</para>
/// <para><b>Behavior:</b> each test starts the Very Back, the Back and the Gateway in one process (<see cref="TierChain"/>), on fixed test
/// ports, and calls the hub as a front does. The tests wait for the answers, never for a fixed delay.</para>
/// </remarks>
public sealed class FakeProcedurePingTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Message a front receives when the Ping method fails on the server, detailed errors being off (Microsoft Learn, SignalR hubs, handle errors)
    /// </summary>
    private const string GenericPingError = "An unexpected error occurred invoking 'Ping' on the server.";

    /// <summary>
    /// Message of the failure of the fake procedure; the front must never receive it
    /// </summary>
    private const string InternalMessage = "secret failure of the fake procedure";

    /// <summary>
    /// Longest duration of a test, in milliseconds
    /// </summary>
    private const int TestTimeout = 30000;

    /// <summary>
    /// Longest duration of a test that waits for refused connections, in milliseconds: on Windows a refusal on the loopback address takes
    /// about 2 seconds, and the test makes two
    /// </summary>
    private const int RefusalTestTimeout = 60000;

    #endregion

    #region Variables

    /// <summary>
    /// Time the fake procedure answers: a fixed instant with an offset that is not the one of any machine running the tests (UTC+05:30)
    /// </summary>
    private static readonly DateTimeOffset _fakeTime = new DateTimeOffset(2026, 10, 30, 14, 45, 12, 345, TimeSpan.FromMinutes(330));

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// A Ping of the front returns exactly the time of the fake procedure, with its offset
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task PingReturnsTheTimeOfTheProcedureWithItsOffsetAsync()
    {
        // Starting the five tiers, and a front
        CancellationToken token = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(_ => _fakeTime);
        await using TierChain chain = await TierChain.StartAsync(procedure, null, token);
        await using HubConnection front = chain.CreateFront();
        await front.StartAsync(token);

        // Calling Ping
        PingResponse response = await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);

        // Checks: the same instant, the same offset, and the procedure was called once, so the answer crossed every tier
        response.ServerTime.ShouldBe(_fakeTime);
        response.ServerTime.Offset.ShouldBe(TimeSpan.FromMinutes(330));
        response.ServerTime.Ticks.ShouldBe(_fakeTime.Ticks);
        procedure.Calls.ShouldBe(1);
    }

    /// <summary>
    /// A procedure that fails gives the front the generic error only, and the next Ping succeeds on the same chain
    /// </summary>
    /// <remarks>
    /// Deterministic: the Very Back stays up and only the first call of the fake procedure fails, so the second Ping must succeed.
    /// </remarks>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = TestTimeout)]
    public async Task AFailingProcedureGivesTheGenericErrorAndTheNextPingSucceedsAsync()
    {
        // Starting the five tiers with a procedure that fails on its first call only, and a front
        CancellationToken token = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(call => call == 1 ? throw new InvalidOperationException(InternalMessage) : _fakeTime);
        await using TierChain chain = await TierChain.StartAsync(procedure, null, token);
        await using HubConnection front = chain.CreateFront();
        await front.StartAsync(token);

        // Calling Ping: it fails
        HubException error = await Should.ThrowAsync<HubException>(async () => await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token));

        // Checks: the generic message only, and the connection of the front survives
        error.Message.ShouldBe(GenericPingError);
        error.Message.ShouldNotContain(InternalMessage);
        front.State.ShouldBe(HubConnectionState.Connected);

        // Calling Ping again: it succeeds
        PingResponse response = await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token);
        response.ServerTime.ShouldBe(_fakeTime);
        procedure.Calls.ShouldBe(2);
    }

    /// <summary>
    /// A Very Back that is stopped gives the front the generic error only, every time, and the Gateway stays usable
    /// </summary>
    /// <remarks>
    /// Why this and not "a later Ping succeeds once the Very Back is back": restarting a Very Back on the same fixed port depends on the
    /// system releasing the port at once (closed connections can keep it busy for a while), which a test cannot prove deterministically. What is
    /// proven instead: the Ping works, the Very Back is stopped, then two Pings in a row fail cleanly (the second also proves that the Back
    /// does not stay stuck on the lost connection), while the connection of the front to the Gateway stays open.
    /// </remarks>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact(Timeout = RefusalTestTimeout)]
    public async Task AStoppedVeryBackGivesTheGenericErrorAndTheGatewayStaysUsableAsync()
    {
        // Starting the five tiers, and a front; a first Ping proves the chain works while the Very Back is up
        CancellationToken token = TestContext.Current.CancellationToken;
        FakePingProcedure procedure = new FakePingProcedure(_ => _fakeTime);
        await using TierChain chain = await TierChain.StartAsync(procedure, null, token);
        await using HubConnection front = chain.CreateFront();
        await front.StartAsync(token);
        (await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token)).ServerTime.ShouldBe(_fakeTime);

        // Stopping the Very Back, then calling Ping twice: both fail with the generic error only
        await chain.StopVeryBackAsync();
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            HubException error = await Should.ThrowAsync<HubException>(async () => await front.InvokeAsync<PingResponse>(PlatformHubContract.Ping, token));
            error.Message.ShouldBe(GenericPingError);
        }

        // Checks: the front is still connected to the Gateway, and the procedure was not called again
        front.State.ShouldBe(HubConnectionState.Connected);
        procedure.Calls.ShouldBe(1);
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
