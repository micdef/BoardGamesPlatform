/*
 *	@file	src/BoardGamesPlatform.Protocol/Control/HelloMessage.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	First message of a connection, sent by the client: the protocol versions and the frame size it supports
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
/// Hello of the client
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the server choose a version both ends speak, and check that both ends agree on the largest frame, before any
/// request.</para>
/// <para><b>Behavior:</b> written as <c>{"control":"hello","minVersion":1,"maxVersion":1,"maxPayloadLength":1048576}</c>; every property is
/// required when it is read. The values are checked by <see cref="ProtocolHandshake"/>.</para>
/// </remarks>
public sealed class HelloMessage : ControlMessage
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
    /// Oldest protocol version the client speaks
    /// </summary>
    public int MinVersion { get; }

    /// <summary>
    /// Newest protocol version the client speaks
    /// </summary>
    public int MaxVersion { get; }

    /// <summary>
    /// Largest payload of a frame the client writes and accepts, in bytes
    /// </summary>
    public int MaxPayloadLength { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="minVersion">Oldest protocol version the client speaks</param>
    /// <param name="maxVersion">Newest protocol version the client speaks</param>
    /// <param name="maxPayloadLength">Largest payload of a frame the client writes and accepts, in bytes</param>
    public HelloMessage(int minVersion, int maxVersion, int maxPayloadLength)
        : this(ControlKinds.Hello, minVersion, maxVersion, maxPayloadLength)
    {
    }

    /// <summary>
    /// Constructor used by the JSON reader, which then requires every property
    /// </summary>
    /// <param name="control">[Cannot be null] Kind written in the message; checked by the serializer</param>
    /// <param name="minVersion">Oldest protocol version the client speaks</param>
    /// <param name="maxVersion">Newest protocol version the client speaks</param>
    /// <param name="maxPayloadLength">Largest payload of a frame the client writes and accepts, in bytes</param>
    [JsonConstructor]
    public HelloMessage(string control, int minVersion, int maxVersion, int maxPayloadLength)
        : base(control)
    {
        // Setting
        MinVersion = minVersion;
        MaxVersion = maxVersion;
        MaxPayloadLength = maxPayloadLength;
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
