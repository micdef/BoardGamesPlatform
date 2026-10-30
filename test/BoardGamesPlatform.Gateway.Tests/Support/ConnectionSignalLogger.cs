/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/ConnectionSignalLogger.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Logger of the fake Back that signals when the link server reports a closed connection
 */

// Using
using Microsoft.Extensions.Logging;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// Logger given to the link server of the fake Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a test wait for the end of a connection on the Back side, instead of sleeping.</para>
/// <para><b>Behavior:</b> calls the given action when the link server logs that a connection from a peer is closed (the message starts with
/// "Connection from" and ends with "closed"). It depends on that text of <c>LinkServer</c>: if the text changes, the tests that wait for it
/// fail by timeout. Everything else is ignored.</para>
/// </remarks>
internal sealed class ConnectionSignalLogger : ILogger<FakeBack>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Action called when a connection is reported closed
    /// </summary>
    private readonly Action _onClosed;

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

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="onClosed">[Cannot be null] Action called when a connection is reported closed</param>
    /// <exception cref="ArgumentNullException">If the action is null</exception>
    public ConnectionSignalLogger(Action onClosed)
    {
        // Check
        if (onClosed is null)
            throw new ArgumentNullException(nameof(onClosed), $"The value of the parameter: {nameof(onClosed)} cannot be null");

        // Setting
        _onClosed = onClosed;
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
        // Signaling the end of a connection
        string message = formatter(state, exception);
        if (message.StartsWith("Connection from", StringComparison.Ordinal) && message.EndsWith("closed", StringComparison.Ordinal))
            _onClosed();
    }

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
