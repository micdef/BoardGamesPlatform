/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/CapturingLogger.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logger that keeps what it is given, for the tests of the warnings of the vault
 */

// Using
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Logger that keeps the level, the event and the message of every entry it is given
/// </summary>
/// <typeparam name="TCategory">Category of the logger (the class that logs)</typeparam>
/// <remarks>
/// <para><b>Purpose:</b> the warnings of the vault are read by the administrators by their event identifier (doc/dev/coding-conventions.md):
/// a test must prove the identifier, the level and the message, not only that the service is registered.</para>
/// </remarks>
internal sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// The entries logged, in their order
    /// </summary>
    private readonly List<(LogLevel Level, EventId Event, string Message)> _entries = new List<(LogLevel Level, EventId Event, string Message)>();

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
    /// Gets the entries logged, in their order
    /// </summary>
    public IReadOnlyList<(LogLevel Level, EventId Event, string Message)> Entries => _entries;

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
    /// Method used to begin a scope (none is kept)
    /// </summary>
    /// <typeparam name="TState">Type of the state of the scope</typeparam>
    /// <param name="state">[Cannot be null] State of the scope</param>
    /// <returns>
    /// Null: the scopes are not kept
    /// </returns>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        // Function's return
        return null;
    }

    /// <summary>
    /// Method used to know whether a level is logged
    /// </summary>
    /// <param name="logLevel">Level of the entry</param>
    /// <returns>
    /// True: every level is kept
    /// </returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to keep an entry
    /// </summary>
    /// <typeparam name="TState">Type of the state of the entry</typeparam>
    /// <param name="logLevel">Level of the entry</param>
    /// <param name="eventId">Event of the entry</param>
    /// <param name="state">[Cannot be null] State of the entry</param>
    /// <param name="exception">[Can be null] Exception of the entry</param>
    /// <param name="formatter">[Cannot be null] Formatter of the message</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Check
        ArgumentNullException.ThrowIfNull(formatter);

        // Keeping the entry
        _entries.Add((logLevel, eventId, formatter(state, exception)));
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
