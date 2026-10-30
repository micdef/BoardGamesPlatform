/*
 *	@file	test/BoardGamesPlatform.EndToEnd.Tests/Support/FakePingProcedure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Fake of the Ping procedure used to run the five tiers without a database
 */

// Using
using BoardGamesPlatform.VeryBack.Procedures;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.EndToEnd.Tests.Support;

/// <summary>
/// Fake of <see cref="IPingProcedure"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the end-to-end tests choose what the "database" answers, or make it fail, while every tier above it is real.</para>
/// <para><b>Behavior:</b> every call runs the function given to the constructor and counts itself.</para>
/// </remarks>
internal sealed class FakePingProcedure : IPingProcedure
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Number of calls so far
    /// </summary>
    private int _calls;

    #endregion

    #region Delegates

    /// <summary>
    /// Function that answers each call; receives the number of the call (1 for the first)
    /// </summary>
    private readonly Func<int, DateTimeOffset> _answer;

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
    /// Number of calls so far
    /// </summary>
    public int Calls => Volatile.Read(ref _calls);

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="answer">[Cannot be null] Function that answers each call, given the number of the call; it may throw to simulate a failure</param>
    public FakePingProcedure(Func<int, DateTimeOffset> answer)
    {
        // Setting
        _answer = answer;
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
    /// Method used to answer a call with the function of the fake
    /// </summary>
    /// <param name="cancellationToken">Token of the call (unused)</param>
    /// <returns>
    /// The time given by the function for this call
    /// </returns>
    public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken)
    {
        // Counting the call, then answering
        int call = Interlocked.Increment(ref _calls);

        // Function's return
        return Task.FromResult(_answer(call));
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
