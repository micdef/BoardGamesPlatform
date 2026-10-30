/*
 *	@file	test/BoardGamesPlatform.Web.Tests/Support/FakeBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Back client, registered in the Gateway started in process, that answers with a given time or fails
 */

// Extern alias
extern alias GatewayApp;

// Using
using BoardGamesPlatform.Back.Contracts.Ping;
using GatewayApp::BoardGamesPlatform.Gateway.BackLink;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.Tests.Support;

/// <summary>
/// Back client that replaces the one of the Gateway in the tests of the website
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the real Gateway answer to the website without a Back, a socket or a database.</para>
/// <para><b>Behavior:</b> answers with the time it was given, throws the exception it was given, or never answers.</para>
/// </remarks>
public sealed class FakeBackClient : IBackClient
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Time returned by the Ping operation; null when the client fails
    /// </summary>
    private readonly DateTimeOffset? _serverTime;

    /// <summary>
    /// Exception thrown by the Ping operation; null when the client answers
    /// </summary>
    private readonly Exception? _failure;

    /// <summary>
    /// True when the client never answers (until the call is canceled)
    /// </summary>
    private readonly bool _hold;

    /// <summary>
    /// Completed when the first Ping call reaches the client
    /// </summary>
    private readonly TaskCompletionSource _entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

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
    /// Completes when the first Ping call reaches the client
    /// </summary>
    public Task Entered => _entered.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor of a client that answers
    /// </summary>
    /// <param name="serverTime">Time returned by the Ping operation</param>
    public FakeBackClient(DateTimeOffset serverTime)
    {
        // Setting
        _serverTime = serverTime;
    }

    /// <summary>
    /// Constructor of a client that never answers
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> lets a test hold a call open in the Gateway.</para>
    /// <para><b>Behavior:</b> the call ends only when it is canceled, for example when the front disconnects.</para>
    /// </remarks>
    public FakeBackClient()
    {
        // Setting
        _hold = true;
    }

    /// <summary>
    /// Constructor of a client that fails
    /// </summary>
    /// <param name="failure">[Cannot be null] Exception thrown by the Ping operation</param>
    public FakeBackClient(Exception failure)
    {
        // Setting
        _failure = failure;
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

    /// <inheritdoc/>
    public async Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Telling the test that the call arrived, and holding it when asked to
        _entered.TrySetResult();
        if (_hold)
            await new TaskCompletionSource().Task.WaitAsync(cancellationToken);

        // Failing when asked to
        if (_failure is not null)
            throw _failure;

        // Function's return
        return new PingResponse(_serverTime!.Value);
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
