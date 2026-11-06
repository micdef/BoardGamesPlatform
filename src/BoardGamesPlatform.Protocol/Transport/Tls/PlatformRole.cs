/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/PlatformRole.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Roles of the tiers that open or accept a socket link, each one with its own certificate
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Role of a tier on the socket links, written in its certificate
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a certificate of the platform authority proves which tier holds it, so that a tier accepts only the tier it expects:
/// the Back accepts the Gateway, the Very Back accepts the Back, and a stolen key of the Gateway never opens the Very Back.</para>
/// <para><b>Behavior:</b> see <see cref="PlatformIdentity"/> for the name and the usages each role gets.</para>
/// </remarks>
public enum PlatformRole
{
    /// <summary>
    /// The Gateway: client of the Back only
    /// </summary>
    Gateway = 1,

    /// <summary>
    /// The Back: server of the Gateway, client of the Very Back
    /// </summary>
    Back = 2,

    /// <summary>
    /// The Very Back: server of the Back only
    /// </summary>
    VeryBack = 3
}
