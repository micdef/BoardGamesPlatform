/*
 *	@file	src/BoardGamesPlatform.Gateway/BackLink/IBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	What the Gateway asks of the Back: one method per operation of the Back contract
 */

// Using
using BoardGamesPlatform.Back.Contracts.Ping;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.BackLink;

/// <summary>
/// Interface of the client that the Gateway uses to call the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the hub call the Back without knowing how the request travels (socket, TLS, correlation), and lets the tests
/// replace the Back by a fake.</para>
/// <para><b>Behavior:</b> one method per operation of the Back contract. A failure of the link or of the Back is an exception, which the hub
/// never describes to a front.</para>
/// </remarks>
public interface IBackClient
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
    /// Method used to send the Ping operation to the Back
    /// </summary>
    /// <param name="cancellationToken">Token used to give up the request</param>
    /// <returns>
    /// The answer of the Back, holding the time of the SQL Server
    /// </returns>
    /// <exception cref="InvalidOperationException">If the Back cannot be reached</exception>
    /// <exception cref="OperationCanceledException">If the request is canceled</exception>
    public Task<PingResponse> PingAsync(CancellationToken cancellationToken);

    #endregion
}
