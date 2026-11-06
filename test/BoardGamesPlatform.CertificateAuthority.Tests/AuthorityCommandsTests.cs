/*
 *	@file	test/BoardGamesPlatform.CertificateAuthority.Tests/AuthorityCommandsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the commands of the certificate authority tool, from the root to the PFX a tier loads
 */

// Using
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Time.Testing;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.CertificateAuthority.Tests;

/// <summary>
/// Tests used to check <see cref="AuthorityCommands"/>
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> each test works in its own temporary folder, deleted at the end, with passwords given by a fake source instead of the
/// console.</para>
/// </remarks>
public sealed class AuthorityCommandsTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Password of the root key in the tests
    /// </summary>
    private const string AuthorityPassword = "root-password-of-the-tests";

    /// <summary>
    /// Password of the key and of the PFX of a tier in the tests
    /// </summary>
    private const string CertificatePassword = "tier-password-of-the-tests";

    /// <summary>
    /// Suffix of the deployment used by the tests
    /// </summary>
    private const string Suffix = "bgp.test";

    #endregion

    #region Variables

    /// <summary>
    /// Temporary folder of the test
    /// </summary>
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"bgp-ca-tests-{Guid.NewGuid():N}");

    /// <summary>
    /// Passwords given to the tool, by environment variable
    /// </summary>
    private readonly Dictionary<string, string> _passwords = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { AuthorityCommands.AuthorityPasswordVariable, AuthorityPassword },
        { AuthorityCommands.CertificatePasswordVariable, CertificatePassword }
    };

    /// <summary>
    /// Results written by the tool
    /// </summary>
    private readonly StringWriter _output = new StringWriter();

    /// <summary>
    /// Errors written by the tool
    /// </summary>
    private readonly StringWriter _error = new StringWriter();

    /// <summary>
    /// Clock of the tool
    /// </summary>
    private readonly FakeTimeProvider _clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 5, 10, 0, 0, TimeSpan.Zero));

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
    /// Method used to run one command of the tool
    /// </summary>
    /// <param name="args">[Cannot be null] Command and options</param>
    /// <returns>
    /// The exit code of the command
    /// </returns>
    private int Run(params string[] args)
    {
        // Function's return
        return new AuthorityCommands(new FakePasswordSource(_passwords), _output, _error, _clock).Run(args);
    }

    /// <summary>
    /// Method used to build a path in the folder of the test
    /// </summary>
    /// <param name="parts">[Cannot be null] Parts of the path below the folder</param>
    /// <returns>
    /// The full path
    /// </returns>
    private string InFolder(params string[] parts)
    {
        // Function's return
        return Path.Combine(new string[] { _folder }.Concat(parts).ToArray());
    }

    /// <summary>
    /// Method used to run every step of the issue of a certificate for the Back
    /// </summary>
    /// <returns>
    /// The path of the PFX of the Back
    /// </returns>
    private string IssueBack()
    {
        // Running the steps, each one on its own folder as on separate machines
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(0, _error.ToString());
        Run("request", "--dir", InFolder("back"), "--role", "back", "--suffix", Suffix).ShouldBe(0, _error.ToString());
        Run("sign", "--dir", InFolder("ca"), "--request", InFolder("back", "back.csr"), "--role", "back", "--suffix", Suffix, "--out", InFolder("back", "back.crt"))
            .ShouldBe(0, _error.ToString());
        Run("install", "--key", InFolder("back", "back.key"), "--certificate", InFolder("back", "back.crt"), "--authority", InFolder("ca", AuthorityCommands.AuthorityCertificateFile),
            "--out", InFolder("back", "back.pfx")).ShouldBe(0, _error.ToString());

        // Function's return
        return InFolder("back", "back.pfx");
    }

    #endregion

    #region Publics

    /// <summary>
    /// The steps from the root to the PFX give a PFX that holds the key and the certificate of the role, chained to the root, and the issue is
    /// recorded
    /// </summary>
    [Fact]
    public void TheStepsGiveAPfxOfTheRole()
    {
        // Issuing the certificate of the Back
        string pfx = IssueBack();

        // Checks: the PFX opens with its password, holds the key, and the certificate is the one of the Back, chained to the root
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(pfx, CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        certificate.HasPrivateKey.ShouldBeTrue();
        certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false).ShouldBe(PlatformIdentity.GetDnsName(PlatformRole.Back, Suffix));
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(InFolder("ca", AuthorityCommands.AuthorityCertificateFile));
        certificate.Issuer.ShouldBe(root.Subject);
        root.HasPrivateKey.ShouldBeFalse();

        // Checks: the issue is recorded and listed, with the thumbprint of the certificate
        string[] issued = File.ReadAllLines(InFolder("ca", AuthorityCommands.IssuedFile));
        issued[0].ShouldBe(AuthorityCommands.IssuedHeader);
        issued.Length.ShouldBe(2);
        issued[1].ShouldContain(certificate.GetCertHashString(HashAlgorithmName.SHA256));
        issued[1].ShouldContain(",back,back.bgp.test,");
        Run("list", "--dir", InFolder("ca")).ShouldBe(0);
        _output.ToString().ShouldContain(certificate.SerialNumber);
    }

    /// <summary>
    /// The root and the PFX of a tier are encrypted with 300 000 iterations of key derivation, far above the 2 000 of the default export, and
    /// still load with the default limits of .NET
    /// </summary>
    [Fact]
    public void ThePfxFilesUseAStrongKeyDerivation()
    {
        // Issuing the certificate of the Back
        string pfx = IssueBack();

        // Checks: both PFX load with the default limits, and not with a limit just below 300 000 iterations
        Pkcs12LoaderLimits below = new Pkcs12LoaderLimits { MacIterationLimit = 299_999 };
        foreach ((string path, string password) in new (string, string)[] { (pfx, CertificatePassword), (InFolder("ca", AuthorityCommands.AuthorityKeyFile), AuthorityPassword) })
        {
            using (X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet))
            {
                // Loaded with the default limits
            }
            Should.Throw<Pkcs12LoadLimitExceededException>(() => X509CertificateLoader.LoadPkcs12FromFile(path, password, X509KeyStorageFlags.EphemeralKeySet, below));
        }
    }

    /// <summary>
    /// A certificate is valid from 5 minutes before it is signed, to the second, so that a tier whose clock is a little late accepts it; the
    /// root too
    /// </summary>
    [Fact]
    public void ACertificateIsValidFromALittleBeforeItIsSigned()
    {
        // Issuing the certificate of the Back at a time with a fraction of a second
        _clock.Advance(TimeSpan.FromMilliseconds(1234));
        string pfx = IssueBack();

        // Checks
        DateTimeOffset expected = new DateTimeOffset(2026, 11, 5, 9, 55, 1, TimeSpan.Zero);
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12FromFile(pfx, CertificatePassword, X509KeyStorageFlags.EphemeralKeySet);
        using X509Certificate2 root = X509CertificateLoader.LoadCertificateFromFile(InFolder("ca", AuthorityCommands.AuthorityCertificateFile));
        new DateTimeOffset(certificate.NotBefore.ToUniversalTime(), TimeSpan.Zero).ShouldBe(expected);
        new DateTimeOffset(root.NotBefore.ToUniversalTime(), TimeSpan.Zero).ShouldBe(expected);
        new DateTimeOffset(certificate.NotAfter.ToUniversalTime(), TimeSpan.Zero).ShouldBe(expected.AddDays(AuthorityCommands.DefaultValidityDays));
    }

    /// <summary>
    /// When the register cannot be written, no certificate is written either: a certificate always appears in the register
    /// </summary>
    [Fact]
    public void ACertificateIsNeverWrittenWithoutItsLineInTheRegister()
    {
        // Creating a root and a request, then making the register impossible to write (a folder of its name)
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(0, _error.ToString());
        Run("request", "--dir", InFolder("back"), "--role", "back", "--suffix", Suffix).ShouldBe(0, _error.ToString());
        Directory.CreateDirectory(InFolder("ca", AuthorityCommands.IssuedFile));

        // Checks: the command fails, and no certificate exists
        Run("sign", "--dir", InFolder("ca"), "--request", InFolder("back", "back.csr"), "--role", "back", "--suffix", Suffix, "--out", InFolder("back", "back.crt"))
            .ShouldBe(1);
        File.Exists(InFolder("back", "back.crt")).ShouldBeFalse();
    }

    /// <summary>
    /// A file that is not a certificate is refused at install with its name
    /// </summary>
    [Fact]
    public void AFileThatIsNotACertificateIsNamedAtInstall()
    {
        // Issuing up to the request, then giving a text file as the certificate
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(0, _error.ToString());
        Run("request", "--dir", InFolder("back"), "--role", "back", "--suffix", Suffix).ShouldBe(0, _error.ToString());
        File.WriteAllText(InFolder("back", "back.crt"), "not a certificate");

        // Checks
        Run("install", "--key", InFolder("back", "back.key"), "--certificate", InFolder("back", "back.crt"), "--authority", InFolder("ca", AuthorityCommands.AuthorityCertificateFile),
            "--out", InFolder("back", "back.pfx")).ShouldBe(1);
        _error.ToString().ShouldContain(InFolder("back", "back.crt"));
        File.Exists(InFolder("back", "back.pfx")).ShouldBeFalse();
    }
    /// <summary>
    /// The key of the tier is written encrypted, and the root key never leaves its PFX
    /// </summary>
    [Fact]
    public void TheKeysAreWrittenEncrypted()
    {
        // Creating a root and a request
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(0, _error.ToString());
        Run("request", "--dir", InFolder("gateway"), "--role", "gateway", "--suffix", Suffix).ShouldBe(0, _error.ToString());

        // Checks: an encrypted PKCS#8 key, which opens with its password only; the public certificate of the root holds no key
        string key = File.ReadAllText(InFolder("gateway", "gateway.key"));
        key.ShouldStartWith("-----BEGIN ENCRYPTED PRIVATE KEY-----");
        using ECDsa opened = ECDsa.Create();
        Should.Throw<CryptographicException>(() => opened.ImportFromEncryptedPem(key, "wrong-password-here"));
        opened.ImportFromEncryptedPem(key, CertificatePassword);
        File.ReadAllText(InFolder("ca", AuthorityCommands.AuthorityCertificateFile)).ShouldNotContain("PRIVATE KEY");
        Should.Throw<CryptographicException>(() => X509CertificateLoader.LoadPkcs12FromFile(InFolder("ca", AuthorityCommands.AuthorityKeyFile), "wrong-password-here"));
    }

    /// <summary>
    /// On Linux, a file that holds a private key is readable by its owner only
    /// </summary>
    [Fact]
    public void APrivateFileIsReadableByItsOwnerOnly()
    {
        // Check that the system has Unix permissions
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file permissions do not exist on Windows: the folder of the authority is protected by its ACL");

        // Issuing the certificate of the Back
        string pfx = IssueBack();

        // Checks (the second guard tells the platform analyzer what the skip already ensures)
        if (OperatingSystem.IsWindows())
            return;
        UnixFileMode privateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        File.GetUnixFileMode(InFolder("ca", AuthorityCommands.AuthorityKeyFile)).ShouldBe(privateMode);
        File.GetUnixFileMode(InFolder("back", "back.key")).ShouldBe(privateMode);
        File.GetUnixFileMode(pfx).ShouldBe(privateMode);
    }

    /// <summary>
    /// No command overwrites a file: a second init leaves the root untouched
    /// </summary>
    [Fact]
    public void NoFileIsOverwritten()
    {
        // Creating a root, then trying again
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(0, _error.ToString());
        byte[] before = File.ReadAllBytes(InFolder("ca", AuthorityCommands.AuthorityKeyFile));

        // Checks
        Run("init", "--dir", InFolder("ca"), "--name", "Other Root CA").ShouldBe(2);
        _error.ToString().ShouldContain("already exists");
        File.ReadAllBytes(InFolder("ca", AuthorityCommands.AuthorityKeyFile)).ShouldBe(before);
    }

    /// <summary>
    /// A missing or short password is refused before anything is written
    /// </summary>
    [Fact]
    public void AMissingOrShortPasswordIsRefused()
    {
        // Without a password, then with a short one
        _passwords.Remove(AuthorityCommands.AuthorityPasswordVariable);
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(2);
        _passwords[AuthorityCommands.AuthorityPasswordVariable] = "short";
        Run("init", "--dir", InFolder("ca"), "--name", "Test Root CA").ShouldBe(2);

        // Checks
        _error.ToString().ShouldContain("No password");
        _error.ToString().ShouldContain($"at least {AuthorityCommands.MinPasswordLength} characters");
        Directory.Exists(InFolder("ca")).ShouldBeFalse();
    }

    /// <summary>
    /// A certificate of another authority is refused at install, and no PFX is written
    /// </summary>
    [Fact]
    public void ACertificateOfAnotherAuthorityIsRefusedAtInstall()
    {
        // Issuing the Back with one root, then creating another root
        IssueBack();
        Run("init", "--dir", InFolder("other"), "--name", "Other Root CA").ShouldBe(0, _error.ToString());

        // Checks
        Run("install", "--key", InFolder("back", "back.key"), "--certificate", InFolder("back", "back.crt"), "--authority", InFolder("other", AuthorityCommands.AuthorityCertificateFile),
            "--out", InFolder("back", "other.pfx")).ShouldBe(1);
        File.Exists(InFolder("back", "other.pfx")).ShouldBeFalse();
    }

    /// <summary>
    /// A key that is not the one of the certificate is refused at install
    /// </summary>
    [Fact]
    public void AKeyOfAnotherRequestIsRefusedAtInstall()
    {
        // Issuing the Back, then making a second request whose key is not the one of the certificate
        IssueBack();
        Run("request", "--dir", InFolder("second"), "--role", "back", "--suffix", Suffix).ShouldBe(0, _error.ToString());

        // Checks
        Run("install", "--key", InFolder("second", "back.key"), "--certificate", InFolder("back", "back.crt"), "--authority", InFolder("ca", AuthorityCommands.AuthorityCertificateFile),
            "--out", InFolder("second", "back.pfx")).ShouldBe(1);
        File.Exists(InFolder("second", "back.pfx")).ShouldBeFalse();
    }

    /// <summary>
    /// A wrong command line is answered with the help
    /// </summary>
    /// <param name="commandLine">Command line, its arguments separated by spaces</param>
    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("init --dir")]
    [InlineData("init --dir x --dir y --name n")]
    [InlineData("request --dir x --role admin --suffix bgp.test")]
    [InlineData("request --dir x --role back --suffix Bad_Suffix")]
    [InlineData("sign --dir x --request r --role back --suffix bgp.test --days 398 --out o")]
    [InlineData("init --dir x --name n --password visible")]
    public void AWrongCommandLineIsAnsweredWithTheHelp(string commandLine)
    {
        // Running the command line
        int exitCode = Run(commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        // Checks
        exitCode.ShouldBe(2);
        _error.ToString().ShouldContain("Usage:");
        Directory.Exists(_folder).ShouldBeFalse();
    }

    /// <summary>
    /// Method used to delete the folder of the test
    /// </summary>
    public void Dispose()
    {
        // Deleting what the test wrote
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
        _output.Dispose();
        _error.Dispose();
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

    /// <summary>
    /// Password source of the tests: the passwords given by variable, never a console
    /// </summary>
    /// <param name="passwords">Passwords by environment variable</param>
    private sealed class FakePasswordSource(Dictionary<string, string> passwords) : IPasswordSource
    {
        /// <inheritdoc/>
        public string? Read(string environmentVariable, string prompt, bool isNew)
        {
            // Function's return
            return passwords.TryGetValue(environmentVariable, out string? password) ? password : null;
        }
    }
}
