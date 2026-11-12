/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/VaultExtensionsTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks how a host gets the vault of its system, and that the tool of the administrators writes secrets the vault reads
 */

// Using
using System.Diagnostics;
using System.Security.Principal;
using BoardGamesPlatform.TestKit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Tests used to check <see cref="VaultExtensions"/> and tools/vault/Set-VaultSecret.ps1
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the vault must give the secrets of a tier under their usual keys, win over every other source, and check the sources of
/// the secrets when the host starts; the tool of the administrators must write files that the vault reads.</para>
/// <para><b>Behavior:</b> the tests of this class set the process variable CREDENTIALS_DIRECTORY (Linux) and run one after the other (xUnit runs
/// the tests of one class in sequence); no other class reads that variable. The test of the tool runs pwsh, and is skipped on other systems than
/// Windows.</para>
/// </remarks>
public sealed class VaultExtensionsTests : IDisposable
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time the tool may take
    /// </summary>
    private const int ToolTimeoutMilliseconds = 60_000;

    #endregion

    #region Variables

    /// <summary>
    /// Command line that holds a secret
    /// </summary>
    private static readonly string[] _secretOnTheCommandLine = new string[] { "--Tls:CertificatePassword=Kx9secret" };

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
    /// Constructor used to name the folder of a test
    /// </summary>
    public VaultExtensionsTests()
    {
        // Setting
        _folder = Path.Combine(Path.GetTempPath(), $"BoardGamesPlatform.VaultTests.{Guid.NewGuid():N}");
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to know whether the tests run with the rights of an administrator (Windows)
    /// </summary>
    /// <returns>
    /// True  ==> the session is elevated
    /// False ==> it is not, or the system is not Windows
    /// </returns>
    private static bool IsElevated()
    {
        // Function's return
        if (!OperatingSystem.IsWindows())
            return false;
        using WindowsIdentity current = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(current).IsInRole(WindowsBuiltInRole.Administrator);
    }

    /// <summary>
    /// Method used to run tools/vault/Set-VaultSecret.ps1 with a value on its standard input
    /// </summary>
    /// <param name="name">[Cannot be null] Key of the secret</param>
    /// <param name="value">[Cannot be null] Value written on the standard input</param>
    /// <returns>
    /// The exit code of the tool and what it printed
    /// </returns>
    private async Task<(int ExitCode, string Output)> RunToolAsync(string name, string value)
    {
        // Setting the call: the value on the standard input, never as an argument
        string script = Path.Combine(RepositoryPaths.GetRoot(), "tools", "vault", "Set-VaultSecret.ps1");
        ProcessStartInfo start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new string[] { "-NoProfile", "-File", script, "-VaultPath", _folder, "-Name", name, "-Account", Environment.UserDomainName + "\\" + Environment.UserName })
            start.ArgumentList.Add(argument);

        // Running the tool
        using Process process = Process.Start(start)!;
        await process.StandardInput.WriteAsync(value);
        process.StandardInput.Close();
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new CancellationTokenSource(ToolTimeoutMilliseconds);
        await process.WaitForExitAsync(timeout.Token);

        // Function's return
        return (process.ExitCode, await output + await error);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to delete the folder of the test, and the variable of systemd it may have set
    /// </summary>
    public void Dispose()
    {
        // Cleaning
        Environment.SetEnvironmentVariable(SystemdCredentialsStore.DirectoryVariable, null);
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }

    /// <summary>
    /// Without a vault (a developer machine, the tests), nothing is added and the host keeps its sources
    /// </summary>
    [Fact]
    public void WithoutAVaultNothingIsAdded()
    {
        // Building a host without vault
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });

        // Check
        builder.AddPlatformVault("Tls:CertificatePassword").ShouldBeFalse();
    }

    /// <summary>
    /// On Linux, the credentials of systemd are given under their keys, and win over the other sources
    /// </summary>
    [Fact]
    public void OnLinuxTheCredentialsOfSystemdAreGivenUnderTheirKeys()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsLinux())
            Assert.Skip("The credentials of systemd exist on Linux only");

        // Writing a credential, and a host whose memory holds another value of it
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "Tls__CertificatePassword"), "from the vault");
        Environment.SetEnvironmentVariable(SystemdCredentialsStore.DirectoryVariable, _folder);
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Tls:CertificatePassword"] = "from the code" });

        // Adding the vault
        bool added = builder.AddPlatformVault("Tls:CertificatePassword");

        // Checks
        added.ShouldBeTrue();
        builder.Configuration["Tls:CertificatePassword"].ShouldBe("from the vault");
    }

    /// <summary>
    /// On Linux, the setting of Windows Vault:Path is refused rather than ignored
    /// </summary>
    [Fact]
    public void OnLinuxTheSettingOfWindowsIsRefused()
    {
        // Skipping on other systems
        if (!OperatingSystem.IsLinux())
            Assert.Skip("The rule concerns Linux only");

        // Building a host with the setting of Windows
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Vault:Path"] = "/etc/boardgamesplatform" });

        // Check
        Should.Throw<VaultException>(() => builder.AddPlatformVault("Tls:CertificatePassword"));
    }

    /// <summary>
    /// The host checks the sources of the secrets when its options are read: a secret on the command line of a server is refused
    /// </summary>
    [Fact]
    public void TheHostRefusesASecretOutsideTheVault()
    {
        // Building a server whose command line holds a secret
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.AddCommandLine(_secretOnTheCommandLine);
        builder.AddPlatformVault("Tls:CertificatePassword");
        using IHost host = builder.Build();

        // Checks
        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<VaultOptions>>().Value);
        exception.Message.ShouldContain("Tls:CertificatePassword");
        exception.Message.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// The tool refuses to run without the rights of an administrator, and writes nothing (run by a session that is not elevated)
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact]
    public async Task TheToolRefusesToRunWithoutTheRightsOfAnAdministratorAsync()
    {
        // Skipping on other systems, and in an elevated session (the next test covers it)
        if (!OperatingSystem.IsWindows())
            Assert.Skip("The tool writes the vault of Windows");
        if (IsElevated())
            Assert.Skip("The session is elevated: the tool is allowed to run");

        // Running the tool
        (int ExitCode, string Output) result = await RunToolAsync("Tls:CertificatePassword", "Kx9secret");

        // Checks
        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("administrator");
        result.Output.ShouldNotContain("Kx9");
        Directory.Exists(_folder).ShouldBeFalse();
    }

    /// <summary>
    /// The tool of the administrators writes a secret that the vault of Windows reads under its key, after dropping the line break that ends the
    /// standard input; a value with another control character is refused by the tool, and the value never appears in what it prints (run by an
    /// elevated session only: skipped otherwise)
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact]
    public async Task TheToolWritesASecretTheVaultReadsAsync()
    {
        // Skipping on other systems, and in a session that is not elevated
        if (!OperatingSystem.IsWindows())
            Assert.Skip("The tool writes the vault of Windows");
        if (!IsElevated())
            Assert.Skip("The tool needs the rights of an administrator: run the tests from an elevated session to cover it");

        // Writing a secret with the tool, then a value it must refuse
        (int ExitCode, string Output) written = await RunToolAsync("Tls:CertificatePassword", "Kx9secret;'\"\r\n");
        (int ExitCode, string Output) refused = await RunToolAsync("Handshake:SigningKeyPassword", "Kx9\tsecret");

        // Reading the vault as a server
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Vault:Path"] = _folder });
        builder.AddPlatformVault("Tls:CertificatePassword", "Handshake:SigningKeyPassword");

        // Checks
        written.ExitCode.ShouldBe(0, written.Output);
        refused.ExitCode.ShouldBe(1);
        (written.Output + refused.Output).ShouldNotContain("Kx9");
        builder.Configuration["Tls:CertificatePassword"].ShouldBe("Kx9secret;'\"");
        builder.Configuration["Handshake:SigningKeyPassword"].ShouldBeNull();
        File.Exists(Path.Combine(_folder, "Handshake__SigningKeyPassword")).ShouldBeFalse();
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
