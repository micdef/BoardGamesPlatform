/*
 *	@file	src/BoardGamesPlatform.Vault/DpapiVaultStore.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Vault of a Windows service: one file per secret, encrypted with DPAPI, readable by the service and the administrators only
 */

// Using
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Store used to read the secrets of a Windows service from a folder of files encrypted with DPAPI
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> on Windows, each secret is a file of the folder named by the setting <c>Vault:Path</c>, written by
/// <c>tools/vault/Set-VaultSecret.ps1</c>: its name is the name of the secret, its content the value encrypted with DPAPI, scope
/// <c>LocalMachine</c> (decided with the security review: a virtual account of a service cannot log on to write a <c>CurrentUser</c> secret).</para>
/// <para><b>Behavior:</b> with <c>LocalMachine</c>, any process of the machine can decrypt a file it can read and write a valid one, so the access
/// lists are the protection, checked before any value is read:</para>
/// <list type="bullet">
/// <item><description>the owner of the folder and of each file is SYSTEM, the Administrators or the account of the service;</description></item>
/// <item><description>no other account may write in the folder (create, delete, change a file, its rights or its owner);</description></item>
/// <item><description>no other account has any right on a file; the rights of a file are read on the handle that reads it, so that the file cannot
/// be swapped between the check and the read.</description></item>
/// </list>
/// <para>The account of the service may own and write its own secrets: it can already read them all, so writing them gives it nothing more. The
/// DPAPI entropy is the name of the file, so a file copied under another name does not decrypt. A file of more than 64 KiB is refused before it
/// is read.</para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class DpapiVaultStore : IVaultStore
{
    #region Members

    #region Constants

    /// <summary>
    /// Prefix of the DPAPI entropy, followed by the name of the secret
    /// </summary>
    private const string EntropyPrefix = "BoardGamesPlatform.Vault:";

    /// <summary>
    /// Largest file of a secret, in bytes (a password or a connection string is far smaller)
    /// </summary>
    private const long MaxFileLength = 64 * 1024;

    /// <summary>
    /// The rights on the folder that let an account change the secrets
    /// </summary>
    private const FileSystemRights FolderWriteRights = FileSystemRights.CreateFiles | FileSystemRights.AppendData | FileSystemRights.WriteAttributes
        | FileSystemRights.WriteExtendedAttributes | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.Delete
        | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;

    #endregion

    #region Variables

    /// <summary>
    /// The accounts that may own and write the vault, and read a secret: SYSTEM, the Administrators, and the account of the service
    /// </summary>
    private readonly SecurityIdentifier[] _trustedAccounts;

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
    /// Gets the folder of the secrets
    /// </summary>
    public string Location { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to read the secrets of a folder, as the account of the current process
    /// </summary>
    /// <param name="directory">[Cannot be null] Folder of the secrets (setting Vault:Path), an absolute path</param>
    /// <exception cref="ArgumentException">If the folder is empty or not an absolute path</exception>
    public DpapiVaultStore(string directory)
    {
        // Checks
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The folder of the vault (Vault:Path) must be an absolute path", nameof(directory));

        // Setting the folder and the trusted accounts
        Location = directory;
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        _trustedAccounts = new SecurityIdentifier[]
        {
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            current.User ?? throw new InvalidOperationException("The account of the current process has no security identifier"),
        };
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to know whether an account is trusted with the vault
    /// </summary>
    /// <param name="identity">[Can be null] Account of an access rule or of an owner</param>
    /// <returns>
    /// True  ==> SYSTEM, the Administrators or the account of the service
    /// False ==> any other account
    /// </returns>
    private bool IsTrusted(IdentityReference? identity)
    {
        // Function's return
        return identity is SecurityIdentifier sid && _trustedAccounts.Contains(sid);
    }

    /// <summary>
    /// Method used to check the owner and the rules of a folder or a file
    /// </summary>
    /// <param name="security">[Cannot be null] Rights of the folder or the file</param>
    /// <param name="forbiddenRights">Rights that no other account may hold</param>
    /// <param name="what">[Cannot be null] Folder or file, for the message</param>
    /// <exception cref="VaultException">If the owner is not trusted, or another account holds one of the forbidden rights</exception>
    private void CheckRights(FileSystemSecurity security, FileSystemRights forbiddenRights, string what)
    {
        // Checking the owner
        if (!IsTrusted(security.GetOwner(typeof(SecurityIdentifier))))
            throw new VaultException($"The owner of {what} must be SYSTEM, the Administrators or the account of the service (see tools/vault/Set-VaultSecret.ps1)");

        // Checking every rule that allows something, except those that only pass to the children of a folder
        foreach (FileSystemAccessRule rule in security.GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier)))
        {
            if (rule.AccessControlType != AccessControlType.Allow || rule.PropagationFlags.HasFlag(PropagationFlags.InheritOnly))
                continue;
            if (!IsTrusted(rule.IdentityReference) && (rule.FileSystemRights & forbiddenRights) != 0)
                throw new VaultException($"The account {rule.IdentityReference.Value} holds rights on {what}: only SYSTEM, the Administrators and the account of the service may (see tools/vault/Set-VaultSecret.ps1)");
        }
    }

    /// <summary>
    /// Method used to read and decrypt one secret, after checking the rights of its file on the handle that reads it
    /// </summary>
    /// <param name="file">[Cannot be null] Path of the file</param>
    /// <param name="name">[Cannot be null] Name of the secret (the name of the file)</param>
    /// <returns>
    /// The text of the secret
    /// </returns>
    /// <exception cref="VaultException">If the rights of the file are too wide, the file too large, or the secret cannot be decrypted</exception>
    private string ReadSecret(string file, string name)
    {
        // Opening the file once: the rights checked are those of the file read
        using FileStream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        CheckRights(stream.GetAccessControl(), (FileSystemRights)(-1), $"the secret {name} of the vault {Location}");
        if (stream.Length > MaxFileLength)
            throw new VaultException($"The secret {name} of the vault {Location} is larger than {MaxFileLength} bytes: it is not a file of the vault");

        // Reading the encrypted bytes
        byte[] encrypted = new byte[stream.Length];
        stream.ReadExactly(encrypted);

        // Decrypting with the entropy bound to the name, then decoding (which erases the clear bytes)
        byte[] clear;
        try
        {
            clear = ProtectedData.Unprotect(encrypted, Encoding.UTF8.GetBytes(EntropyPrefix + name), DataProtectionScope.LocalMachine);
        }
        catch (CryptographicException exception)
        {
            throw new VaultException($"The secret {name} of the vault {Location} cannot be decrypted: written on another machine, under another name, or damaged", exception);
        }

        // Function's return
        return VaultText.Decode(clear, name, Location);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to encrypt a secret as the vault stores it (used by the tests; the administrators use tools/vault/Set-VaultSecret.ps1)
    /// </summary>
    /// <param name="name">[Cannot be null] Name of the secret in the vault ("Tls__CertificatePassword")</param>
    /// <param name="value">[Cannot be null] Value of the secret</param>
    /// <returns>
    /// The encrypted bytes, the content of the file of the secret
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public static byte[] Protect(string name, string value)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        // Encrypting, then erasing the clear bytes
        byte[] clear = Encoding.UTF8.GetBytes(value);
        try
        {
            // Function's return
            return ProtectedData.Protect(clear, Encoding.UTF8.GetBytes(EntropyPrefix + name), DataProtectionScope.LocalMachine);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(clear);
        }
    }

    /// <summary>
    /// Method used to read every secret of the folder
    /// </summary>
    /// <param name="knownNames">[Cannot be null] Names in the vault of the secrets the tier knows, ignoring the case</param>
    /// <returns>
    /// The secrets, by name in the vault
    /// </returns>
    /// <exception cref="VaultException">If the folder is missing or too open, holds an unknown secret, or a secret it cannot read safely</exception>
    public IReadOnlyDictionary<string, string> ReadAll(IReadOnlySet<string> knownNames)
    {
        // Checks: the folder exists, and no other account can change what it holds
        ArgumentNullException.ThrowIfNull(knownNames);
        DirectoryInfo folder = new DirectoryInfo(Location);
        if (!folder.Exists)
            throw new VaultException($"The folder of the vault {Location} does not exist (Vault:Path)");
        CheckRights(folder.GetAccessControl(), FolderWriteRights, $"the folder of the vault {Location}");

        // Reading every secret; an unknown one is a mistake of the installation, never ignored
        Dictionary<string, string> secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (FileInfo file in folder.EnumerateFiles())
        {
            if (!knownNames.Contains(file.Name))
                throw new VaultException($"The file {file.Name} of the vault {Location} is not a secret of this tier: remove it");
            secrets[file.Name] = ReadSecret(file.FullName, file.Name);
        }

        // Function's return
        return secrets;
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
