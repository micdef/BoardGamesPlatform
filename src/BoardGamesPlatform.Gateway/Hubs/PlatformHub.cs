/*
 *	@file	src/BoardGamesPlatform.Gateway/Hubs/PlatformHub.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	SignalR hub that the fronts connect to
 */

// Using
using BoardGamesPlatform.Gateway.BackLink;
using BoardGamesPlatform.Gateway.Contracts;
using Microsoft.AspNetCore.SignalR;

// Static using
// #N/A

// Named using
using BackPingResponse = BoardGamesPlatform.Back.Contracts.Ping.PingResponse;
using FrontPingResponse = BoardGamesPlatform.Gateway.Contracts.Ping.PingResponse;

// Namespace
namespace BoardGamesPlatform.Gateway.Hubs;

/// <summary>
/// SignalR hub that every front (Blazor, WPF, third-party UI) connects to
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the single entry point of the fronts. The Gateway holds no game logic and no business rule: it only translates
/// the calls of the fronts into requests to the Back, and the answers of the Back into answers to the fronts.</para>
/// <para><b>Behavior:</b> the method names are those of <see cref="PlatformHubContract"/>. The hub is NOT authenticated yet: anyone who
/// can reach the Gateway can call it. Authentication comes in 0.10.0. Detailed errors are off: an exception raised by the Back link reaches
/// a front as a generic error, without its message.</para>
/// </remarks>
public sealed class PlatformHub : Hub
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

    /// <summary>
    /// Client used to call the Back
    /// </summary>
    private readonly IBackClient _svcBackClient;

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
    /// <param name="svcBackClient">[Cannot be null] Client used to call the Back</param>
    /// <exception cref="ArgumentNullException">If the client is null</exception>
    public PlatformHub(IBackClient svcBackClient)
    {
        // Check
        if (svcBackClient is null)
            throw new ArgumentNullException(nameof(svcBackClient), $"The value of the parameter: {nameof(svcBackClient)} cannot be null");

        // Setting
        _svcBackClient = svcBackClient;
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

    /// <summary>
    /// Method used by a front to check that the chain of tiers answers
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> proves to a front that the Gateway, the Back, the Very Back and SQL Server answer.</para>
    /// <para><b>Behavior:</b> exposed to the fronts under the name <see cref="PlatformHubContract.Ping"/>. Asks the Back and returns its
    /// time as it is; the request is canceled when the front disconnects.</para>
    /// </remarks>
    /// <returns>
    /// The date and time of the SQL Server when it answered, with its offset from UTC
    /// </returns>
    [HubMethodName(PlatformHubContract.Ping)]
    public async Task<FrontPingResponse> PingAsync()
    {
        // Asking the Back, giving up when the front disconnects
        BackPingResponse back = await _svcBackClient.PingAsync(Context.ConnectionAborted);

        // Function's return
        return new FrontPingResponse(back.ServerTime);
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
