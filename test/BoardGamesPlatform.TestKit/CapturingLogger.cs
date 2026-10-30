/*
 *	@file	test/BoardGamesPlatform.TestKit/CapturingLogger.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logger that keeps the messages, used to check what a tier logs
 */

// Using
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Logger that keeps every message with its level
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test check the level and the text of what is logged, for example that a warning names no secret.</para>
/// <para><b>Behavior:</b> every level is enabled; scopes are ignored.</para>
/// </remarks>
/// <typeparam name="T">Category of the logger</typeparam>
public sealed class CapturingLogger<T> : ILogger<T>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Entries logged so far
    /// </summary>
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new ConcurrentQueue<(LogLevel Level, string Message)>();

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
    /// Copy of the entries logged so far, with their level
    /// </summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => _entries.ToArray();

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

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        // Function's return
        return null;
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        // Function's return
        return true;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Keeping the entry
        _entries.Enqueue((logLevel, formatter(state, exception)));
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
