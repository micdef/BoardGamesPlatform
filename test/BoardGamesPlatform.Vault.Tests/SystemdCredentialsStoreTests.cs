/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/SystemdCredentialsStoreTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the reading of the credentials that systemd gives to a service
 */

// Using
using System.Diagnostics;
using System.Text;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Tests used to check <see cref="SystemdCredentialsStore"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the store reads the folder systemd fills (CREDENTIALS_DIRECTORY): each file is a secret, its name the name of the
/// secret.</para>
/// <para><b>Behavior:</b> plays that folder with a temporary folder of plain files, as systemd leaves them after their decryption; it runs on every
/// system, since the store only reads files. On Linux, one test also encrypts and decrypts a secret with systemd-creds (key of the user); the
/// decryption by a unit of the system (LoadCredentialEncrypted=) is checked by hand in the WSL FedoraLinux-44 (doc/adm/configuration.md).</para>
/// </remarks>
public sealed class SystemdCredentialsStoreTests : IDisposable
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// The names the tier of the tests knows
    /// </summary>
    private static readonly HashSet<string> _knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Tls__CertificatePassword", "ConnectionStrings__Main" };

    /// <summary>
    /// Temporary folder that plays the folder of the credentials
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
    /// Constructor used to create the temporary folder of a test
    /// </summary>
    public SystemdCredentialsStoreTests()
    {
        // Setting
        _folder = Path.Combine(Path.GetTempPath(), $"BoardGamesPlatform.VaultTests.{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to write a credential as systemd leaves it
    /// </summary>
    /// <param name="name">[Cannot be null] Name of the credential</param>
    /// <param name="bytes">[Cannot be null] Its content</param>
    private void Write(string name, byte[] bytes)
    {
        // Writing the file
        File.WriteAllBytes(Path.Combine(_folder, name), bytes);
    }

    /// <summary>
    /// Method used to run systemd-creds for the user, with a text on its standard input
    /// </summary>
    /// <param name="input">[Cannot be null] Text written on the standard input (empty for none)</param>
    /// <param name="arguments">[Cannot be null] Arguments of systemd-creds</param>
    /// <returns>
    /// The exit code, or -1 when systemd-creds cannot be started (missing)
    /// </returns>
    private static async Task<int> RunSystemdCredsAsync(string input, params string[] arguments)
    {
        // Setting the call
        ProcessStartInfo start = new ProcessStartInfo("systemd-creds")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("--user");
        foreach (string argument in arguments)
            start.ArgumentList.Add(argument);

        // Running it, never longer than a minute
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Function's return: not installed
            return -1;
        }
        using (process)
        {
            await process!.StandardInput.WriteAsync(input);
            process.StandardInput.Close();
            Task<string> output = process.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                await Task.WhenAll(output, error);
                throw;
            }
            await Task.WhenAll(output, error);

            // Function's return
            return process.ExitCode;
        }
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to delete the temporary folder of the test
    /// </summary>
    public void Dispose()
    {
        // Deleting the folder and what it holds
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// On Linux, a secret encrypted by systemd-creds under its name is decrypted under that name only, and read by the store as the tier expects
    /// it (no trailing line break)
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    /// <remarks>
    /// <para><b>Behavior:</b> uses the key of the user (systemd-creds --user), which needs no root; the decryption by a unit of the system
    /// (LoadCredentialEncrypted=) stays a check by hand (doc/adm/configuration.md). Skipped outside Linux, and where systemd-creds or the
    /// manager of the user is missing.</para>
    /// </remarks>
    [Fact]
    public async Task OnLinuxASecretEncryptedBySystemdIsReadUnderItsNameOnlyAsync()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsLinux())
            Assert.Skip("systemd-creds exists on Linux only");

        // Encrypting the secret under its name, outside the folder of the credentials
        string encrypted = Path.Combine(Path.GetTempPath(), $"BoardGamesPlatform.VaultTests.{Guid.NewGuid():N}.cred");
        try
        {
            int encrypt = await RunSystemdCredsAsync("Kx9secret", "encrypt", "--name=Tls__CertificatePassword", "-", encrypted);
            if (encrypt != 0)
                Assert.Skip($"systemd-creds --user cannot encrypt here (exit code {encrypt}): no systemd-creds, or no manager of the user");

            // Decrypting it under its name into the folder of the credentials, then under another name
            int decrypt = await RunSystemdCredsAsync(string.Empty, "decrypt", "--name=Tls__CertificatePassword", encrypted, Path.Combine(_folder, "Tls__CertificatePassword"));
            int renamed = await RunSystemdCredsAsync(string.Empty, "decrypt", "--name=ConnectionStrings__Main", encrypted, Path.Combine(_folder, "ConnectionStrings__Main"));
            IReadOnlyDictionary<string, string> secrets = new SystemdCredentialsStore(_folder).ReadAll(_knownNames);

            // Checks
            decrypt.ShouldBe(0);
            renamed.ShouldNotBe(0, "A credential decrypted under another name than its own must be refused by systemd");
            secrets.ShouldContainKeyAndValue("Tls__CertificatePassword", "Kx9secret");
            secrets.ContainsKey("ConnectionStrings__Main").ShouldBeFalse();
        }
        finally
        {
            File.Delete(encrypted);
        }
    }

    /// <summary>
    /// The credentials are read under their names, as given, accents included
    /// </summary>
    [Fact]
    public void TheCredentialsAreReadUnderTheirNames()
    {
        // Writing two credentials
        Write("Tls__CertificatePassword", Encoding.UTF8.GetBytes("pässwörd"));
        Write("ConnectionStrings__Main", Encoding.UTF8.GetBytes("Server=sql;Database=X"));

        // Reading them
        IReadOnlyDictionary<string, string> secrets = new SystemdCredentialsStore(_folder).ReadAll(_knownNames);

        // Checks
        secrets.Count.ShouldBe(2);
        secrets["Tls__CertificatePassword"].ShouldBe("pässwörd");
        secrets["ConnectionStrings__Main"].ShouldBe("Server=sql;Database=X");
    }

    /// <summary>
    /// A credential that the tier does not know stops the reading, named, rather than being ignored
    /// </summary>
    [Fact]
    public void AnUnknownCredentialIsRefused()
    {
        // Writing a known and an unknown credential
        Write("Tls__CertificatePassword", Encoding.UTF8.GetBytes("x"));
        Write("Smtp__Password", Encoding.UTF8.GetBytes("y"));

        // Check
        VaultException exception = Should.Throw<VaultException>(() => new SystemdCredentialsStore(_folder).ReadAll(_knownNames));
        exception.Message.ShouldContain("Smtp__Password");
    }

    /// <summary>
    /// A value with a line break, an empty value and a value that is not UTF-8 are refused, and the message never holds the value
    /// </summary>
    /// <param name="hex">Content of the credential, in hexadecimal</param>
    [Theory]
    [InlineData("4B78397365637265740A")]
    [InlineData("4B78397365637265740D0A")]
    [InlineData("4B783973656372657409")]
    [InlineData("")]
    [InlineData("4B7839C328")]
    [InlineData("EFBBBF4B7839736563726574")]
    public void AValueThatIsNotASecretIsRefusedWithoutQuotingIt(string hex)
    {
        // Writing the credential ("Kx9secret" followed by a control character, nothing, or a broken UTF-8 sequence)
        Write("Tls__CertificatePassword", Convert.FromHexString(hex));

        // Checks
        VaultException exception = Should.Throw<VaultException>(() => new SystemdCredentialsStore(_folder).ReadAll(_knownNames));
        exception.Message.ShouldContain("Tls__CertificatePassword");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// A folder of the credentials that holds a subfolder is refused rather than half read
    /// </summary>
    [Fact]
    public void AFolderWithASubfolderIsRefused()
    {
        // Writing a credential and a subfolder
        Write("Tls__CertificatePassword", Encoding.UTF8.GetBytes("x"));
        Directory.CreateDirectory(Path.Combine(_folder, "ConnectionStrings__Main"));

        // Check
        Should.Throw<VaultException>(() => new SystemdCredentialsStore(_folder).ReadAll(_knownNames)).Message.ShouldContain("subfolder");
    }

    /// <summary>
    /// A missing folder stops the reading, and a relative folder is refused at once
    /// </summary>
    [Fact]
    public void AMissingOrRelativeFolderIsRefused()
    {
        // Checks
        Should.Throw<VaultException>(() => new SystemdCredentialsStore(Path.Combine(_folder, "missing")).ReadAll(_knownNames));
        Should.Throw<ArgumentException>(() => new SystemdCredentialsStore("relative/folder"));
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
