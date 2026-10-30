/*
 *	@file	src/BoardGamesPlatform.Observability/HostExitCode.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Exit code of an executable once its host has run, telling the service manager whether a background service failed
 */

// Using
using Microsoft.Extensions.Hosting;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Observability;

/// <summary>
/// Exit code of an executable of the platform
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a background service that fails stops the host, but the host then ends without an error (.NET 10), so the service
/// manager (Windows services, systemd) would see a normal stop and never restart the executable. Shared by every executable that runs
/// background services, next to the other signals an executable gives to the operators.</para>
/// <para><b>Behavior:</b> <see cref="Failure"/> when one of the background services ended with an exception, <see cref="Success"/>
/// otherwise. The hosted services must be read before the host runs: it is disposed once it has run.</para>
/// </remarks>
public static class HostExitCode
{
    #region Members

    #region Constants

    /// <summary>
    /// Exit code of an executable that stopped normally
    /// </summary>
    public const int Success = 0;

    /// <summary>
    /// Exit code of an executable stopped by the failure of one of its background services
    /// </summary>
    public const int Failure = 1;

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
    /// Method used to get the exit code of the process once the host has run
    /// </summary>
    /// <param name="hostedServices">[Cannot be null] Hosted services of the host, read before it ran</param>
    /// <returns>
    /// The exit code of the process
    /// </returns>
    /// <exception cref="ArgumentNullException">If the services are null</exception>
    public static int FromHostedServices(IEnumerable<IHostedService> hostedServices)
    {
        // Check
        if (hostedServices is null)
            throw new ArgumentNullException(nameof(hostedServices), $"The value of the parameter: {nameof(hostedServices)} cannot be null");

        // Looking for a background service that failed
        foreach (IHostedService hostedService in hostedServices)
        {
            if (hostedService is BackgroundService { ExecuteTask.IsFaulted: true })
                return Failure;
        }

        // Function's return
        return Success;
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
