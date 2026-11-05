/*
 *	@file	test/BoardGamesPlatform.CertificateAuthority.Tests/CertificateIssuerTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks what the certificates of the platform authority hold, and the requests it refuses to sign
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
namespace BoardGamesPlatform.CertificateAuthority.Tests;

/// <summary>
/// Tests used to check <see cref="CertificateIssuer"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the tiers trust a certificate for what it holds: its name, its usages and its signer. These tests prove that the
/// authority writes exactly that, and takes nothing else from a request.</para>
/// </remarks>
public sealed class CertificateIssuerTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Suffix of the deployment used by the tests
    /// </summary>
    private const string Suffix = "bgp.test";

    #endregion

    #region Variables

    /// <summary>
    /// Start of the validity used by the tests
    /// </summary>
    private static readonly DateTimeOffset _now = new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero);

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
    /// Method used to create a root valid for ten years from the start of the tests
    /// </summary>
    /// <returns>
    /// The root with its key
    /// </returns>
    private static X509Certificate2 CreateRoot()
    {
        // Function's return
        return CertificateIssuer.CreateRoot("Test Root CA", _now.AddDays(-1), _now.AddYears(10));
    }

    /// <summary>
    /// Method used to sign a new request of a role for one year
    /// </summary>
    /// <param name="root">[Cannot be null] Root with its key</param>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The certificate of the tier
    /// </returns>
    private static X509Certificate2 SignRole(X509Certificate2 root, PlatformRole role)
    {
        // Requesting, then signing
        using ECDsa key = CertificateIssuer.CreateRequest(role, Suffix, out string requestPem);

        // Function's return
        return CertificateIssuer.Sign(root, requestPem, role, Suffix, _now, _now.AddDays(365));
    }

    /// <summary>
    /// Method used to read the usages of a certificate
    /// </summary>
    /// <param name="certificate">[Cannot be null] Certificate to read</param>
    /// <returns>
    /// The object identifiers of its extended key usages
    /// </returns>
    private static string[] GetUsages(X509Certificate2 certificate)
    {
        // Function's return
        return certificate.Extensions.OfType<X509EnhancedKeyUsageExtension>().Single().EnhancedKeyUsages.Cast<Oid>()
            .Select(oid => oid.Value ?? string.Empty).Order(StringComparer.Ordinal).ToArray();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The root is a self-signed authority that may sign end certificates only
    /// </summary>
    [Fact]
    public void TheRootSignsEndCertificatesOnly()
    {
        // Creating a root
        using X509Certificate2 root = CreateRoot();

        // Checks
        root.HasPrivateKey.ShouldBeTrue();
        root.Subject.ShouldBe(root.Issuer);
        X509BasicConstraintsExtension constraints = root.Extensions.OfType<X509BasicConstraintsExtension>().Single();
        constraints.CertificateAuthority.ShouldBeTrue();
        constraints.HasPathLengthConstraint.ShouldBeTrue();
        constraints.PathLengthConstraint.ShouldBe(0);
        constraints.Critical.ShouldBeTrue();
        root.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages.ShouldBe(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign);
        using ECDsa? key = root.GetECDsaPublicKey();
        key.ShouldNotBeNull().KeySize.ShouldBe(256);
    }

    /// <summary>
    /// The certificate of each role holds its name only, the usages of its side, no authority right, and chains to the root
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <param name="usages">Usages expected, sorted</param>
    [Theory]
    [InlineData(PlatformRole.Gateway, new string[] { PlatformIdentity.ClientAuthenticationOid })]
    [InlineData(PlatformRole.Back, new string[] { PlatformIdentity.ServerAuthenticationOid, PlatformIdentity.ClientAuthenticationOid })]
    [InlineData(PlatformRole.VeryBack, new string[] { PlatformIdentity.ServerAuthenticationOid })]
    public void ACertificateHoldsTheIdentityOfItsRole(PlatformRole role, string[] usages)
    {
        // Signing a request of the role
        using X509Certificate2 root = CreateRoot();
        using X509Certificate2 certificate = SignRole(root, role);
        string dnsName = PlatformIdentity.GetDnsName(role, Suffix);

        // Checks: the name, the usages, no authority right, no private key, a positive serial of 16 bytes
        certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false).ShouldBe(dnsName);
        certificate.Subject.ShouldBe($"CN={dnsName}");
        X509SubjectAlternativeNameExtension names = certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>().Single();
        names.EnumerateDnsNames().ShouldBe(new string[] { dnsName });
        names.EnumerateIPAddresses().ShouldBeEmpty();
        GetUsages(certificate).ShouldBe(usages.Order(StringComparer.Ordinal).ToArray());
        certificate.Extensions.OfType<X509BasicConstraintsExtension>().Single().CertificateAuthority.ShouldBeFalse();
        certificate.Extensions.OfType<X509KeyUsageExtension>().Single().KeyUsages.ShouldBe(X509KeyUsageFlags.DigitalSignature);
        certificate.HasPrivateKey.ShouldBeFalse();
        certificate.SerialNumberBytes.Length.ShouldBe(16);
        certificate.Issuer.ShouldBe(root.Subject);

        // Check: the chain builds on the root alone
        using X509Chain chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = _now.AddDays(1).UtcDateTime;
        chain.Build(certificate).ShouldBeTrue(string.Join("; ", chain.ChainStatus.Select(status => status.StatusInformation)));
    }

    /// <summary>
    /// Two certificates never share a serial number
    /// </summary>
    [Fact]
    public void TheSerialNumbersDiffer()
    {
        // Signing two requests of the same role
        using X509Certificate2 root = CreateRoot();
        using X509Certificate2 first = SignRole(root, PlatformRole.Back);
        using X509Certificate2 second = SignRole(root, PlatformRole.Back);

        // Check
        first.SerialNumber.ShouldNotBe(second.SerialNumber);
    }

    /// <summary>
    /// A request whose signature does not match its key is refused: the requester must hold the private key
    /// </summary>
    [Fact]
    public void ATamperedRequestIsRefused()
    {
        // Changing one byte of the body of a request
        using X509Certificate2 root = CreateRoot();
        using ECDsa key = CertificateIssuer.CreateRequest(PlatformRole.Back, Suffix, out string requestPem);
        byte[] der = PemEncoding.Find(requestPem) is PemFields fields ? Convert.FromBase64String(requestPem[fields.Base64Data]) : Array.Empty<byte>();
        der[^20] ^= 0x01;
        string tampered = PemEncoding.WriteString("CERTIFICATE REQUEST", der);

        // Check
        Should.Throw<CryptographicException>(() => CertificateIssuer.Sign(root, tampered, PlatformRole.Back, Suffix, _now, _now.AddDays(30)));
    }

    /// <summary>
    /// A request of one role is never signed for another one: the mistake is found before a certificate exists
    /// </summary>
    [Fact]
    public void ARequestOfAnotherRoleIsRefused()
    {
        // Requesting for the Gateway
        using X509Certificate2 root = CreateRoot();
        using ECDsa key = CertificateIssuer.CreateRequest(PlatformRole.Gateway, Suffix, out string requestPem);

        // Checks: neither another role nor another suffix
        Should.Throw<ArgumentException>(() => CertificateIssuer.Sign(root, requestPem, PlatformRole.VeryBack, Suffix, _now, _now.AddDays(30)));
        Should.Throw<ArgumentException>(() => CertificateIssuer.Sign(root, requestPem, PlatformRole.Gateway, "other.test", _now, _now.AddDays(30)));
    }

    /// <summary>
    /// A request with a key other than ECDSA P-256 is refused
    /// </summary>
    [Fact]
    public void ARequestWithAnotherKindOfKeyIsRefused()
    {
        // Requesting with an RSA key, named after the role
        using X509Certificate2 root = CreateRoot();
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new CertificateRequest($"CN={PlatformIdentity.GetDnsName(PlatformRole.Back, Suffix)}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        // Check
        Should.Throw<ArgumentException>(() => CertificateIssuer.Sign(root, request.CreateSigningRequestPem(), PlatformRole.Back, Suffix, _now, _now.AddDays(30)));
    }

    /// <summary>
    /// A validity that is empty, too long, or beyond the root is refused, and so is an authority without its key
    /// </summary>
    [Fact]
    public void AnInvalidValidityOrAuthorityIsRefused()
    {
        // Preparing a request and a root valid for two years only
        using X509Certificate2 root = CertificateIssuer.CreateRoot("Short Root CA", _now.AddDays(-1), _now.AddYears(2));
        using ECDsa key = CertificateIssuer.CreateRequest(PlatformRole.Back, Suffix, out string requestPem);
        using X509Certificate2 publicRoot = X509CertificateLoader.LoadCertificate(root.RawData);

        // Checks
        Should.Throw<ArgumentOutOfRangeException>(() => CertificateIssuer.Sign(root, requestPem, PlatformRole.Back, Suffix, _now, _now));
        Should.Throw<ArgumentOutOfRangeException>(() => CertificateIssuer.Sign(root, requestPem, PlatformRole.Back, Suffix, _now, _now.AddDays(CertificateIssuer.MaxValidityDays + 1)));
        Should.Throw<ArgumentOutOfRangeException>(() => CertificateIssuer.Sign(root, requestPem, PlatformRole.Back, Suffix, _now.AddYears(2).AddDays(-30), _now.AddYears(2).AddDays(30)));
        Should.Throw<ArgumentException>(() => CertificateIssuer.Sign(publicRoot, requestPem, PlatformRole.Back, Suffix, _now, _now.AddDays(30)));
        Should.Throw<ArgumentOutOfRangeException>(() => CertificateIssuer.CreateRoot("Root", _now, _now.AddYears(CertificateIssuer.MaxRootValidityYears + 1)));
        Should.Throw<ArgumentException>(() => CertificateIssuer.CreateRoot(" ", _now, _now.AddYears(1)));
    }

    /// <summary>
    /// A certificate of a tier cannot sign another one: it is no authority
    /// </summary>
    [Fact]
    public void ACertificateOfATierCannotSign()
    {
        // Joining the certificate of the Back with its key, then using it as an authority
        using X509Certificate2 root = CreateRoot();
        using ECDsa key = CertificateIssuer.CreateRequest(PlatformRole.Back, Suffix, out string requestPem);
        using X509Certificate2 back = CertificateIssuer.Sign(root, requestPem, PlatformRole.Back, Suffix, _now, _now.AddDays(30));
        using X509Certificate2 backWithKey = back.CopyWithPrivateKey(key);
        using ECDsa otherKey = CertificateIssuer.CreateRequest(PlatformRole.Gateway, Suffix, out string otherPem);

        // Check
        Should.Throw<ArgumentException>(() => CertificateIssuer.Sign(backWithKey, otherPem, PlatformRole.Gateway, Suffix, _now, _now.AddDays(10)));
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
