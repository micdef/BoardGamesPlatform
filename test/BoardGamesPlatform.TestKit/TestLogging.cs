/*
 *	@file	test/BoardGamesPlatform.TestKit/TestLogging.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Settings that keep a tier started by a test from writing to the system log of the machine
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Logging settings of the tiers started by the tests
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier started through its Program.cs writes to the Windows Event Log, under a source that only an administrator
/// can create (the installer does it from 0.25.0). On a machine or a CI runner without that source, the first warning of a test tries to
/// create it, which may be refused; the logger then throws into the code under test, and the test fails for a reason of the machine, not of
/// the code (seen on the CI, at random).</para>
/// <para><b>Behavior:</b> the Event Log provider gets the level None: nothing reaches it, the other providers (the console, the capturing
/// loggers of the tests) are unchanged.</para>
/// </remarks>
public static class TestLogging
{
    #region Members

    #region Constants

    // #N/A

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

    /// <summary>
    /// Settings to add to the configuration of a tier started by a test: the Event Log receives nothing
    /// </summary>
    public static IReadOnlyDictionary<string, string?> WithoutEventLog { get; } = new Dictionary<string, string?>
    {
        ["Logging:EventLog:LogLevel:Default"] = "None"
    };

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

    // #N/A

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
