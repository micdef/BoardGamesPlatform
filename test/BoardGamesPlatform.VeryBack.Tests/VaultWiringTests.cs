/*
 *	@file	test/BoardGamesPlatform.VeryBack.Tests/VaultWiringTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the Very Back reads each of its secrets from its vault, and refuses them from any other source on a server
 */

// Using
using BoardGamesPlatform.Vault;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Tests;

/// <summary>
/// Tests used to check the secrets that <see cref="VeryBackExtensions.AddPlatformVeryBack"/> gives to the vault
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the vault checks only the keys a tier gives it: a secret missing from that list would be accepted from an environment
/// variable or the command line of a server, without any warning. The list of the tier is checked exactly, and each of its secrets is refused
/// on the command line of a server.</para>
/// <para><b>Behavior:</b> builds the host in the Production environment, without vault, and reads the options of the vault only (the other
/// settings of the tier are not needed for that).</para>
/// </remarks>
public sealed class VaultWiringTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// The secrets of the Very Back, in the order it gives them to the vault
    /// </summary>
    private static readonly string[] _secretKeys = new string[] { "ConnectionStrings:Main", "Tls:CertificatePassword", "Handshake:SigningKeyPassword" };

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
    /// Method used to build the Very Back as a server, with a command line
    /// </summary>
    /// <param name="commandLine">[Cannot be null] Arguments of the command line</param>
    /// <returns>
    /// The host, not started
    /// </returns>
    private static IHost BuildServer(params string[] commandLine)
    {
        // Building the host in Production
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration.AddCommandLine(commandLine);
        builder.AddPlatformVeryBack();

        // Function's return
        return builder.Build();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The Very Back gives exactly its three secrets to the vault
    /// </summary>
    [Fact]
    public void TheTierGivesExactlyItsSecretsToTheVault()
    {
        // Building the host
        using IHost host = BuildServer();

        // Check
        host.Services.GetRequiredService<VaultSecretKeys>().Keys.ShouldBe(_secretKeys);
    }

    /// <summary>
    /// Each secret of the Very Back on the command line of a server is refused, naming the secret and never its value
    /// </summary>
    /// <param name="key">Configuration key of the secret</param>
    [Theory]
    [InlineData("ConnectionStrings:Main")]
    [InlineData("Tls:CertificatePassword")]
    [InlineData("Handshake:SigningKeyPassword")]
    public void ASecretOnTheCommandLineOfAServerIsRefused(string key)
    {
        // Building the host with the secret on its command line
        using IHost host = BuildServer($"--{key}=Kx9secret");

        // Checks
        OptionsValidationException exception = Should.Throw<OptionsValidationException>(() => host.Services.GetRequiredService<IOptions<VaultOptions>>().Value);
        exception.Message.ShouldContain(key);
        exception.Message.ShouldNotContain("Kx9");
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
