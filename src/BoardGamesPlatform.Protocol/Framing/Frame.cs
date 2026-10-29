/*
 *	@file	src/BoardGamesPlatform.Protocol/Framing/Frame.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	One message of the socket protocol: a correlation identifier and an opaque payload
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Framing;

/// <summary>
/// One message exchanged on a link of the platform (Gateway ↔ Back, Back ↔ Very Back)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the unit that <see cref="FrameCodec"/> writes to and reads from a connection.</para>
/// <para><b>Behavior:</b> the correlation identifier pairs a response with its request, so that several requests can be in flight on the
/// same connection. The payload is opaque at this level: the layers above decide what it holds (UTF-8 JSON on the links of the platform).
/// An empty payload is allowed. The payload is not copied: a frame built from an array shares it, so the array must not be modified until
/// the frame is written; a frame returned by <see cref="FrameCodec.ReadAsync"/> owns its array.</para>
/// </remarks>
public sealed class Frame
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
    /// Identifier that pairs a response with its request
    /// </summary>
    public ulong CorrelationId { get; }

    /// <summary>
    /// Content of the message, opaque at this level; may be empty
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="correlationId">Identifier that pairs a response with its request</param>
    /// <param name="payload">Content of the message; may be empty</param>
    public Frame(ulong correlationId, ReadOnlyMemory<byte> payload)
    {
        // Setting
        CorrelationId = correlationId;
        Payload = payload;
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
