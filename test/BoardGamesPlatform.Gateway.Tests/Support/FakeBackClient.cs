/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/FakeBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake Back client that answers with a given time, or fails with a given exception
 */

// Using
using BoardGamesPlatform.Back.Contracts.Ping;
using BoardGamesPlatform.Gateway.BackLink;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// Back client replaced by the tests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the hub be tested without a Back, a socket or a database.</para>
/// <para><b>Behavior:</b> answers with the time it was given, or throws the exception it was given; counts its calls.</para>
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
    /// Number of Ping calls received
    /// </summary>
    private int _pingCalls;

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
    /// Number of Ping calls received
    /// </summary>
    public int PingCalls => Volatile.Read(ref _pingCalls);

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
    public Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Counting the call
        Interlocked.Increment(ref _pingCalls);

        // Failing when asked to
        if (_failure is not null)
            throw _failure;

        // Function's return
        return Task.FromResult(new PingResponse(_serverTime!.Value));
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
