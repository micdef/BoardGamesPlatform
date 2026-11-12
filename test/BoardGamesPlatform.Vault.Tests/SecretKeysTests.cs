/*
 *	@file	test/BoardGamesPlatform.Vault.Tests/SecretKeysTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the names of the secrets in the vault
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Vault.Tests;

/// <summary>
/// Tests used to check <see cref="SecretKeys"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a secret written under a wrong name would never reach its setting: the tier would refuse to start for a missing secret.</para>
/// </remarks>
public sealed class SecretKeysTests
{
    #region Members

    #region Constants

    // #N/A

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

    // #N/A

    #endregion

    #region Publics

    /// <summary>
    /// A key and its name in the vault map to each other, at every depth
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <param name="name">Name in the vault</param>
    [Theory]
    [InlineData("Tls:CertificatePassword", "Tls__CertificatePassword")]
    [InlineData("ConnectionStrings:Main", "ConnectionStrings__Main")]
    [InlineData("Smtp:Accounts:0:Password", "Smtp__Accounts__0__Password")]
    public void AKeyAndItsNameInTheVaultMapToEachOther(string key, string name)
    {
        // Checks
        SecretKeys.ToVaultName(key).ShouldBe(name);
        SecretKeys.ToKey(name).ShouldBe(key);
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
