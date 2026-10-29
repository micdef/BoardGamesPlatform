/*
 *	@file	src/SocialGamesPlatform.VeryBack/Services/StartupService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Reports the start and the stop of the Very Back in the logs
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace SocialGamesPlatform.VeryBack.Services;

/// <summary>
/// Service used to report the start and the stop of the Very Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the Very Back host starts and stops cleanly, until its real services exist.</para>
/// <para><b>Behavior:</b> writes one log entry when the host starts and one when it stops; it does nothing else.</para>
/// </remarks>
public sealed partial class StartupService : IHostedService
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

    /// <summary>
    /// Logger of the service
    /// </summary>
    private readonly ILogger<StartupService> _logger;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the service</param>
    /// <exception cref="ArgumentNullException">If the logger is null</exception>
    public StartupService(ILogger<StartupService> logger)
    {
        // Check
        if (logger is null)
            throw new ArgumentNullException(nameof(logger), $"The value of the parameter: {nameof(logger)} cannot be null");

        // Setting services
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log the start of the Very Back
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Very Back started")]
    private partial void LogStarted();

    /// <summary>
    /// Method used to log the stop of the Very Back
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Very Back stopped")]
    private partial void LogStopped();

    #endregion

    #region Publics

    /// <summary>
    /// Method used to report the start of the Very Back (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> called by the host once it has started its services.</para>
    /// <para><b>Behavior:</b> writes the "Very Back started" log entry; never fails.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token signaled when the start must be aborted</param>
    /// <returns>
    /// A completed task
    /// </returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Logging the start
        LogStarted();

        // Function's return
        return Task.CompletedTask;
    }

    /// <summary>
    /// Method used to report the stop of the Very Back (Asynchronous)
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> called by the host when it is asked to stop.</para>
    /// <para><b>Behavior:</b> writes the "Very Back stopped" log entry; never fails.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token signaled when the stop must no longer be graceful</param>
    /// <returns>
    /// A completed task
    /// </returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Logging the stop
        LogStopped();

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
