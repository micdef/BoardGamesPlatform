/*
 *	@file	test/BoardGamesPlatform.TestKit/TestTlsFiles.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Certificate files of the three roles, written once per test run, and the "Tls" settings that point to them
 */

// Using
using BoardGamesPlatform.Protocol.Transport;
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
    /// Method used to get the "Tls" settings of a role, to add to the settings in memory of a host
    /// </summary>
    /// <param name="role">Role of the tier</param>
    /// <returns>
    /// The settings, by key
    /// </returns>
    public Dictionary<string, string?> GetSettings(PlatformRole role)
    {
        // Function's return
        return new Dictionary<string, string?>
        {
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.IdentitySuffix)}"] = TestCertificates.Suffix,
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePath)}"] = GetCertificatePath(role),
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.CertificatePassword)}"] = Password,
            [$"{TlsOptions.SectionName}:{nameof(TlsOptions.TrustedRootsPath)}"] = TrustedRootsPath,
        };
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
        // Function's return
        return new TlsTransportConnector(new TcpTransportConnector(), LoadCredentials(clientRole), serverRole, NullLogger.Instance);
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
