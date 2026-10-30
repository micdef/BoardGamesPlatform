/*
 *	@file	src/BoardGamesPlatform.Gateway/BackLink/UnavailableBackClient.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Temporary Back client that always fails, until the link to the Back is wired
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
/// Back client that always fails because the link to the Back is not wired yet
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Gateway start and serve its hub before the socket link to the Back exists.</para>
/// <para><b>Behavior:</b> every call throws an <see cref="InvalidOperationException"/>. TEMPORARY: the task feature/back-gateway-link
/// replaces it by the real client (a request client on port 7821) and removes this class.</para>
/// </remarks>
public sealed class UnavailableBackClient : IBackClient
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

    // #N/A

    #endregion

    #region Publics

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Always: the Back link is not wired yet</exception>
    public Task<PingResponse> PingAsync(CancellationToken cancellationToken)
    {
        // Failing, the link to the Back does not exist yet
        throw new InvalidOperationException("The link to the Back is not wired yet");
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
