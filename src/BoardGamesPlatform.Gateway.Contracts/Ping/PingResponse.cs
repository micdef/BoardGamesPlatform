/*
 *	@file	src/BoardGamesPlatform.Gateway.Contracts/Ping/PingResponse.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Result of the Ping hub method, returned by the Gateway to a front
 */

// Using
using System.Text.Json.Serialization;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Contracts.Ping;

/// <summary>
/// Result of the Ping hub method, returned by the Gateway to a front
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets a front show that the whole chain answers: front, Gateway, Back, Very Back and SQL Server.</para>
/// <para><b>Behavior:</b> written as <c>{"serverTime":"2026-10-30T09:15:00.1234567+01:00"}</c>.</para>
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
