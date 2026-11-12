/*
 *	@file	src/BoardGamesPlatform.Vault/VaultMissingWarningService.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Warns at startup that a tier outside Development runs without a vault
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
/// Hosted service used to log a warning when a tier outside Development has no vault
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> without a vault (no setting Vault:Path on Windows, no CREDENTIALS_DIRECTORY on Linux), a server has no source left for its
/// secrets: the validation of its settings then refuses the missing secret, but that message names the secret, not the missing vault. This
/// warning names the cause (found by the review of the silent failures).</para>
/// <para><b>Behavior:</b> registered only outside Development, when no vault was found; logs one warning when the host starts (event 9072 of the
/// range 9071-9079 of the vault), then does nothing.</para>
/// </remarks>
public sealed partial class VaultMissingWarningService : IHostedService
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
    private readonly ILogger<VaultMissingWarningService> _logger;

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
    public VaultMissingWarningService(ILogger<VaultMissingWarningService> logger)
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
    /// Method used to log that the tier runs without a vault
    /// </summary>
    [LoggerMessage(EventId = 9072, Level = LogLevel.Warning,
        Message = "The tier runs outside Development without a vault (no Vault:Path on Windows, no CREDENTIALS_DIRECTORY of systemd on Linux): its secrets have no source")]
    private partial void LogNoVault();

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
        LogNoVault();

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
