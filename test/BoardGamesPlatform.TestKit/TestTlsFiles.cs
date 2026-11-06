/*
 *	@file	test/BoardGamesPlatform.TestKit/TestTlsFiles.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Certificate files of the three roles and signing keys of the secure channel, written once per test run, and the settings that point to them
 */

// Using
using System.Security.Cryptography;
using BoardGamesPlatform.Protocol.Transport;
using BoardGamesPlatform.Protocol.Transport.Secure;
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Logging.Abstractions;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.TestKit;

/// <summary>
/// PFX of the Gateway, the Back and the Very Back, and the roots of the test authority, on disk
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a tier started as Program.cs starts it reads its certificate from files, as on a server; the tests that start a tier
/// give it these files, so that the links between the tiers run over mutual TLS exactly as delivered.</para>
/// <para><b>Behavior:</b> <see cref="Shared"/> is built on first use, in a temporary folder deleted when the test process ends. The files are
/// built at run time with the code of the authority: no private key is committed.</para>
/// </remarks>
public sealed class TestTlsFiles
{
    #region Members

    #region Constants

    /// <summary>
    /// Password of the PFX files of the tests
    /// </summary>
    public const string Password = "password-of-the-test-certificates";

    /// <summary>
    /// Password of the signing keys of the tests, distinct from the one of the PFX as a tier requires
    /// </summary>
    public const string SigningKeyPassword = "password-of-the-test-signing-keys";

    #endregion

    #region Variables

    /// <summary>
    /// Files shared by the tests of the process, built on first use
    /// </summary>
    private static readonly Lazy<TestTlsFiles> _shared = new Lazy<TestTlsFiles>(() => new TestTlsFiles(), LazyThreadSafetyMode.ExecutionAndPublication);

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
    /// Files shared by the tests of the process
    /// </summary>
    public static TestTlsFiles Shared => _shared.Value;

    /// <summary>
    /// Folder of the files
    /// </summary>
    public string Folder { get; }

