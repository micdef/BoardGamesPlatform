/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/FailingBackgroundService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Background service whose work fails at once, used to test the exit code of the Very Back
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
/// Background service that fails
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> stands for a background service of the Very Back that ends with an exception.</para>
/// <para><b>Behavior:</b> its work fails with an <see cref="InvalidOperationException"/> as soon as it starts.</para>
/// </remarks>
internal sealed class FailingBackgroundService : BackgroundService
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

    // #N/A

    #endregion

    #endregion

    #region Overrides

    #region Methods

    /// <summary>
    /// Method used to fail the work of the service (Asynchronous)
    /// </summary>
    /// <param name="stoppingToken">Token of the host (unused)</param>
    /// <returns>
    /// A task that has failed
    /// </returns>
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Function's return
        return Task.FromException(new InvalidOperationException("The work of the service failed"));
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
