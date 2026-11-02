/*
 *	@file	src/BoardGamesPlatform.Protocol/Errors/ErrorCodes.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Catalogue of the error codes of the socket protocol, answered instead of a response body
 */

// Using
using System.Text.RegularExpressions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Errors;

/// <summary>
/// Error codes of the socket protocol
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier that cannot answer a request tells why with a code, never with a display text: the fronts translate the codes,
/// and a code never carries an internal detail (an exception message, a server name, a piece of SQL).</para>
/// <para><b>Behavior:</b> a code is a short dotted name in camelCase, for example <c>request.unknownOperation</c>: readable in the logs, stable
/// across versions. The codes of this class belong to the protocol itself (the transport of a request); the business codes of the games and of
/// the platform come later, in the contracts of the tiers. The details of a failure stay in the logs of the tier where it happened.</para>
/// </remarks>
public static partial class ErrorCodes
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest error code accepted
    /// </summary>
    public const int MaxCodeLength = 64;

    /// <summary>
    /// The operation of the request is unknown to the peer, for example when two tiers of different versions talk to each other
    /// </summary>
    public const string RequestUnknownOperation = "request.unknownOperation";

    /// <summary>
    /// The body of the request does not match its operation
    /// </summary>
    public const string RequestInvalid = "request.invalid";

    /// <summary>
    /// The peer failed while handling the request; the details are in its logs only
    /// </summary>
    public const string Internal = "internal";

    /// <summary>
    /// A tier the peer needs to answer cannot be reached, or its connection was lost before the response
    /// </summary>
    public const string LinkUnavailable = "link.unavailable";

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

    // #N/A

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to get the pattern of a well-formed error code
    /// </summary>
    /// <returns>
    /// The compiled pattern: one to four dotted parts, each one a lowercase ASCII letter followed by ASCII letters and digits
    /// </returns>
    [GeneratedRegex(@"^[a-z][A-Za-z0-9]*(\.[a-z][A-Za-z0-9]*){0,3}\z", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    #endregion

    #region Publics

    /// <summary>
    /// Method used to check that a text is a well-formed error code
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> checks the shape only, not that the code is known: a newer peer may answer a code this version does not know yet,
    /// and the caller then handles it as a failure like any other.</para>
    /// </remarks>
    /// <param name="code">[Can be null] Text to check</param>
    /// <returns>
    /// True  ==> The text is a well-formed error code, at most <see cref="MaxCodeLength"/> characters long<br/>
    /// False ==> The text is null, too long or malformed
    /// </returns>
    public static bool IsWellFormed(string? code)
    {
        // Function's return
        return code is not null && code.Length <= MaxCodeLength && CodePattern().IsMatch(code);
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
