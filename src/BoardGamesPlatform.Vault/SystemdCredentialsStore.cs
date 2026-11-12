/*
 *	@file	src/BoardGamesPlatform.Vault/SystemdCredentialsStore.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Vault of a systemd unit on Fedora: the credentials that systemd decrypted for the service
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault;

/// <summary>
/// Store used to read the credentials that systemd gives to a service (LoadCredentialEncrypted=)
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> on Fedora, each secret is encrypted by <c>systemd-creds encrypt --name=&lt;name&gt;</c> (key of the host, and of the TPM2
/// when present) and named by the unit with <c>LoadCredentialEncrypted=&lt;name&gt;:&lt;file&gt;</c>; systemd checks the embedded name, decrypts
/// the secret at start into a read-only, non-swappable folder readable by root and by the user of the service, and names that folder in the
/// variable <c>CREDENTIALS_DIRECTORY</c>.</para>
/// <para><b>Behavior:</b> reads every file of that folder: its name is the name of the secret, its content the value, as given (no line break is
/// trimmed: one is refused). A file whose name the tier does not know stops the startup. The protection of the folder is the work of systemd;
/// the store does not check it.</para>
/// </remarks>
public sealed class SystemdCredentialsStore : IVaultStore
{
    #region Members

    #region Constants

    /// <summary>
    /// Variable in which systemd names the folder of the credentials of the service
    /// </summary>
    public const string DirectoryVariable = "CREDENTIALS_DIRECTORY";

    #endregion

    #region Variables

    // #N/A

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
    /// Gets the folder of the credentials
    /// </summary>
    public string Location { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor used to read the credentials of a folder
    /// </summary>
    /// <param name="directory">[Cannot be null] Folder of the credentials, as systemd names it in <see cref="DirectoryVariable"/></param>
    /// <exception cref="ArgumentException">If the folder is empty or not an absolute path</exception>
    public SystemdCredentialsStore(string directory)
    {
        // Checks
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException($"The folder of the credentials must be an absolute path, as systemd gives it in {DirectoryVariable}", nameof(directory));

        // Setting
        Location = directory;
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
    /// Method used to read every credential of the folder
    /// </summary>
    /// <param name="knownNames">[Cannot be null] Names in the vault of the secrets the tier knows, ignoring the case</param>
    /// <returns>
    /// The secrets, by name in the vault
    /// </returns>
    /// <exception cref="VaultException">If the folder is missing, holds an unknown credential, or a value that is not a valid secret</exception>
    public IReadOnlyDictionary<string, string> ReadAll(IReadOnlySet<string> knownNames)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(knownNames);
        if (!Directory.Exists(Location))
            throw new VaultException($"The folder of the credentials {Location} does not exist ({DirectoryVariable})");
        if (Directory.EnumerateDirectories(Location).Any())
            throw new VaultException($"The folder of the credentials {Location} holds a subfolder: it may only hold the credentials of the unit");

        // Reading every credential; an unknown one is a mistake of the unit, never ignored
        Dictionary<string, string> secrets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(Location))
        {
            string name = Path.GetFileName(file);
            if (!knownNames.Contains(name))
                throw new VaultException($"The credential {name} of {Location} is not a secret of this tier: remove it from the unit (LoadCredentialEncrypted=)");
            secrets[name] = VaultText.Decode(File.ReadAllBytes(file), name, Location);
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
