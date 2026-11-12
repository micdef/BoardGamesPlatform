/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/ProcessEnvironment.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Collection of the test classes that set variables of the process, so that they never run at the same time
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Collection used to run one after the other the test classes that set or read variables of the process
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the variables of a process are shared by every test of the run: a class that sets SQLCONNSTR_Main or CREDENTIALS_DIRECTORY
/// must never run while another class reads the variables of the process. xUnit runs the classes of one collection in sequence; the other
/// classes keep running in parallel.</para>
/// <para><b>Behavior:</b> holds no shared state: the collection only orders the classes.</para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class ProcessEnvironment
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the collection
    /// </summary>
    public const string Name = "Variables of the process";

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
