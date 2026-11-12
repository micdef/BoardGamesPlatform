/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/DpapiVaultStoreTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the vault of a Windows service: DPAPI files, bound to their names, behind access lists that only trust the service
 */

// Using
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Tests used to check <see cref="DpapiVaultStore"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> with DPAPI LocalMachine, the access lists are the protection of the secrets (any process of the machine can decrypt a file
/// it can read and write a valid one): a vault whose lists trust another account must stop the startup.</para>
/// <para><b>Behavior:</b> each test makes a temporary folder whose access list is protected (no inheritance) and trusts SYSTEM, the
/// Administrators and the account that runs the tests, which plays the account of the service; the files are encrypted by
/// <see cref="DpapiVaultStore.Protect"/>. Skipped on other systems than Windows.</para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class DpapiVaultStoreTests : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// The names the tier of the tests knows
    /// </summary>
    private static readonly HashSet<string> _knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tls__CertificatePassword", "Handshake__SigningKeyPassword" };

    /// <summary>
    /// Temporary folder of the vault of a test
    /// </summary>
    private readonly string _folder;

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

    /// <summary>
    /// Constructor used to create the protected folder of a test, on Windows
    /// </summary>
    public DpapiVaultStoreTests()
    {
        // Setting the folder (created by the first step of each test, which skips on other systems)
        _folder = Path.Combine(Path.GetTempPath(), $"BoardGamesPlatform.VaultTests.{Guid.NewGuid():N}");
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to give the accounts trusted with the vault of the tests
    /// </summary>
    /// <returns>
    /// SYSTEM, the Administrators and the account that runs the tests
    /// </returns>
    private static SecurityIdentifier[] TrustedAccounts()
    {
        // Function's return
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        return new SecurityIdentifier[]
        {
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            current.User!,
        };
    }

    /// <summary>
    /// Method used to create the folder of the test with a protected access list, or to skip the test on other systems than Windows
    /// </summary>
    private void CreateFolder()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsWindows())
            Assert.Skip("DPAPI exists on Windows only");

        // Creating the folder, trusting only SYSTEM, the Administrators and the account of the tests
        DirectorySecurity security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (SecurityIdentifier account in TrustedAccounts())
            security.AddAccessRule(new FileSystemAccessRule(account, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(_folder).Create(security);
    }

    /// <summary>
    /// Method used to write a secret as the vault stores it
    /// </summary>
    /// <param name="fileName">[Cannot be null] Name of the file</param>
    /// <param name="encryptedFor">[Cannot be null] Name the secret is encrypted for (the name of the file, unless the test renames it)</param>
    /// <param name="value">[Cannot be null] Value of the secret</param>
    private void Write(string fileName, string encryptedFor, string value)
    {
        // Writing the encrypted bytes (the file inherits the protected list of the folder)
        File.WriteAllBytes(Path.Combine(_folder, fileName), DpapiVaultStore.Protect(encryptedFor, value));
    }

    /// <summary>
    /// Method used to give a right to the group Users on a file or on the folder
    /// </summary>
    /// <param name="security">[Cannot be null] Rights to change</param>
    /// <param name="rights">Rights given to the group Users</param>
    private static void GrantUsers(FileSystemSecurity security, FileSystemRights rights)
    {
        // Adding the rule
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), rights, AccessControlType.Allow));
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to delete the folder of the test
    /// </summary>
    public void Dispose()
    {
        // Deleting the folder and what it holds
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// The secrets of a folder that only trusts the service are read under their names
    /// </summary>
    [Fact]
    public void TheSecretsAreReadUnderTheirNames()
    {
        // Writing two secrets
        CreateFolder();
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", "pässwörd;'\"");
        Write("Handshake__SigningKeyPassword", "Handshake__SigningKeyPassword", "other");

        // Reading them
        IReadOnlyDictionary<string, string> secrets = new DpapiVaultStore(_folder).ReadAll(_knownNames);

        // Checks
        secrets.Count.ShouldBe(2);
        secrets["Tls__CertificatePassword"].ShouldBe("pässwörd;'\"");
        secrets["Handshake__SigningKeyPassword"].ShouldBe("other");
    }

    /// <summary>
    /// A secret copied under another name does not decrypt: the entropy is the name
    /// </summary>
    [Fact]
    public void ASecretCopiedUnderAnotherNameIsRefused()
    {
        // Writing the secret of the handshake under the name of the TLS password
        CreateFolder();
        Write("Tls__CertificatePassword", "Handshake__SigningKeyPassword", "Kx9secret");

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => new DpapiVaultStore(_folder).ReadAll(_knownNames));
        exception.Message.ShouldContain("cannot be decrypted");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A damaged file, an unknown file, a value with a line break and a file larger than 64 KiB are refused, never quoting a value
    /// </summary>
    /// <param name="scenario">The case of the test</param>
    [Theory]
    [InlineData("damaged")]
    [InlineData("unknown")]
    [InlineData("line break")]
    [InlineData("too large")]
    public void AFileThatIsNotASecretOfTheTierIsRefused(string scenario)
    {
        // Writing the file of the case
        CreateFolder();
        string path = Path.Combine(_folder, "Tls__CertificatePassword");
        switch (scenario)
        {
            case "damaged":
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
                break;
            case "unknown":
                Write("Smtp__Password", "Smtp__Password", "Kx9secret");
                break;
            case "line break":
                Write("Tls__CertificatePassword", "Tls__CertificatePassword", "Kx9secret\r\n");
                break;
            default:
                File.WriteAllBytes(path, new byte[(64 * 1024) + 1]);
                break;
        }

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => new DpapiVaultStore(_folder).ReadAll(_knownNames));
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A file that another account may read is refused, and so is a folder in which another account may write
    /// </summary>
    /// <param name="onTheFolder">True to open the folder to writes, false to open the file to reads</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AVaultOpenToAnotherAccountIsRefused(bool onTheFolder)
    {
        // Writing a secret, then opening the file or the folder to the group Users
        CreateFolder();
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", "Kx9secret");
        if (onTheFolder)
        {
            DirectoryInfo folder = new DirectoryInfo(_folder);
            DirectorySecurity security = folder.GetAccessControl();
            GrantUsers(security, FileSystemRights.CreateFiles);
            folder.SetAccessControl(security);
        }
        else
        {
            FileInfo file = new FileInfo(Path.Combine(_folder, "Tls__CertificatePassword"));
            FileSecurity security = file.GetAccessControl();
            GrantUsers(security, FileSystemRights.Read);
            file.SetAccessControl(security);
        }

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => new DpapiVaultStore(_folder).ReadAll(_knownNames));
        exception.Message.ShouldContain("holds rights on");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A missing folder stops the reading, and a relative folder is refused at once
    /// </summary>
    [Fact]
    public void AMissingOrRelativeFolderIsRefused()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsWindows())
            Assert.Skip("DPAPI exists on Windows only");

        // Checks
        Should.Throw<VaultException>(() => new DpapiVaultStore(_folder).ReadAll(_knownNames));
        Should.Throw<ArgumentException>(() => new DpapiVaultStore("relative\\folder"));
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