    /// <summary>
    /// PEM file of the root of the tests
    /// </summary>
    public string TrustedRootsPath => Path.Combine(Folder, "platform-ca.crt");

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor
    /// </summary>
    private TestTlsFiles()
    {
        // Writing the root and the PFX of each role, the folder being deleted when the process ends
        Folder = Directory.CreateTempSubdirectory("bgp-test-tls-").FullName;
        using (TestCertificates certificates = new TestCertificates())
        {
            certificates.WriteTrustedRoots(TrustedRootsPath);
            foreach (PlatformRole role in new PlatformRole[] { PlatformRole.Gateway, PlatformRole.Back, PlatformRole.VeryBack })
                TestCertificates.WritePfx(certificates.Issue(role), GetCertificatePath(role), Password);
        }

        // Writing the signing key of each end of the secure channel, and its public key, the file of the peer of the other end
        foreach (PlatformRole role in new PlatformRole[] { PlatformRole.Back, PlatformRole.VeryBack })
        {
            using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(GetSigningKeyPath(role), key.ExportEncryptedPkcs8PrivateKeyPem(SigningKeyPassword,
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)));
            File.WriteAllText(GetPublicKeyPath(role), SigningPublicKey.FromKey(key).ToBase64() + Environment.NewLine);
        }
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try
            {
                Directory.Delete(Folder, recursive: true);
            }
            catch (IOException)
            {
                // A file still in use: the system cleans its temporary folder
            }
        };
    }

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
    /// Method used to get the PFX of a role
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The path of its PFX
    /// </returns>
    public string GetCertificatePath(PlatformRole role)
    {
        // Function's return
        return Path.Combine(Folder, $"{PlatformIdentity.GetRoleName(role)}.pfx");
    }

    /// <summary>
    /// Method used to get the signing key of an end of the secure channel
    /// </summary>
    /// <param name="role">The Back or the Very Back</param>
    /// <returns>
    /// The path of its encrypted PEM file
    /// </returns>
    public string GetSigningKeyPath(PlatformRole role)
    {
        // Function's return
        return Path.Combine(Folder, $"{PlatformIdentity.GetRoleName(role)}-signing.key");
    }

    /// <summary>
    /// Method used to get the public key of an end of the secure channel, the file of the public keys its peer pins
    /// </summary>
    /// <param name="role">The Back or the Very Back</param>
    /// <returns>
    /// The path of the file of its public key
    /// </returns>
    public string GetPublicKeyPath(PlatformRole role)
    {
        // Function's return
        return Path.Combine(Folder, $"{PlatformIdentity.GetRoleName(role)}-signing.pub");
    }

    /// <summary>
    /// Method used to get the "Tls" settings of a role, and the "Handshake" settings of the Back and the Very Back, to add to the settings in
    /// memory of a host
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The settings, by key
    /// </returns>
    public Dictionary<string, string?> GetSettings(PlatformRole role)
    {
        // The certificate of the role, then the keys of the secure channel when the role is an end of it
        Dictionary<string, string?> settings = new Dictionary<string, string?>
        {
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.IdentitySuffix)}"] = TestCertificates.Suffix,
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePath)}"] = GetCertificatePath(role),
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePassword)}"] = Password,
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.TrustedRootsPath)}"] = TrustedRootsPath,
        };
        if (role is PlatformRole.Back or PlatformRole.VeryBack)
        {
            PlatformRole peer = role == PlatformRole.Back ? PlatformRole.VeryBack : PlatformRole.Back;
            settings[$"{HandshakeOptions.SectionName}:{nameof(HandshakeOptions.SigningKeyPath)}"] = GetSigningKeyPath(role);
            settings[$"{HandshakeOptions.SectionName}:{nameof(HandshakeOptions.SigningKeyPassword)}"] = SigningKeyPassword;
            settings[$"{HandshakeOptions.SectionName}:{nameof(HandshakeOptions.PeerPublicKeysPath)}"] = GetPublicKeyPath(peer);
        }

        // Function's return
        return settings;
    }

    /// <summary>
    /// Method used to add the "Tls" settings of a role to settings in memory
    /// </summary>
    /// <param name="settings">[Cannot be null] Settings of the host</param>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The same settings, to chain the calls
    /// </returns>
    public Dictionary<string, string?> AddTo(Dictionary<string, string?> settings, PlatformRole role)
    {
        // Adding, a value already given by the test winning
        foreach (KeyValuePair<string, string?> setting in GetSettings(role))
            settings.TryAdd(setting.Key, setting.Value);

        // Function's return
        return settings;
    }

    /// <summary>
    /// Method used to build the connector a tier of a role uses towards its server, over mutual TLS
    /// </summary>
    /// <param name="clientRole">Role of the tier that connects</param>
    /// <param name="serverRole">Role of its server</param>
    /// <returns>
    /// The connector
    /// </returns>
    public ITransportConnector CreateConnector(PlatformRole clientRole, PlatformRole serverRole)
    {
        // Mutual TLS, then the secure channel on the Back → Very Back link, as the tiers do
        TlsCredentials credentials = LoadCredentials(clientRole);
        TlsTransportConnector tls = new TlsTransportConnector(new TcpTransportConnector(), credentials, serverRole, NullLogger.Instance);
        if (clientRole != PlatformRole.Back || serverRole != PlatformRole.VeryBack)
            return tls;

        // Function's return
        return new SecureTransportConnector(tls, LoadSigningKeys(PlatformRole.Back, credentials), NullLogger.Instance);
    }

    /// <summary>
    /// Method used to load the keys of an end of the secure channel from their files, as the tier does (for a fake tier of the tests)
    /// </summary>
    /// <param name="role">The Back or the Very Back</param>
    /// <param name="credentials">[Cannot be null] TLS credentials of the tier, whose key must differ from the signing key</param>
    /// <returns>
    /// The keys
    /// </returns>
    public SigningKeys LoadSigningKeys(PlatformRole role, TlsCredentials credentials)
    {
        // Function's return
        PlatformRole peer = role == PlatformRole.Back ? PlatformRole.VeryBack : PlatformRole.Back;
        return SigningKeys.FromFiles(GetSigningKeyPath(role), SigningKeyPassword, GetPublicKeyPath(peer), Password, () => credentials.Certificate, TimeProvider.System,
            NullLogger.Instance);
    }

    /// <summary>
    /// Method used to load the credentials of a role from its files, as the tier does (for a fake tier of the tests)
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The credentials
    /// </returns>
    public TlsCredentials LoadCredentials(PlatformRole role)
    {
        // Function's return
        return TlsCredentials.FromFiles(role, TestCertificates.Suffix, GetCertificatePath(role), Password, TrustedRootsPath, Array.Empty<string>(), TlsCredentials.DefaultExpiryWarning,
            TimeProvider.System, NullLogger.Instance);
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
