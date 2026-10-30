/*
 *	@file	test/BoardGamesPlatform.TestKit/FailingTransportListener.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake listener whose accepts fail as a test decides, used to test the accept loop of a link
 */

// Using
using System.Net;
using BoardGamesPlatform.Protocol.Transport;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Fake of <see cref="ITransportListener"/> that never accepts a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a real listener cannot be made to lose its socket or to fail its accepts on demand; this fake lets the tests prove
/// what the accept loop does then.</para>
/// <para><b>Behavior:</b> each accept runs the function given to the constructor, with the number of the call (1 for the first): the
/// function throws the failure to simulate, or returns false to make the accept wait until its token is canceled, like an idle
/// listener.</para>
/// </remarks>
public sealed class FailingTransportListener : ITransportListener
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Number of accepts so far
    /// </summary>
    private int _accepts;

    /// <summary>
    /// Number of disposals so far
    /// </summary>
    private int _disposals;

    /// <summary>
    /// Completed when an accept starts waiting (the function returned false)
    /// </summary>
    private readonly TaskCompletionSource _waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    #endregion

    #region Delegates

    /// <summary>
    /// Function run by each accept, given the number of the call; it throws a failure, or returns false to wait
    /// </summary>
    private readonly Func<int, bool> _accept;

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

    /// <inheritdoc/>
    public IPEndPoint LocalEndPoint { get; }

    /// <summary>
    /// Number of accepts so far
    /// </summary>
    public int Accepts => Volatile.Read(ref _accepts);

    /// <summary>
    /// Number of disposals so far
    /// </summary>
    public int Disposals => Volatile.Read(ref _disposals);

    /// <summary>
    /// Task completed when an accept starts waiting for its token
    /// </summary>
    public Task Waiting => _waiting.Task;

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="localEndPoint">[Cannot be null] End point the fake claims to listen on</param>
    /// <param name="accept">[Cannot be null] Function run by each accept, given the number of the call</param>
    public FailingTransportListener(IPEndPoint localEndPoint, Func<int, bool> accept)
    {
        // Setting
        LocalEndPoint = localEndPoint;
        _accept = accept;
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
    /// Method used to run the function of the fake, then wait for the token when it returns false (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token of the accept loop</param>
    /// <returns>
    /// Never a connection: the task fails, or is canceled with the token
    /// </returns>
    public async Task<ITransportConnection> AcceptAsync(CancellationToken cancellationToken = default)
    {
        // Running the function of the fake
        int call = Interlocked.Increment(ref _accepts);
        if (!_accept(call))
        {
            // Waiting like an idle listener, until the loop stops
            _waiting.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }

        // Function's return (never reached: the function throws when it returns true)
        throw new InvalidOperationException("The function of the fake must throw or return false");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        // Counting the disposal
        Interlocked.Increment(ref _disposals);

        // Function's return
        return ValueTask.CompletedTask;
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
