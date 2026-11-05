/*
 *	@file	src/BoardGamesPlatform.Protocol/Errors/LinkFailureException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Exception thrown by the client of a link when the request could not get its answer from the peer
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Errors;

/// <summary>
/// Exception thrown when a request sent on a link gets no answer: the peer cannot be reached, or the connection is lost before the response
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one exception type for every way a link can fail, carrying the error code that a tier answers in turn to its own
/// caller; the Back can then tell the Gateway <see cref="ErrorCodes.LinkUnavailable"/> instead of failing as a whole.</para>
/// <para><b>Behavior:</b> it is an <see cref="IOException"/>, the cause being kept as the inner exception (for the logs of the tier, never
/// sent to a peer). A request that fails this way was perhaps handled by the peer: it is never retried automatically.</para>
/// </remarks>
public sealed class LinkFailureException : IOException
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

    /// <summary>
    /// Error code of the failure (see <see cref="ErrorCodes"/>)
    /// </summary>
    public string Code { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor required by the exception conventions (CA1032); the code is then <see cref="ErrorCodes.LinkUnavailable"/>
    /// </summary>
    public LinkFailureException()
        : this(ErrorCodes.LinkUnavailable, "The peer of the link cannot be reached", null)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (CA1032); the code is then <see cref="ErrorCodes.LinkUnavailable"/>
    /// </summary>
    /// <param name="message">[Cannot be null] Description of the failure, for the logs</param>
    public LinkFailureException(string message)
        : this(ErrorCodes.LinkUnavailable, message, null)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (CA1032); the code is then <see cref="ErrorCodes.LinkUnavailable"/>
    /// </summary>
    /// <param name="message">[Cannot be null] Description of the failure, for the logs</param>
    /// <param name="innerException">[Can be null] Cause of the failure</param>
    public LinkFailureException(string message, Exception? innerException)
        : this(ErrorCodes.LinkUnavailable, message, innerException)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194: every constructor of <see cref="IOException"/>); the code is then
    /// <see cref="ErrorCodes.LinkUnavailable"/>
    /// </summary>
    /// <param name="message">[Cannot be null] Description of the failure, for the logs</param>
    /// <param name="hresult">HRESULT of the failure</param>
    public LinkFailureException(string message, int hresult)
        : base(message, hresult)
    {
        // Setting
        Code = ErrorCodes.LinkUnavailable;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="code">[Cannot be null] Well-formed error code of the failure</param>
    /// <param name="message">[Cannot be null] Description of the failure, for the logs</param>
    /// <param name="innerException">[Can be null] Cause of the failure</param>
    /// <exception cref="ArgumentException">If the code is null or not well-formed</exception>
    public LinkFailureException(string code, string message, Exception? innerException)
        : base(message, innerException)
    {
        // Check
        if (!ErrorCodes.IsWellFormed(code))
            throw new ArgumentException($"The value of the parameter: {nameof(code)} is not a well-formed error code", nameof(code));

        // Setting
        Code = code;
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

    // #N/A

    #endregion

    #region Operators

    // #N/A

    #endregion

    #endregion
}
