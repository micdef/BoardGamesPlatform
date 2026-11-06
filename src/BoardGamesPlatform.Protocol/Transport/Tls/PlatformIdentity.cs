/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/PlatformIdentity.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Identity a certificate of the platform carries for each role: its DNS name and its usages
 */

// Using
using System.Text.RegularExpressions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Identity written in the certificate of each role, shared by the certificate authority that issues it and the tiers that check it
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one definition of the names and usages, so that the issuer and the validator can never drift apart.</para>
/// <para><b>Behavior:</b> the identity of a role is the DNS name <c>&lt;role&gt;.&lt;suffix&gt;</c> (for example <c>back.bgp.internal</c>), written as
/// the only subject alternative name of the certificate and as its common name. The suffix belongs to the deployment and is configured; the
/// name of a role never depends on the machine, so that two tiers on one host keep their own identity. The usages follow the side the
/// role takes on the links: <see cref="ServerAuthenticationOid"/> for the side that accepts, <see cref="ClientAuthenticationOid"/> for the side
/// that connects.</para>
/// </remarks>
public static partial class PlatformIdentity
{
    #region Members

    #region Constants

    /// <summary>
    /// Object identifier of the "TLS server authentication" extended key usage (RFC 5280)
    /// </summary>
    public const string ServerAuthenticationOid = "1.3.6.1.5.5.7.3.1";

    /// <summary>
    /// Object identifier of the "TLS client authentication" extended key usage (RFC 5280)
    /// </summary>
    public const string ClientAuthenticationOid = "1.3.6.1.5.5.7.3.2";

    /// <summary>
    /// Longest suffix accepted, in characters, so that a full name stays well below the 253 characters of a DNS name
    /// </summary>
    public const int MaxSuffixLength = 200;

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

    /// <summary>
    /// Method used to get the expression of a valid suffix: DNS labels of lower-case letters, digits and inner hyphens, separated by dots
    /// </summary>
    /// <returns>
    /// The compiled expression
    /// </returns>
    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*\\z", RegexOptions.CultureInvariant)]
    private static partial Regex SuffixExpression();

    #endregion

    #region Publics

    /// <summary>
    /// Method used to check a deployment suffix
    /// </summary>
    /// <param name="suffix">Suffix to check, for example "bgp.internal"</param>
    /// <returns>
    /// True  ==> The suffix is DNS labels of lower-case letters, digits and inner hyphens, of 1 to <see cref="MaxSuffixLength"/> characters<br/>
    /// False ==> Anything else, null and blank included
    /// </returns>
    public static bool IsValidSuffix(string? suffix)
    {
        // Function's return
        return !string.IsNullOrEmpty(suffix) && suffix.Length <= MaxSuffixLength && SuffixExpression().IsMatch(suffix);
    }

    /// <summary>
    /// Method used to get the name of a role, as written in the first label of its DNS name
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// "gateway", "back" or "veryback"
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">If the role is not one of the platform</exception>
    public static string GetRoleName(PlatformRole role)
    {
        // Function's return
        return role switch
        {
            PlatformRole.Gateway => "gateway",
            PlatformRole.Back => "back",
            PlatformRole.VeryBack => "veryback",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, $"The value of the parameter: {nameof(role)} is not a role of the platform")
        };
    }

    /// <summary>
    /// Method used to read a role from its name
    /// </summary>
    /// <param name="name">Name of the role: "gateway", "back" or "veryback", in lower case</param>
    /// <param name="role">The role read, when the name is known</param>
    /// <returns>
    /// True  ==> The name is the one of a role<br/>
    /// False ==> Any other text, null included
    /// </returns>
    public static bool TryParseRole(string? name, out PlatformRole role)
    {
        // Matching the exact names only
        switch (name)
        {
            case "gateway":
                role = PlatformRole.Gateway;
                break;
            case "back":
                role = PlatformRole.Back;
                break;
            case "veryback":
                role = PlatformRole.VeryBack;
                break;
            default:
                role = default;

                // Function's return
                return false;
        }

        // Function's return
        return true;
    }

    /// <summary>
    /// Method used to build the DNS name of a role in a deployment
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment (see <see cref="IsValidSuffix"/>)</param>
    /// <returns>
    /// The name, for example "back.bgp.internal"
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">If the role is not one of the platform</exception>
    /// <exception cref="ArgumentException">If the suffix is invalid</exception>
    public static string GetDnsName(PlatformRole role, string suffix)
    {
        // Check
        if (!IsValidSuffix(suffix))
            throw new ArgumentException($"The value of the parameter: {nameof(suffix)} must be DNS labels of lower-case letters, digits and inner hyphens, of 1 to {MaxSuffixLength} characters", nameof(suffix));

        // Function's return
        return $"{GetRoleName(role)}.{suffix}";
    }

    /// <summary>
    /// Method used to tell whether a role accepts connections
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// True  ==> The Back and the Very Back, whose certificate holds the server usage<br/>
    /// False ==> The Gateway
    /// </returns>
    public static bool IsServer(PlatformRole role)
    {
        // Function's return
        return role is PlatformRole.Back or PlatformRole.VeryBack;
    }

    /// <summary>
    /// Method used to tell whether a role opens connections
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// True  ==> The Gateway and the Back, whose certificate holds the client usage<br/>
    /// False ==> The Very Back
    /// </returns>
    public static bool IsClient(PlatformRole role)
    {
        // Function's return
        return role is PlatformRole.Gateway or PlatformRole.Back;
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
