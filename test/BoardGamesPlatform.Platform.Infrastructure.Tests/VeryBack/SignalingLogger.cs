/*
 *	@file	test/BoardGamesPlatform.Platform.Infrastructure.Tests/VeryBack/SignalingLogger.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logger that records the log levels and lets a test wait for a message without any delay
 */

// Using
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Platform.Infrastructure.Tests.VeryBack;

/// <summary>
/// Logger used by the tests of the Very Back client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test wait for an event of the client (a connection lost, for example) instead of sleeping.</para>
/// <para><b>Behavior:</b> keeps the formatted messages; <see cref="WaitForAsync"/> completes when a message containing a text has been logged,
/// even if it was logged before the wait started.</para>
/// </remarks>
/// <typeparam name="T">Category of the logger</typeparam>
internal sealed class SignalingLogger<T> : ILogger<T>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Messages logged so far
    /// </summary>
    private readonly ConcurrentQueue<string> _messages;

    /// <summary>
    /// Waits in progress, by awaited text
    /// </summary>
    private readonly ConcurrentDictionary<string, TaskCompletionSource> _waits;

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
    /// Copy of the messages logged so far
    /// </summary>
    public IReadOnlyList<string> Messages => _messages.ToArray();

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    public SignalingLogger()
    {
        // Setting
        _messages = new ConcurrentQueue<string>();
        _waits = new ConcurrentDictionary<string, TaskCompletionSource>();
    }

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
    /// Method used to wait until a message containing a text has been logged
    /// </summary>
    /// <param name="text">[Cannot be null] Text to find in a message</param>
    /// <returns>
    /// A task that completes when such a message has been logged
    /// </returns>
    public Task WaitForAsync(string text)
    {
        // Registering the wait, then completing it at once if the message is already there
        TaskCompletionSource wait = _waits.GetOrAdd(text, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        if (_messages.Any(message => message.Contains(text, StringComparison.Ordinal)))
            wait.TrySetResult();

        // Function's return
        return wait.Task;
    }

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
        // Keeping the message
        string message = formatter(state, exception);
        _messages.Enqueue(message);

        // Completing the waits that look for it
        foreach (KeyValuePair<string, TaskCompletionSource> wait in _waits)
        {
            if (message.Contains(wait.Key, StringComparison.Ordinal))
                wait.Value.TrySetResult();
        }
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
