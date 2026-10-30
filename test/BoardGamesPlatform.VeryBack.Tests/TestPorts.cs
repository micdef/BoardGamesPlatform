/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/TestPorts.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Block of fixed test ports of this test project
 */

// Using
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests;

/// <summary>
/// Test ports of this project
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> every listener of these tests binds a fixed port of the block of this project (17100-17199 by default), never a port
/// handed out by the system (see <see cref="TestPortBlock"/>).</para>
/// <para><b>Behavior:</b> one block for the whole test run of the project; each call to <see cref="TestPortBlock.Next"/> gives a new port of
/// it.</para>
/// </remarks>
internal static class TestPorts
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Block of ports of this test project, locked on first use; an error (an invalid variable, another run holding the block) is thrown as
    /// it is, not hidden in a type initialization error, and cached for the whole run: every later use throws the same exception, nothing is
    /// retried
    /// </summary>
    private static readonly Lazy<TestPortBlock> _block = new Lazy<TestPortBlock>(() => new TestPortBlock(TestPortBlocks.VeryBack), LazyThreadSafetyMode.ExecutionAndPublication);

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
    /// Block of ports of this test project
    /// </summary>
    public static TestPortBlock Block => _block.Value;

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
