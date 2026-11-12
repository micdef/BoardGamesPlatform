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
    /// Method used to give the owners of the vault of the tests
    /// </summary>
    /// <returns>
    /// SYSTEM, the Administrators, and the account that runs the tests: it owns the files it creates, since it is not elevated
    /// </returns>
    private static SecurityIdentifier[] Owners()
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
    /// Method used to give the account that plays the service in the tests: the group Users, distinct from the owners, so that its rights can be
    /// checked
    /// </summary>
    /// <returns>
    /// The security identifier of the group Users
    /// </returns>
    private static SecurityIdentifier Service()
    {
        // Function's return
        return new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
    }

    /// <summary>
    /// Method used to open the vault of the test, with the owners and the service of the tests
    /// </summary>
    /// <returns>
    /// The store of the folder of the test
    /// </returns>
    private DpapiVaultStore Store()
    {
        // Function's return
        return new DpapiVaultStore(_folder, Owners(), Service());
    }

    /// <summary>
    /// Method used to create the folder of the test with a protected access list, or to skip the test on other systems than Windows
    /// </summary>
    private void CreateFolder()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsWindows())
            Assert.Skip("DPAPI exists on Windows only");

        // Creating the folder, trusting only its owners
        DirectorySecurity security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        foreach (SecurityIdentifier account in Owners())
            security.AddAccessRule(new FileSystemAccessRule(account, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(_folder).Create(security);
    }

    /// <summary>
    /// Method used to give a right to an account on the file of a secret of the folder
    /// </summary>
    /// <param name="fileName">[Cannot be null] Name of the file</param>
    /// <param name="account">[Cannot be null] Account</param>
    /// <param name="rights">Rights given</param>
    private void GrantOnFile(string fileName, SecurityIdentifier account, FileSystemRights rights)
    {
        // Adding the rule
        FileInfo file = new FileInfo(Path.Combine(_folder, fileName));
        FileSecurity security = file.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(account, rights, AccessControlType.Allow));
        file.SetAccessControl(security);
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
        IReadOnlyDictionary<string, string> secrets = Store().ReadAll(_knownNames);

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
        VaultException exception = Should.Throw<VaultException>(() => Store().ReadAll(_knownNames));
        exception.Message.ShouldContain("cannot be decrypted");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A damaged file is refused: it does not decrypt
    /// </summary>
    [Fact]
    public void ADamagedFileIsRefused()
    {
        // Writing bytes that DPAPI did not write
        CreateFolder();
        File.WriteAllBytes(Path.Combine(_folder, "Tls__CertificatePassword"), new byte[] { 1, 2, 3, 4 });

        // Check
        Should.Throw<VaultException>(() => Store().ReadAll(_knownNames)).Message.ShouldContain("cannot be decrypted");
    }

    /// <summary>
    /// A file whose name is not a secret of the tier is refused, never quoting its value
    /// </summary>
    [Fact]
    public void AnUnknownFileIsRefused()
    {
        // Writing a secret of another tier
        CreateFolder();
        Write("Smtp__Password", "Smtp__Password", "Kx9secret");

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => Store().ReadAll(_knownNames));
        exception.Message.ShouldContain("not a secret of this tier");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A value with a line break, or with the byte order mark of Notepad, is refused, never quoting the value
    /// </summary>
    /// <param name="value">Value of the secret</param>
    [Theory]
    [InlineData("Kx9secret\r\n")]
    [InlineData("﻿Kx9secret")]
    public void AValueWithAControlOrFormatCharacterIsRefused(string value)
    {
        // Writing the value
        CreateFolder();
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", value);

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => Store().ReadAll(_knownNames));
        exception.Message.ShouldContain("control or format character");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A file larger than 64 KiB is refused before it is read
    /// </summary>
    [Fact]
    public void AFileLargerThan64KiBIsRefused()
    {
        // Writing a large file
        CreateFolder();
        File.WriteAllBytes(Path.Combine(_folder, "Tls__CertificatePassword"), new byte[(64 * 1024) + 1]);

        // Check
        Should.Throw<VaultException>(() => Store().ReadAll(_knownNames)).Message.ShouldContain("larger than");
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
        // Writing a secret, then opening the file or the folder to Everyone
        CreateFolder();
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", "Kx9secret");
        SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        if (onTheFolder)
        {
            DirectoryInfo folder = new DirectoryInfo(_folder);
            DirectorySecurity security = folder.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(everyone, FileSystemRights.CreateFiles, AccessControlType.Allow));
            folder.SetAccessControl(security);
        }
        else
        {
            GrantOnFile("Tls__CertificatePassword", everyone, FileSystemRights.Read);
        }

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => Store().ReadAll(_knownNames));
        exception.Message.ShouldContain("holds rights on");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// The account of the service may read a secret, but a right to write it is refused: a compromised service could otherwise rewrite its secrets
    /// and keep them after a restart
    /// </summary>
    /// <param name="rights">Rights of the service on the file</param>
    /// <param name="accepted">True when the vault must be read</param>
    [Theory]
    [InlineData(FileSystemRights.Read, true)]
    [InlineData(FileSystemRights.ReadAndExecute, true)]
    [InlineData(FileSystemRights.Write, false)]
    [InlineData(FileSystemRights.Modify, false)]
    [InlineData(FileSystemRights.ChangePermissions, false)]
    public void TheServiceMayOnlyReadItsSecrets(FileSystemRights rights, bool accepted)
    {
        // Writing a secret, then giving the rights to the account of the service
        CreateFolder();
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", "Kx9secret");
        GrantOnFile("Tls__CertificatePassword", Service(), rights);

        // Checks
        if (accepted)
            Store().ReadAll(_knownNames)["Tls__CertificatePassword"].ShouldBe("Kx9secret");
        else
            Should.Throw<VaultException>(() => Store().ReadAll(_knownNames)).Message.ShouldContain("more than read rights");
    }

    /// <summary>
    /// The public constructor, the one of the tiers, refuses a vault owned by another account than SYSTEM and the Administrators (run by a session
    /// that is not elevated, whose files belong to its own account)
    /// </summary>
    [Fact]
    public void AVaultOwnedByAnotherAccountIsRefused()
    {
        // Writing a secret in a folder owned by the account of the tests
        CreateFolder();
        using (WindowsIdentity current = WindowsIdentity.GetCurrent())
        {
            if (new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator))
                Assert.Skip("The session is elevated: its files belong to the Administrators");
        }
        Write("Tls__CertificatePassword", "Tls__CertificatePassword", "Kx9secret");

        // Check
        Should.Throw<VaultException>(() => new DpapiVaultStore(_folder).ReadAll(_knownNames)).Message.ShouldContain("must be SYSTEM or the Administrators");
    }

    /// <summary>
    /// A folder that holds a subfolder is refused: the vault holds the files of the secrets only
    /// </summary>
    [Fact]
    public void AFolderWithASubfolderIsRefused()
    {
        // Creating a subfolder
        CreateFolder();
        Directory.CreateDirectory(Path.Combine(_folder, "Tls__CertificatePassword"));

        // Check
        Should.Throw<VaultException>(() => Store().ReadAll(_knownNames)).Message.ShouldContain("subfolder");
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
        Should.Throw<VaultException>(() => Store().ReadAll(_knownNames));
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
