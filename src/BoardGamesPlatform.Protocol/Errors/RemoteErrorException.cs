/*
 *	@file	src/BoardGamesPlatform.Protocol/Errors/RemoteErrorException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Exception thrown to the caller of a request when the peer answers an error code instead of a body
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
/// Exception thrown when the peer of a link answers a request with an error code
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> tells the caller that the request reached the peer and was answered, but not with a body, and why, as a code.</para>
/// <para><b>Behavior:</b> the connection is sound and stays open: the next requests go on as usual. The message names the code only; the
/// details of the failure stay in the logs of the peer.</para>
/// </remarks>
public sealed class RemoteErrorException : Exception
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
    /// Error code answered by the peer (see <see cref="ErrorCodes"/>)
    /// </summary>
    public string Code { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor required by the exception conventions (CA1032); the code is then <see cref="ErrorCodes.Internal"/>
    /// </summary>
    public RemoteErrorException()
        : this(ErrorCodes.Internal)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (CA1032); the code is then <see cref="ErrorCodes.Internal"/>
    /// </summary>
    /// <param name="message">[Can be null] Description of the failure</param>
    /// <param name="innerException">[Can be null] Cause of the failure</param>
    public RemoteErrorException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        // Setting
        Code = ErrorCodes.Internal;
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="code">[Cannot be null] Well-formed error code answered by the peer</param>
    /// <exception cref="ArgumentException">If the code is null or not well-formed</exception>
    public RemoteErrorException(string code)
        : base($"The peer answered the error code {code}")
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
