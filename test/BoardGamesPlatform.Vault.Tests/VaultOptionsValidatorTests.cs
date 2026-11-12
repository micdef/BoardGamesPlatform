/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/VaultOptionsValidatorTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that, outside Development, a secret coming from another source than the vault stops the startup
 */

// Using
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Tests used to check <see cref="VaultOptionsValidator"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> rule 1 of the vault: on a server, a secret comes from the vault only; an environment variable, a JSON file edited on the
/// server or a command-line argument holding a secret stops the startup.</para>
/// <para><b>Behavior:</b> builds configurations with each kind of source; the environment variables get a prefix of their own (a GUID), so that
/// the tests running at the same time never see them.</para>
/// </remarks>
[Collection(ProcessEnvironment.Name)]
public sealed class VaultOptionsValidatorTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Command line that holds a secret
    /// </summary>
    private static readonly string[] _secretOnTheCommandLine = new string[] { "--Tls:CertificatePassword=Kx9secret" };

    /// <summary>
    /// Command line that holds an empty secret key and an ordinary setting
    /// </summary>
    private static readonly string[] _emptySecretOnTheCommandLine = new string[] { "--Tls:CertificatePassword=", "--Tls:CertificatePath=tier.pfx" };

    /// <summary>
    /// Keys of the secrets of the vault of a test
    /// </summary>
    private static readonly string[] _connectionStringKey = new string[] { "ConnectionStrings:Main" };

    /// <summary>
    /// Secrets of the tier of the tests (those of the Very Back)
    /// </summary>
    private static readonly string[] _secretsOfTheTier = new string[] { "ConnectionStrings:Main", "Tls:CertificatePassword", "Handshake:SigningKeyPassword" };

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
    /// Method used to validate a configuration in an environment
    /// </summary>
    /// <param name="configuration">[Cannot be null] Configuration to check</param>
    /// <param name="environmentName">[Cannot be null] Name of the environment (Production, Development...)</param>
    /// <param name="options">[Can be null] Settings of the vault; empty settings when null</param>
    /// <returns>
    /// The result of the validation
    /// </returns>
    private static ValidateOptionsResult Validate(IConfiguration configuration, string environmentName, VaultOptions? options = null)
    {
        // Function's return
        return new VaultOptionsValidator(configuration, new TestEnvironment(environmentName), new VaultSecretKeys(_secretsOfTheTier)).Validate(null, options ?? new VaultOptions());
    }

    #endregion

    #region Publics

    /// <summary>
    /// Outside Development, a secret set by an environment variable, by a JSON file or by the command line is refused, naming the key and the
    /// source, never the value
    /// </summary>
    /// <param name="source">The source of the secret</param>
    [Theory]
    [InlineData("environment")]
    [InlineData("json")]
    [InlineData("command line")]
    public void ASecretOutsideTheVaultIsRefusedOnAServer(string source)
    {
        // Building a configuration whose source holds a secret
        string prefix = $"BGPTEST{Guid.NewGuid():N}_";
        string json = Path.Combine(Path.GetTempPath(), $"{prefix}appsettings.json");
        ConfigurationManager configuration = new ConfigurationManager();
        try
        {
            switch (source)
            {
                case "environment":
                    Environment.SetEnvironmentVariable($"{prefix}Tls__CertificatePassword", "Kx9secret");
                    configuration.AddEnvironmentVariables(prefix);
                    break;
                case "json":
                    File.WriteAllText(json, """{ "Tls": { "CertificatePassword": "Kx9secret" } }""");
                    configuration.AddJsonFile(new PhysicalFileProvider(Path.GetTempPath()), Path.GetFileName(json), optional: false, reloadOnChange: false);
                    break;
                default:
                    configuration.AddCommandLine(_secretOnTheCommandLine);
                    break;
            }

            // Validating as a server
            ValidateOptionsResult result = Validate(configuration, Environments.Production);

            // Checks
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("Tls:CertificatePassword");
            result.FailureMessage.ShouldNotContain("Kx9");
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}Tls__CertificatePassword", null);
            File.Delete(json);
        }
    }

    /// <summary>
    /// Outside Development, an empty secret key (as the committed appsettings.json documents it), the ordinary settings, the in-memory settings
    /// of the code, and the secrets of the vault are accepted
    /// </summary>
    [Fact]
    public void TheVaultAndTheEmptySecretKeysAreAcceptedOnAServer()
    {
        // Building a configuration with an empty secret key, an ordinary setting, a secret in memory, and a vault
        ConfigurationManager configuration = new ConfigurationManager();
        configuration.AddCommandLine(_emptySecretOnTheCommandLine);
        configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Handshake:SigningKeyPassword"] = "from the code" });
        ((IConfigurationBuilder)configuration).Add(new VaultConfigurationSource(new FakeStore(new Dictionary<string, string> { ["ConnectionStrings__Main"] = "Server=sql" }),
            _connectionStringKey));

        // Checks
        Validate(configuration, Environments.Production).Succeeded.ShouldBeTrue();
        configuration["ConnectionStrings:Main"].ShouldBe("Server=sql");
    }

    /// <summary>
    /// Outside Development, a connection string set by the SQLCONNSTR_ prefix of the environment variables is refused: the check reads the key
    /// as the provider maps it, ConnectionStrings:Main
    /// </summary>
    [Fact]
    public void AConnectionStringOfTheSqlPrefixIsRefusedOnAServer()
    {
        // Building a configuration from every environment variable, as the hosts of .NET do, with SQLCONNSTR_Main set
        string? before = Environment.GetEnvironmentVariable("SQLCONNSTR_Main");
        try
        {
            Environment.SetEnvironmentVariable("SQLCONNSTR_Main", "Server=sql;Password=Kx9secret");
            ConfigurationManager configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables();

            // Validating as a server
            ValidateOptionsResult result = Validate(configuration, Environments.Production);

            // Checks
            result.Failed.ShouldBeTrue();
            result.FailureMessage.ShouldContain("ConnectionStrings:Main");
            result.FailureMessage.ShouldNotContain("Kx9");
        }
        finally
        {
            Environment.SetEnvironmentVariable("SQLCONNSTR_Main", before);
        }
    }

    /// <summary>
    /// Outside Development, a variable of the machine that looks like a secret but is not one of the tier (a token of a tool) is ignored
    /// </summary>
    [Fact]
    public void AVariableOfTheMachineThatIsNotASecretOfTheTierIsIgnored()
    {
        // Building a configuration whose environment holds a token of a tool
        string prefix = $"BGPTEST{Guid.NewGuid():N}_";
        try
        {
            Environment.SetEnvironmentVariable($"{prefix}GITHUB_TOKEN", "Kx9secret");
            ConfigurationManager configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables(prefix);

            // Check
            Validate(configuration, Environments.Production).Succeeded.ShouldBeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable($"{prefix}GITHUB_TOKEN", null);
        }
    }

    /// <summary>
    /// In Development, a secret from any source is accepted: the user secrets are the vault of a developer machine
    /// </summary>
    [Fact]
    public void AnySourceIsAcceptedInDevelopment()
    {
        // Building a configuration whose command line holds a secret
        ConfigurationManager configuration = new ConfigurationManager();
        configuration.AddCommandLine(_secretOnTheCommandLine);

        // Check
        Validate(configuration, Environments.Development).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// A secret hidden in a configuration chained into the configuration of the tier is refused on a server
    /// </summary>
    [Fact]
    public void ASecretOfAChainedConfigurationIsRefusedOnAServer()
    {
        // Building a configuration whose command line holds a secret, chained into the configuration of the tier
        ConfigurationManager inner = new ConfigurationManager();
        inner.AddCommandLine(_secretOnTheCommandLine);
        ConfigurationManager configuration = new ConfigurationManager();
        configuration.AddConfiguration(inner);

        // Checks
        ValidateOptionsResult result = Validate(configuration, Environments.Production);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Tls:CertificatePassword");
        result.FailureMessage.ShouldNotContain("Kx9");
    }

    /// <summary>
    /// On a server, a configuration whose sources cannot be listed (a section instead of the root) is refused rather than trusted
    /// </summary>
    [Fact]
    public void AConfigurationThatCannotBeInspectedIsRefusedOnAServer()
    {
        // Giving a section of the configuration instead of its root
        ConfigurationManager configuration = new ConfigurationManager();
        configuration.AddCommandLine(_secretOnTheCommandLine);

        // Checks
        ValidateOptionsResult result = Validate(configuration.GetSection("Tls"), Environments.Production);
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("cannot be inspected");
    }

    /// <summary>
    /// A relative folder of the vault is refused, in every environment
    /// </summary>
    /// <param name="environmentName">Name of the environment</param>
    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void ARelativeFolderOfTheVaultIsRefused(string environmentName)
    {
        // Check
        Validate(new ConfigurationManager(), environmentName, new VaultOptions { Path = "vault" }).Failed.ShouldBeTrue();
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
