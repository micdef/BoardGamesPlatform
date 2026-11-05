/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HelloAckMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Answer of the server to an accepted hello: the protocol version chosen and the limits of the server
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Control;

/// <summary>
/// Acceptance of the hello by the server
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> tells the client which version the connection speaks from now on, and how many requests the server handles at the
/// same time, so that the client never sends more.</para>
/// <para><b>Behavior:</b> written as <c>{"control":"helloAck","version":1,"maxPayloadLength":1048576,"maxConcurrentRequests":64}</c>; every
/// property is required when it is read. The values are checked by <see cref="ProtocolHandshake"/>.</para>
/// </remarks>
public sealed class HelloAckMessage : ControlMessage
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
    /// Largest payload of a frame the server writes and accepts, in bytes; equal to the one of the client
    /// </summary>
    public int MaxPayloadLength { get; }

    /// <summary>
    /// Number of requests of this connection the server handles at the same time, at most
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
    /// <param name="maxPayloadLength">Largest payload of a frame the server writes and accepts, in bytes</param>
    /// <param name="maxConcurrentRequests">Number of requests of the connection handled at the same time, at most</param>
    public HelloAckMessage(int version, int maxPayloadLength, int maxConcurrentRequests)
        : this(ControlKinds.HelloAck, version, maxPayloadLength, maxConcurrentRequests)
    {
    }

    /// <summary>
    /// Constructor used by the JSON reader, which then requires every property
    /// </summary>
    /// <param name="control">[Cannot be null] Kind written in the message; checked by the serializer</param>
    /// <param name="version">Protocol version the connection speaks</param>
    /// <param name="maxPayloadLength">Largest payload of a frame the server writes and accepts, in bytes</param>
    /// <param name="maxConcurrentRequests">Number of requests of the connection handled at the same time, at most</param>
    [JsonConstructor]
    public HelloAckMessage(string control, int version, int maxPayloadLength, int maxConcurrentRequests)
        : base(control)
    {
        // Setting
        Version = version;
        MaxPayloadLength = maxPayloadLength;
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
