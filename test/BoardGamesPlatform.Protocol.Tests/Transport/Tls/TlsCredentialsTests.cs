/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Tls/TlsCredentialsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that a tier refuses to start with a certificate its peers would refuse, and takes a renewed certificate without restarting
 */

// Using
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Tls;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Tls;

/// <summary>
/// Tests used to check <see cref="TlsCredentials"/>
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> each test writes its PFX and its roots in its own temporary folder, deleted at the end.</para>
/// </remarks>
public sealed class TlsCredentialsTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Password of the PFX files of the tests
    /// </summary>
    private const string Password = "password-of-the-tests";

    #endregion

    #region Variables

    /// <summary>
    /// Authority of the test
    /// </summary>
    private readonly TestCertificates _certificates = new TestCertificates();

    /// <summary>
    /// Temporary folder of the test
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("bgp-tls-tests-").FullName;

    /// <summary>
    /// Logger of the tier
    /// </summary>
    private readonly CapturingLogger<TlsCredentialsTests> _logger = new CapturingLogger<TlsCredentialsTests>();

    /// <summary>
    /// Refused list holding a text that is no thumbprint
    /// </summary>
    private static readonly string[] _notAThumbprint = new string[] { "not-a-thumbprint" };

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
    /// PFX of the tier in the folder of the test
    /// </summary>
    private string PfxPath => Path.Combine(_folder, "back.pfx");

    /// <summary>
    /// Roots of the platform in the folder of the test
    /// </summary>
    private string RootsPath => Path.Combine(_folder, "platform-ca.crt");

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
    /// Method used to load the credentials of the Back from the files of the test
    /// </summary>
    /// <param name="refused">Thumbprints refused; none when null</param>
    /// <param name="clock">Clock of the checks; the system clock when null</param>
    /// <returns>
    /// The credentials
    /// </returns>
    private TlsCredentials Load(IEnumerable<string>? refused = null, TimeProvider? clock = null)
    {
        // Function's return
        return TlsCredentials.FromFiles(PlatformRole.Back, TestCertificates.Suffix, PfxPath, Password, RootsPath, refused ?? Array.Empty<string>(), TlsCredentials.DefaultExpiryWarning,
            clock ?? TimeProvider.System, _logger);
    }

    /// <summary>
    /// Method used to write a PFX, with a change time later than the previous one, as a renewal does
    /// </summary>
    /// <param name="certificate">[Cannot be null] Certificate with its key</param>
    /// <param name="password">[Cannot be null] Password of the PFX</param>
    private void Replace(X509Certificate2 certificate, string password)
    {
        // Writing the file, then moving its change time forward, since two writes may share the same tick of the file system
        DateTime before = File.Exists(PfxPath) ? File.GetLastWriteTimeUtc(PfxPath) : DateTime.MinValue;
        TestCertificates.WritePfx(certificate, PfxPath, password);
        File.SetLastWriteTimeUtc(PfxPath, before == DateTime.MinValue ? DateTime.UtcNow : before.AddSeconds(5));
    }

    #endregion

    #region Publics

    /// <summary>
    /// Valid files are loaded, and the certificate logged with its validity
    /// </summary>
    [Fact]
    public void ValidFilesAreLoaded()
    {
        // Writing the files of the Back
        X509Certificate2 back = _certificates.Issue(PlatformRole.Back);
        Replace(back, Password);
        _certificates.WriteTrustedRoots(RootsPath);

        // Loading them
        TlsCredentials credentials = Load();

        // Checks
        credentials.Role.ShouldBe(PlatformRole.Back);
        PeerCertificateValidator.GetThumbprint(credentials.Certificate).ShouldBe(PeerCertificateValidator.GetThumbprint(back));
        credentials.TrustedRoots.Count.ShouldBe(1);
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Information && entry.Message.Contains("Certificate of back.bgp.test loaded"));
        _logger.Entries.ShouldNotContain(entry => entry.Level == LogLevel.Warning);
    }

    /// <summary>
    /// A tier refuses to start with a certificate its peers would refuse, a wrong password or a missing file; the password never appears
    /// </summary>
    [Fact]
    public void AnUnusableCertificateStopsTheStartup()
    {
        // Writing the roots, then each wrong certificate in turn
        _certificates.WriteTrustedRoots(RootsPath);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using TestCertificates other = new TestCertificates("Other Root CA");

        // Checks: another role (the Gateway has no server usage; the Very Back has it, but not the name of the Back), another authority,
        // expired, a certificate refused, without the usage of a client
        Replace(_certificates.Issue(PlatformRole.Gateway), Password);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("NotValidForUsage");
        Replace(_certificates.Issue(PlatformRole.VeryBack), Password);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("not the one of back.bgp.test");
        Replace(other.Issue(PlatformRole.Back), Password);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("not valid for this link");
        Replace(_certificates.Issue(PlatformRole.Back, now.AddDays(-20), now.AddDays(-1)), Password);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("NotTimeValid");
        X509Certificate2 refused = _certificates.Issue(PlatformRole.Back);
        Replace(refused, Password);
        Should.Throw<InvalidOperationException>(() => Load(new string[] { PeerCertificateValidator.GetThumbprint(refused) })).Message.ShouldContain("refused list");
        Replace(_certificates.IssueWithUsages(PlatformRole.Back, PlatformIdentity.ServerAuthenticationOid), Password);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("NotValidForUsage");

        // Checks: a wrong password, a missing file, a refused thumbprint that is not one
        Replace(_certificates.Issue(PlatformRole.Back), "another-password");
        InvalidOperationException wrongPassword = Should.Throw<InvalidOperationException>(() => Load());
        wrongPassword.Message.ShouldContain("wrong password");
        wrongPassword.Message.ShouldNotContain(Password);
        File.Delete(RootsPath);
        Should.Throw<InvalidOperationException>(() => Load()).Message.ShouldContain("does not exist");
        Should.Throw<ArgumentException>(() => Load(_notAThumbprint));
    }

    /// <summary>
    /// A renewed PFX is taken before the next connection, without restarting the tier
    /// </summary>
    [Fact]
    public void ARenewedCertificateIsTakenWithoutRestart()
    {
        // Loading the first certificate, then renewing it
        Replace(_certificates.Issue(PlatformRole.Back), Password);
        _certificates.WriteTrustedRoots(RootsPath);
        TlsCredentials credentials = Load();
        X509Certificate2 renewed = _certificates.Issue(PlatformRole.Back);
        Replace(renewed, Password);

        // Taking the change
        credentials.ReloadIfChanged();

        // Check
        PeerCertificateValidator.GetThumbprint(credentials.Certificate).ShouldBe(PeerCertificateValidator.GetThumbprint(renewed));
    }

    /// <summary>
    /// A renewed PFX that fails the check is refused and logged, and the current certificate is kept
    /// </summary>
    [Fact]
    public void AWrongRenewalKeepsTheCurrentCertificate()
    {
        // Loading the certificate of the Back, then replacing it by the one of the Gateway
        X509Certificate2 back = _certificates.Issue(PlatformRole.Back);
        Replace(back, Password);
        _certificates.WriteTrustedRoots(RootsPath);
        TlsCredentials credentials = Load();
        Replace(_certificates.Issue(PlatformRole.Gateway), Password);

        // Taking the change
        credentials.ReloadIfChanged();

        // Checks
        PeerCertificateValidator.GetThumbprint(credentials.Certificate).ShouldBe(PeerCertificateValidator.GetThumbprint(back));
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("were refused; the current certificate is kept"));
    }

    /// <summary>
    /// A certificate that expires within the warning delay is logged as a warning
    /// </summary>
    [Fact]
    public void ACertificateThatExpiresSoonIsWarned()
    {
        // Writing a certificate that expires in 10 days
        DateTimeOffset now = DateTimeOffset.UtcNow;
        Replace(_certificates.Issue(PlatformRole.Back, now.AddDays(-1), now.AddDays(10)), Password);
        _certificates.WriteTrustedRoots(RootsPath);

        // Loading it
        Load(clock: new FakeTimeProvider(now));

        // Check
        _logger.Entries.ShouldContain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains("renew it"));
    }

    /// <summary>
    /// Method used to delete the folder of the test and release the certificates
    /// </summary>
    public void Dispose()
    {
        // Releasing everything
        _certificates.Dispose();
        Directory.Delete(_folder, recursive: true);
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
