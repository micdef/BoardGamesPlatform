/*
 *	@file	src/BoardGamesPlatform.Vault/VaultDevelopmentWarningService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Warns at startup that a tier runs as Development, where the secrets may come from any source
 */

// Using
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Hosted service used to log a warning when a tier runs in the Development environment
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> rule 2 of the vault (doc/dev/database.md, The vault): in Development the check of the sources of the secrets is off, so
/// an environment set to Development by mistake on a server must be visible in the journal.</para>
/// <para><b>Behavior:</b> registered only in Development; logs one warning when the host starts (event 9071 of the range 9071-9079 of the vault),
/// then does nothing.</para>
/// </remarks>
public sealed partial class VaultDevelopmentWarningService : IHostedService
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
    /// Logger of the tier
    /// </summary>
    private readonly ILogger<VaultDevelopmentWarningService> _logger;

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
    /// Constructor used to log through the logger of the tier
    /// </summary>
    /// <param name="logger">[Cannot be null] Logger of the tier</param>
    /// <exception cref="ArgumentNullException">If the logger is null</exception>
    public VaultDevelopmentWarningService(ILogger<VaultDevelopmentWarningService> logger)
    {
        // Check
        ArgumentNullException.ThrowIfNull(logger);

        // Setting
        _logger = logger;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to log that the tier runs as Development
    /// </summary>
    [LoggerMessage(EventId = 9071, Level = LogLevel.Warning,
        Message = "The tier runs in the Development environment: its secrets may come from any source, not only from its vault. A server never runs as Development")]
    private partial void LogDevelopment();

    #endregion

    #region Publics

    /// <summary>
    /// Method used to log the warning when the host starts (Asynchronous)
    /// </summary>
    /// <param name="cancellationToken">Token of the start of the host (unused: nothing waits)</param>
    /// <returns>
    /// A completed task
    /// </returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Logging
        LogDevelopment();

        // Function's return
        return Task.CompletedTask;
    }

    /// <summary>
    /// Method used when the host stops (Asynchronous): nothing to do
    /// </summary>
    /// <param name="cancellationToken">Token of the stop of the host (unused)</param>
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
