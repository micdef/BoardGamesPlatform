/*
 *	@file	src/BoardGamesPlatform.CertificateAuthority/CertificateIssuer.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Builds the root of the platform authority, the certificate requests of the tiers, and the certificates it signs for them
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Tls;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.CertificateAuthority;

/// <summary>
/// Certificates of the platform: the root of its authority, the requests of the tiers and the certificates signed for them
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> one place that decides what a certificate of the platform holds, used by the command-line tool and by the tests, which
/// build their certificates at run time with the same code (no private key is ever committed).</para>
/// <para><b>Behavior:</b> every key is ECDSA on the NIST P-256 curve, signed with SHA-256, supported by Windows and Linux alike.</para>
/// <list type="bullet">
/// <item><description>The root is a self-signed authority that may sign end certificates only (path length 0), never another authority.</description></item>
/// <item><description>A tier makes its own key and sends a request (PKCS#10): its private key never leaves its machine.</description></item>
/// <item><description>The authority keeps only the public key of a request, and its signature as the proof that the requester holds the private
/// key. Everything else is decided here, never taken from the request: the name of the role, the usages, the validity. A request whose name is
/// not the one of the role signed is refused, so that a mistake of the operator is found before a certificate exists.</description></item>
/// </list>
/// </remarks>
public static class CertificateIssuer
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest validity of a certificate of a tier, in days: the limit of the public authorities (CA/Browser Forum), so that a key is renewed
    /// at least once a year and a stolen one does not stay valid for long
    /// </summary>
    public const int MaxValidityDays = 397;

    /// <summary>
    /// Longest validity of the root, in years
    /// </summary>
    public const int MaxRootValidityYears = 20;

    /// <summary>
    /// Object identifier of the common name (CN) in a distinguished name
    /// </summary>
    private const string CommonNameOid = "2.5.4.3";

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
    /// Method used to build a random serial number of 16 bytes, positive, as RFC 5280 requires
    /// </summary>
    /// <returns>
    /// The serial number, big-endian
    /// </returns>
    private static byte[] CreateSerialNumber()
    {
        // Drawing random bits; the first byte is kept between 0x40 and 0x7F, so that the number stays positive and its DER encoding minimal
        byte[] serial = RandomNumberGenerator.GetBytes(16);
        serial[0] = (byte)((serial[0] & 0x3F) | 0x40);

        // Function's return
        return serial;
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to create the root of the platform authority
    /// </summary>
    /// <param name="commonName">[Cannot be null or blank] Common name of the authority, for example "BoardGamesPlatform Root CA"</param>
    /// <param name="notBefore">Start of the validity</param>
    /// <param name="notAfter">End of the validity; after the start, at most <see cref="MaxRootValidityYears"/> years later</param>
    /// <returns>
    /// The root certificate with its private key, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the name is null</exception>
    /// <exception cref="ArgumentException">If the name is blank</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the validity is empty or too long</exception>
    public static X509Certificate2 CreateRoot(string commonName, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        // Checks
        if (commonName is null)
            throw new ArgumentNullException(nameof(commonName), $"The value of the parameter: {nameof(commonName)} cannot be null");
        if (string.IsNullOrWhiteSpace(commonName))
            throw new ArgumentException($"The value of the parameter: {nameof(commonName)} cannot be blank", nameof(commonName));
        if (notAfter <= notBefore || notAfter > notBefore.AddYears(MaxRootValidityYears))
            throw new ArgumentOutOfRangeException(nameof(notAfter), $"The validity of the root must be longer than zero and at most {MaxRootValidityYears} years");

        // Describing an authority that signs end certificates only
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        X500DistinguishedNameBuilder subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(commonName);
        CertificateRequest request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, critical: true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        // Function's return
        return request.CreateSelfSigned(notBefore, notAfter);
    }

    /// <summary>
    /// Method used to create the key of a tier and its certificate request
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment (see <see cref="PlatformIdentity.IsValidSuffix"/>)</param>
    /// <param name="requestPem">The request, PEM-encoded, to send to the authority</param>
    /// <returns>
    /// The new private key, which stays on the machine of the tier; owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">If the role is not one of the platform</exception>
    /// <exception cref="ArgumentException">If the suffix is invalid</exception>
    public static ECDsa CreateRequest(PlatformRole role, string suffix, out string requestPem)
    {
        // Naming the request after the role; the authority decides the rest
        string dnsName = PlatformIdentity.GetDnsName(role, suffix);
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        try
        {
            X500DistinguishedNameBuilder subject = new X500DistinguishedNameBuilder();
            subject.AddCommonName(dnsName);
            CertificateRequest request = new CertificateRequest(subject.Build(), key, HashAlgorithmName.SHA256);
            requestPem = request.CreateSigningRequestPem();
        }
        catch
        {
            key.Dispose();
            throw;
        }

        // Function's return
        return key;
    }

    /// <summary>
    /// Method used to sign the certificate request of a tier
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> the signature of the request is checked first: it proves that the requester holds the private key. The
    /// certificate then gets the name of the role as its only subject alternative name and as its common name, the usages of the role, no
    /// authority right, and the validity given, within the one of the root. Nothing else is taken from the request.</para>
    /// </remarks>
    /// <param name="authority">[Cannot be null] Root of the platform, with its private key</param>
    /// <param name="requestPem">[Cannot be null] Request of the tier, PEM-encoded</param>
    /// <param name="role">Role of the tier</param>
    /// <param name="suffix">[Cannot be invalid] Suffix of the deployment</param>
    /// <param name="notBefore">Start of the validity, not before the one of the root</param>
    /// <param name="notAfter">End of the validity: after the start, at most <see cref="MaxValidityDays"/> days later and not after the root</param>
    /// <returns>
    /// The certificate of the tier, without private key, owned by the caller, who disposes it
    /// </returns>
    /// <exception cref="ArgumentNullException">If the authority or the request is null</exception>
    /// <exception cref="ArgumentException">If the authority has no private key or is not an authority, if the suffix is invalid, if the request
    /// is not one of this role, or if its key is not ECDSA P-256</exception>
    /// <exception cref="ArgumentOutOfRangeException">If the role is unknown, or the validity is empty, too long, or outside the one of the root</exception>
    /// <exception cref="CryptographicException">If the request is malformed, or its signature does not match its key</exception>
    public static X509Certificate2 Sign(X509Certificate2 authority, string requestPem, PlatformRole role, string suffix, DateTimeOffset notBefore, DateTimeOffset notAfter)
    {
        // Checks
        if (authority is null)
            throw new ArgumentNullException(nameof(authority), $"The value of the parameter: {nameof(authority)} cannot be null");
        if (requestPem is null)
            throw new ArgumentNullException(nameof(requestPem), $"The value of the parameter: {nameof(requestPem)} cannot be null");
        if (!authority.HasPrivateKey)
            throw new ArgumentException("The authority must hold its private key to sign", nameof(authority));
        if (authority.Extensions.OfType<X509BasicConstraintsExtension>().FirstOrDefault() is not { CertificateAuthority: true })
            throw new ArgumentException("The certificate given as authority is not an authority", nameof(authority));
        string dnsName = PlatformIdentity.GetDnsName(role, suffix);
        if (notAfter <= notBefore || notAfter > notBefore.AddDays(MaxValidityDays))
            throw new ArgumentOutOfRangeException(nameof(notAfter), $"The validity of a certificate must be longer than zero and at most {MaxValidityDays} days");
        if (notBefore < authority.NotBefore || notAfter > authority.NotAfter)
            throw new ArgumentOutOfRangeException(nameof(notAfter), "The validity of a certificate must stay within the validity of the root");

        // Reading the request, which checks that its signature matches its key
        CertificateRequest received = CertificateRequest.LoadSigningRequestPem(requestPem, HashAlgorithmName.SHA256);
        using (ECDsa? requestKey = received.PublicKey.GetECDsaPublicKey())
        {
            if (requestKey is null || requestKey.KeySize != 256)
                throw new ArgumentException("The key of the request must be ECDSA on the P-256 curve", nameof(requestPem));
        }
        List<X500RelativeDistinguishedName> names = received.SubjectName.EnumerateRelativeDistinguishedNames().ToList();
        if (names.Count != 1 || names[0].HasMultipleElements || names[0].GetSingleElementType().Value != CommonNameOid || names[0].GetSingleElementValue() != dnsName)
            throw new ArgumentException($"The request is not one of the role signed: its name must be the common name {dnsName} only", nameof(requestPem));

        // Building the certificate from the public key of the request only
        X500DistinguishedNameBuilder subject = new X500DistinguishedNameBuilder();
        subject.AddCommonName(dnsName);
        CertificateRequest request = new CertificateRequest(subject.Build(), received.PublicKey, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: false, hasPathLengthConstraint: false, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, critical: true));
        OidCollection usages = new OidCollection();
        if (PlatformIdentity.IsServer(role))
            usages.Add(new Oid(PlatformIdentity.ServerAuthenticationOid));
        if (PlatformIdentity.IsClient(role))
            usages.Add(new Oid(PlatformIdentity.ClientAuthenticationOid));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, critical: false));
        SubjectAlternativeNameBuilder alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName(dnsName);
        request.CertificateExtensions.Add(alternativeNames.Build(critical: false));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(authority, includeKeyIdentifier: true, includeIssuerAndSerial: false));

        // Function's return
        return request.Create(authority, notBefore, notAfter, CreateSerialNumber());
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
