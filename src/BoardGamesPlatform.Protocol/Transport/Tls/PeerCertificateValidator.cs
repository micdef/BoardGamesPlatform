/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/PeerCertificateValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Decides whether a certificate is the one of the tier expected: signed by the platform authority, of the right role, and not refused
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Check of the certificate of a peer on a socket link
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a valid certificate is not enough: it must be the one of the tier expected. The Back accepts the Gateway only, the
/// Very Back the Back only, so that the key of one tier never opens a link meant for another one.</para>
/// <para><b>Behavior:</b> the certificate is accepted when all of these hold, checked at the time given:</para>
/// <list type="number">
/// <item><description>it chains to one of the trusted roots of the platform, and to nothing else: the trust store of the system is never
/// used;</description></item>
/// <item><description>it is valid now, every certificate of its chain too, and allowed for the usage of its side (TLS server or client); no
/// revocation is checked and nothing is downloaded: the links run in a closed network, and a compromised certificate is refused by its
/// thumbprint;</description></item>
/// <item><description>its only DNS name is the one of the role expected, and it has no IP address;</description></item>
/// <item><description>its SHA-256 thumbprint is not in the refused list.</description></item>
/// </list>
/// <para>The reason of a refusal never quotes a text of the certificate (a peer chooses its subject), only the facts checked and the
/// thumbprint, so that a refusal can be logged without letting a peer write into the logs.</para>
/// </remarks>
public static class PeerCertificateValidator
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

    /// <summary>
    /// Method used to get the thumbprint a refused list holds for a certificate
    /// </summary>
    /// <param name="certificate">[Cannot be null] Certificate</param>
    /// <returns>
    /// Its SHA-256 thumbprint, 64 upper-case hexadecimal digits
    /// </returns>
    public static string GetThumbprint(X509Certificate2 certificate)
    {
        // Function's return
        return certificate.GetCertHashString(HashAlgorithmName.SHA256);
    }

    /// <summary>
    /// Method used to check the certificate of a peer
    /// </summary>
    /// <param name="certificate">Certificate presented by the peer; null when it presented none</param>
    /// <param name="expectedRole">Role the peer must have</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment</param>
    /// <param name="usageOid">[Cannot be null] Usage the certificate must allow: <see cref="PlatformIdentity.ServerAuthenticationOid"/> for the
    /// side that accepts, <see cref="PlatformIdentity.ClientAuthenticationOid"/> for the side that connects</param>
    /// <param name="trustedRoots">[Cannot be null or empty] Roots of the platform, the only ones trusted</param>
    /// <param name="refusedThumbprints">[Cannot be null] SHA-256 thumbprints refused, whatever their validity</param>
    /// <param name="now">Time at which the validity is checked</param>
    /// <returns>
    /// Null ==> The certificate is the one of the role expected<br/>
    /// Otherwise, why it is refused, without any text taken from the certificate
    /// </returns>
    /// <exception cref="ArgumentNullException">If the usage, the roots or the refused list is null</exception>
    /// <exception cref="ArgumentException">If no root is given, or the suffix is invalid</exception>
    public static string? Validate(X509Certificate? certificate, PlatformRole expectedRole, string suffix, string usageOid, X509Certificate2Collection trustedRoots,
        IReadOnlySet<string> refusedThumbprints, DateTimeOffset now)
    {
        // Checks
        if (usageOid is null)
            throw new ArgumentNullException(nameof(usageOid), $"The value of the parameter: {nameof(usageOid)} cannot be null");
        if (trustedRoots is null)
            throw new ArgumentNullException(nameof(trustedRoots), $"The value of the parameter: {nameof(trustedRoots)} cannot be null");
        if (trustedRoots.Count == 0)
            throw new ArgumentException("At least one trusted root is needed", nameof(trustedRoots));
        if (refusedThumbprints is null)
            throw new ArgumentNullException(nameof(refusedThumbprints), $"The value of the parameter: {nameof(refusedThumbprints)} cannot be null");
        string expectedName = PlatformIdentity.GetDnsName(expectedRole, suffix);

        // A peer without a certificate is refused
        if (certificate is null)
        {
            // Function's return
            return "the peer presented no certificate";
        }

        // Reading the certificate in its full form (the TLS layer may give the base type), released if it is a copy
        X509Certificate2? copy = certificate is X509Certificate2 ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
        try
        {
            X509Certificate2 peer = copy ?? (X509Certificate2)certificate;
            string thumbprint = GetThumbprint(peer);

            // Refusing a certificate of the refused list first: it may be valid in every other way
            if (refusedThumbprints.Contains(thumbprint))
            {
                // Function's return
                return $"the certificate {thumbprint} is in the refused list";
            }

            // Requiring the extended usages to be written: a chain accepts a certificate without them for any usage, and the authority of
            // the platform always writes them
            X509EnhancedKeyUsageExtension? usages = peer.Extensions.OfType<X509EnhancedKeyUsageExtension>().FirstOrDefault();
            if (usages is null || !usages.EnhancedKeyUsages.Cast<Oid>().Any(usage => usage.Value == usageOid))
            {
                // Function's return
                return $"the certificate {thumbprint} does not allow the usage of its side of the link";
            }

            // Building its chain on the roots of the platform alone, for the usage of its side
            using X509Chain chain = new X509Chain();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.DisableCertificateDownloads = true;
            chain.ChainPolicy.ApplicationPolicy.Add(new Oid(usageOid));
            chain.ChainPolicy.VerificationTime = now.UtcDateTime;
            chain.ChainPolicy.VerificationTimeIgnored = false;
            if (!chain.Build(peer))
            {
                string problems = string.Join(", ", chain.ChainStatus.Select(status => status.Status.ToString()).Distinct(StringComparer.Ordinal));

                // Function's return
                return $"the certificate {thumbprint} is not valid for this link ({problems})";
            }

            // Checking that it names the role expected, and only it
            X509SubjectAlternativeNameExtension? names = peer.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
            if (names is null || names.EnumerateIPAddresses().Any() || !names.EnumerateDnsNames().SequenceEqual(new string[] { expectedName }, StringComparer.Ordinal))
            {
                // Function's return
                return $"the certificate {thumbprint} is not the one of {expectedName}";
            }
        }
        finally
        {
            copy?.Dispose();
        }

        // Function's return
        return null;
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
