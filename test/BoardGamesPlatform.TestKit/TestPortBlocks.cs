/*
 *	@file	test/BoardGamesPlatform.TestKit/TestPortBlocks.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Index of the block of test ports of each test project
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
/// Block of test ports of each test project that opens sockets
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place that gives each test project its own block, so that two projects running at the same time never share a
/// port. A new test project that opens sockets takes the next free index here.</para>
/// <para><b>Behavior:</b> with the default range, block <c>n</c> holds the ports 17000 + 100 × n to 17099 + 100 × n.</para>
/// </remarks>
public static class TestPortBlocks
{
    #region Members

    #region Constants

    /// <summary>
    /// Socket protocol tests: 17000-17099
    /// </summary>
    public const int Protocol = 0;

    /// <summary>
    /// Very Back tests: 17100-17199
    /// </summary>
    public const int VeryBack = 1;

    /// <summary>
    /// Platform adapters tests (the client of the Very Back): 17200-17299
    /// </summary>
    public const int PlatformInfrastructure = 2;

    /// <summary>
    /// Back tests: 17300-17399
    /// </summary>
    public const int Back = 3;

    /// <summary>
    /// Gateway tests: 17400-17499
    /// </summary>
    public const int Gateway = 4;

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
