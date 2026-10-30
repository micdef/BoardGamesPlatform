/*
 *	@file	src/BoardGamesPlatform.Back.Contracts/Ping/PingResponse.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Body of the Ping response sent by the Back to the Gateway
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Contracts.Ping;

/// <summary>
/// Body of the Ping response sent by the Back to the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that the request crossed every tier down to the database.</para>
/// <para><b>Behavior:</b> written as <c>{"serverTime":"2026-10-30T09:15:00.1234567+01:00"}</c>; the time is required when it is read.</para>
/// </remarks>
public sealed class PingResponse
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
    /// Date and time of the SQL Server when it answered, with its offset from UTC
    /// </summary>
    public DateTimeOffset ServerTime { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor, also used by the JSON reader, which then requires the time
    /// </summary>
    /// <param name="serverTime">Date and time of the SQL Server when it answered</param>
    [JsonConstructor]
    public PingResponse(DateTimeOffset serverTime)
    {
        // Setting
        ServerTime = serverTime;
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
