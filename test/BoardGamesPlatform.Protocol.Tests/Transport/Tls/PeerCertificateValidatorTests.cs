/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Tls/PeerCertificateValidatorTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks each rule of the check of a peer certificate on its own, without a connection
 */

// Using
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Tls;

/// <summary>
/// Tests used to check <see cref="PeerCertificateValidator"/>
/// </summary>
public sealed class PeerCertificateValidatorTests : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Authority of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// Empty refused list
    /// </summary>
    private static readonly IReadOnlySet<string> _noRefused = new HashSet<string>();

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
    /// Method used to check a certificate as the Back checks a client
    /// </summary>
    /// <param name="certificate">Certificate to check</param>
    /// <param name="refused">Refused list</param>
    /// <returns>
    /// The reason of the refusal, or null
    /// </returns>
    private string? CheckAsClientOfTheBack(X509Certificate? certificate, IReadOnlySet<string>? refused = null)
    {
        // Function's return
        return PeerCertificateValidator.Validate(certificate, PlatformRole.Gateway, TestCertificates.Suffix, PlatformIdentity.ClientAuthenticationOid, _certificates.TrustedRoots,
            refused ?? _noRefused, DateTimeOffset.UtcNow);
    }

    #endregion

    #region Publics

    /// <summary>
    /// The certificate of the Gateway is accepted as a client, and refused as a server (it has no server usage)
    /// </summary>
    [Fact]
    public void TheCertificateOfTheRoleIsAccepted()
    {
        // Issuing the certificate of the Gateway
        X509Certificate2 gateway = _certificates.Issue(PlatformRole.Gateway);

        // Checks
        CheckAsClientOfTheBack(gateway).ShouldBeNull();
        PeerCertificateValidator.Validate(gateway, PlatformRole.Gateway, TestCertificates.Suffix, PlatformIdentity.ServerAuthenticationOid, _certificates.TrustedRoots, _noRefused,
            DateTimeOffset.UtcNow).ShouldNotBeNull().ShouldContain("NotValidForUsage");
    }

    /// <summary>
    /// No certificate is refused
    /// </summary>
    [Fact]
    public void NoCertificateIsRefused()
    {
        // Check
        CheckAsClientOfTheBack(null).ShouldBe("the peer presented no certificate");
    }

    /// <summary>
    /// A certificate of another role, or of another deployment, is refused by its name
    /// </summary>
    [Fact]
    public void AnotherRoleIsRefusedByItsName()
    {
        // Issuing the certificate of the Back, which has the client usage too
        X509Certificate2 back = _certificates.Issue(PlatformRole.Back);

        // Checks
        CheckAsClientOfTheBack(back).ShouldNotBeNull().ShouldContain("is not the one of gateway.bgp.test");
        PeerCertificateValidator.Validate(back, PlatformRole.Back, "other.test", PlatformIdentity.ClientAuthenticationOid, _certificates.TrustedRoots, _noRefused, DateTimeOffset.UtcNow)
            .ShouldNotBeNull().ShouldContain("is not the one of back.other.test");
    }

    /// <summary>
    /// A certificate not yet valid, or no longer valid, at the time of the check is refused
    /// </summary>
    [Fact]
    public void TheValidityIsCheckedAtTheTimeGiven()
    {
        // Issuing a certificate valid for one month from yesterday
        DateTimeOffset now = DateTimeOffset.UtcNow;
        X509Certificate2 gateway = _certificates.Issue(PlatformRole.Gateway, now.AddDays(-1), now.AddDays(30));

        // Checks
        PeerCertificateValidator.Validate(gateway, PlatformRole.Gateway, TestCertificates.Suffix, PlatformIdentity.ClientAuthenticationOid, _certificates.TrustedRoots, _noRefused, now.AddDays(-2))
            .ShouldNotBeNull().ShouldContain("NotTimeValid");
        PeerCertificateValidator.Validate(gateway, PlatformRole.Gateway, TestCertificates.Suffix, PlatformIdentity.ClientAuthenticationOid, _certificates.TrustedRoots, _noRefused, now.AddDays(31))
            .ShouldNotBeNull().ShouldContain("NotTimeValid");
    }

    /// <summary>
    /// A refused thumbprint is refused, in any case, and the reason never quotes the certificate beyond its thumbprint
    /// </summary>
    [Fact]
    public void ARefusedThumbprintIsRefused()
    {
        // Issuing a certificate and refusing it
        X509Certificate2 gateway = _certificates.Issue(PlatformRole.Gateway);
        string thumbprint = PeerCertificateValidator.GetThumbprint(gateway);

        // Checks
        string reason = CheckAsClientOfTheBack(gateway, new HashSet<string> { thumbprint }).ShouldNotBeNull();
        reason.ShouldBe($"the certificate {thumbprint} is in the refused list");
        reason.ShouldNotContain(gateway.Subject);
    }

    /// <summary>
    /// The parameters are checked
    /// </summary>
    [Fact]
    public void TheParametersAreChecked()
    {
        // Checks
        Should.Throw<ArgumentNullException>(() => PeerCertificateValidator.Validate(null, PlatformRole.Gateway, TestCertificates.Suffix, null!, _certificates.TrustedRoots, _noRefused, DateTimeOffset.UtcNow));
        Should.Throw<ArgumentException>(() => PeerCertificateValidator.Validate(null, PlatformRole.Gateway, TestCertificates.Suffix, PlatformIdentity.ClientAuthenticationOid, new X509Certificate2Collection(), _noRefused, DateTimeOffset.UtcNow));
        Should.Throw<ArgumentException>(() => PeerCertificateValidator.Validate(null, PlatformRole.Gateway, "Bad Suffix", PlatformIdentity.ClientAuthenticationOid, _certificates.TrustedRoots, _noRefused, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Method used to release the certificates of the test
    /// </summary>
    public void Dispose()
    {
        // Releasing the authority and its certificates
        _certificates.Dispose();
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
