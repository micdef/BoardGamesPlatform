/*
 *	@file	src/BoardGamesPlatform.Protocol/Correlation/RequestNotSentException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	A request refused by a request client being replaced, before anything of it was written
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Correlation;

/// <summary>
/// Failure of a request that a request client refused because it no longer takes new requests (see <see cref="RequestClient.StopAccepting"/>)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a renewed connection stops taking requests before it is closed; a request refused that way was never registered nor
/// written, so the link client may send it on the new connection without any risk of running it twice.</para>
/// <para><b>Behavior:</b> an <see cref="IOException"/>, as any request on a closing connection, for the callers that do not know it.</para>
/// </remarks>
public sealed class RequestNotSentException : IOException
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
    public RequestNotSentException()
        : base("The request was not sent: the connection no longer takes new requests")
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194)
    /// </summary>
    /// <param name="message">Message of the failure</param>
    public RequestNotSentException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194)
    /// </summary>
    /// <param name="message">Message of the failure</param>
    /// <param name="innerException">Cause of the failure</param>
    public RequestNotSentException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194: every constructor of <see cref="IOException"/>)
    /// </summary>
    /// <param name="message">Message of the failure</param>
    /// <param name="hresult">Result code of the failure</param>
    public RequestNotSentException(string message, int hresult)
        : base(message, hresult)
    {
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
