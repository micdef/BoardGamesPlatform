/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/IdleHostedService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Hosted service that does nothing, used to test the exit code of the Very Back
 */

// Using
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests;

/// <summary>
/// Hosted service that is not a background service
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that a hosted service which is not a background service never changes the exit code.</para>
/// <para><b>Behavior:</b> starts and stops at once.</para>
/// </remarks>
internal sealed class IdleHostedService : IHostedService
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
    /// Method used to start the service, which does nothing
    /// </summary>
    /// <param name="cancellationToken">Token of the start (unused)</param>
    /// <returns>
    /// A completed task
    /// </returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return Task.CompletedTask;
    }

    /// <summary>
    /// Method used to stop the service, which does nothing
    /// </summary>
    /// <param name="cancellationToken">Token of the stop (unused)</param>
    /// <returns>
    /// A completed task
    /// </returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Function's return
        return Task.CompletedTask;
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
