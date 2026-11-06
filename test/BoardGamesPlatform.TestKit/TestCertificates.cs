/*
 *	@file	test/BoardGamesPlatform.TestKit/TestCertificates.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Certificates of the platform built at run time for the tests: a root, the certificate of each role, and wrong ones on demand
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.CertificateAuthority;
using BoardGamesPlatform.Protocol.Transport.Tls;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// Authority of the tests, and the certificates it issues
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> no private key is ever committed: every test builds its certificates when it runs, with the code of the authority of
/// the platform, so that the tests check the certificates the tiers will really get.</para>
/// <para><b>Behavior:</b> the root is valid from 30 days ago for ten years, so that a test can issue a certificate that already expired. The
/// certificates issued keep their key in memory only.</para>
/// </remarks>
public sealed class TestCertificates : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Suffix of the deployment of the tests
    /// </summary>
    public const string Suffix = "bgp.test";

    #endregion

    #region Variables

    /// <summary>
    /// Certificates issued, released with the authority
    /// </summary>
    private readonly List<X509Certificate2> _issued = new List<X509Certificate2>();

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
    /// Root of the tests, with its key
    /// </summary>
    public X509Certificate2 Root { get; }

    /// <summary>
    /// Roots trusted by the tiers of the tests: the root, without its key
    /// </summary>
    public X509Certificate2Collection TrustedRoots { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="name">Common name of the root</param>
    public TestCertificates(string name = "Test Root CA")
    {
        // Creating the root, and the public copy the tiers trust
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Root = CertificateIssuer.CreateRoot(name, now.AddDays(-30), now.AddYears(10));
        TrustedRoots = new X509Certificate2Collection(X509CertificateLoader.LoadCertificate(Root.RawData));
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to give a certificate joined to an ephemeral key a key that TLS can use on every system
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> on Windows, the TLS layer (Schannel) cannot use a key that only lives in memory; the certificate goes through a PFX
    /// and is loaded back, as a tier loads its own, but exportable, so that a test can write it to a file (a tier never exports its key).</para>
    /// </remarks>
    /// <param name="joined">[Cannot be null] Certificate joined to its key; released by this method</param>
    /// <returns>
    /// The certificate with a key usable by TLS
    /// </returns>
    private static X509Certificate2 ToUsable(X509Certificate2 joined)
    {
        // Going through a PFX, the key being kept as a tier would keep it
        using (joined)
        {
            // Function's return
            return X509CertificateLoader.LoadPkcs12(joined.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, "transit"), "transit", X509KeyStorageFlags.Exportable);
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to issue the certificate of a role, with its key
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="notBefore">Start of the validity; one day ago when null</param>
    /// <param name="notAfter">End of the validity; in one year when null</param>
    /// <returns>
    /// The certificate with its key, released with the authority
    /// </returns>
    public X509Certificate2 Issue(PlatformRole role, DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        // Requesting, signing, then joining the key
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using ECDsa key = CertificateIssuer.CreateRequest(role, Suffix, out string requestPem);
        using X509Certificate2 certificate = CertificateIssuer.Sign(Root, requestPem, role, Suffix, notBefore ?? now.AddDays(-1), notAfter ?? now.AddDays(365));
        X509Certificate2 joined = ToUsable(certificate.CopyWithPrivateKey(key));
        _issued.Add(joined);

        // Function's return
        return joined;
    }

    /// <summary>
    /// Method used to issue, outside the rules of the authority, a certificate named after a role but with the usages given (to test a wrong one)
    /// </summary>
    /// <param name="role">Role whose name the certificate takes</param>
    /// <param name="usageOids">[Cannot be null] Usages of the certificate</param>
    /// <returns>
    /// The certificate with its key, released with the authority
    /// </returns>
    public X509Certificate2 IssueWithUsages(PlatformRole role, params string[] usageOids)
    {
        // Building the certificate as the authority would, except for its usages
        string dnsName = PlatformIdentity.GetDnsName(role, Suffix);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CertificateRequest request = new CertificateRequest($"CN={dnsName}", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, true));
        OidCollection usages = new OidCollection();
        foreach (string usage in usageOids)
            usages.Add(new Oid(usage));
        if (usages.Count > 0)
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(usages, false));
        SubjectAlternativeNameBuilder names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(dnsName);
        request.CertificateExtensions.Add(names.Build());
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using X509Certificate2 certificate = request.Create(Root, now.AddDays(-1), now.AddDays(30), RandomNumberGenerator.GetBytes(16).Select((value, index) => index == 0 ? (byte)((value & 0x3F) | 0x40) : value).ToArray());
        X509Certificate2 joined = ToUsable(certificate.CopyWithPrivateKey(key));
        _issued.Add(joined);

        // Function's return
        return joined;
    }

    /// <summary>
    /// Method used to build the credentials of a tier with a new certificate of its role
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="refusedThumbprints">Thumbprints the tier refuses; none when null</param>
    /// <returns>
    /// The credentials
    /// </returns>
    public TlsCredentials CredentialsFor(PlatformRole role, IEnumerable<string>? refusedThumbprints = null)
    {
        // Function's return
        return TlsCredentials.FromCertificates(role, Suffix, Issue(role), TrustedRoots, refusedThumbprints ?? Array.Empty<string>(), TimeProvider.System);
    }

    /// <summary>
    /// Method used to write a certificate with its key to a PFX file
    /// </summary>
    /// <param name="certificate">[Cannot be null] Certificate with its key</param>
    /// <param name="path">[Cannot be null] File to write, replaced if it exists</param>
    /// <param name="password">[Cannot be null] Password of the PFX</param>
    public static void WritePfx(X509Certificate2 certificate, string path, string password)
    {
        // Writing the PFX as the authority tool does
        File.WriteAllBytes(path, certificate.ExportPkcs12(Pkcs12ExportPbeParameters.Pbes2Aes256Sha256, password));
    }

    /// <summary>
    /// Method used to write the trusted roots to a PEM file
    /// </summary>
    /// <param name="path">[Cannot be null] File to write, replaced if it exists</param>
    public void WriteTrustedRoots(string path)
    {
        // Writing the public root as the authority tool does
        File.WriteAllText(path, Root.ExportCertificatePem());
    }

    /// <summary>
    /// Method used to release the root and the certificates issued
    /// </summary>
    public void Dispose()
    {
        // Releasing everything built
        foreach (X509Certificate2 certificate in _issued)
            certificate.Dispose();
        foreach (X509Certificate2 root in TrustedRoots)
            root.Dispose();
        Root.Dispose();
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
