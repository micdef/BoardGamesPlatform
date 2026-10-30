/*
 *	@file	src/BoardGamesPlatform.Web/GatewayLink/IGatewayClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Contract of the client that the website uses to call the Gateway
 */

// Using
using BoardGamesPlatform.Gateway.Contracts.Ping;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Web.GatewayLink;

/// <summary>
/// Client used by the website to call the SignalR hub of the Gateway
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the pages be tested with a fake client, and keeps SignalR out of the components.</para>
/// <para><b>Behavior:</b> one instance lives as long as the circuit of one player (scoped). A failure of any kind (Gateway unreachable,
/// error returned by the hub) is raised as an exception; callers show a generic message and never the text of the exception.</para>
/// </remarks>
public interface IGatewayClient
{
    #region Enumerations

    // #N/A

    #endregion

    #region Accessors/Properties

    #region Members

    // #N/A

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Methods

    /// <summary>
    /// Method used to check that the chain of tiers answers
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> shows that the Gateway, the Back, the Very Back and SQL Server answer.</para>
    /// <para><b>Behavior:</b> calls the Ping method of the hub, connecting to the Gateway first when it is not connected yet.</para>
    /// </remarks>
    /// <param name="cancellationToken">Token used to give up the call</param>
    /// <returns>
    /// The date and time of the SQL Server when it answered, with its offset from UTC
    /// </returns>
    public Task<PingResponse> PingAsync(CancellationToken cancellationToken);

    #endregion
}
