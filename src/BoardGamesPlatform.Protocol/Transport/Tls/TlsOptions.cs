/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsOptions.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	"Tls" settings of a tier: its certificate, the roots it trusts, the suffix of the deployment and the certificates it refuses
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
/// Settings of mutual TLS on the links of a tier, bound from the "Tls" section
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the same section in the Gateway, the Back and the Very Back; checked at startup by <see cref="TlsOptionsValidator"/>, so
/// that a tier never starts without valid certificates (fail closed).</para>
/// <para><b>Behavior:</b> a relative path is taken from the folder of the tier (its content root). The password is a secret: empty in the
/// committed files, given by the user secrets in development and by the vault of the tier on the servers (0.7.0)
/// (<c>Tls__CertificatePassword</c>).</para>
/// </remarks>
public sealed class TlsOptions
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the configuration section
    /// </summary>
    public const string SectionName = "Tls";

    /// <summary>
    /// Longest warning delay before the expiry accepted
    /// </summary>
    public static readonly TimeSpan MaxExpiryWarning = TimeSpan.FromDays(365);

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
    /// Suffix of the deployment written in the names of the certificates, for example "bgp.internal"
    /// </summary>
    public string IdentitySuffix { get; set; } = string.Empty;

    /// <summary>
    /// PFX of the tier, with its private key
    /// </summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Password of the PFX (a secret: never in a committed file)
    /// </summary>
    public string CertificatePassword { get; set; } = string.Empty;

    /// <summary>
    /// PEM file of the roots of the platform, the only ones trusted (several during a change of root)
    /// </summary>
    public string TrustedRootsPath { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 thumbprints of the certificates refused, whatever their validity (a compromised or withdrawn certificate)
    /// </summary>
    public string[] RefusedThumbprints { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Delay before the expiry of the certificate under which a warning is logged
    /// </summary>
    public TimeSpan ExpiryWarning { get; set; } = TlsCredentials.DefaultExpiryWarning;

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
