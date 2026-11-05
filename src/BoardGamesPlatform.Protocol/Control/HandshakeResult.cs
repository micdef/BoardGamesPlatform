/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HandshakeResult.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	What both ends of a connection agreed on during the handshake
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Agreement reached by the handshake of a connection
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> keeps what the rest of the connection must respect: the version it speaks, and how many requests the server handles
/// at the same time.</para>
/// </remarks>
public sealed class HandshakeResult
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
    /// Protocol version the connection speaks
    /// </summary>
    public int Version { get; }

    /// <summary>
    /// Number of requests of the connection the server handles at the same time, at most
    /// </summary>
    public int MaxConcurrentRequests { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="version">Protocol version the connection speaks</param>
    /// <param name="maxConcurrentRequests">Number of requests of the connection the server handles at the same time, at most</param>
    public HandshakeResult(int version, int maxConcurrentRequests)
    {
        // Setting
        Version = version;
        MaxConcurrentRequests = maxConcurrentRequests;
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
