/*
 *	@file	test/BoardGamesPlatform.Protocol.Tests/Transport/Tls/TlsOptionsValidatorTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the startup check of the "Tls" settings
 */

// Using
using BoardGamesPlatform.Protocol.Transport.Tls;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Tests.Transport.Tls;

/// <summary>
/// Tests used to check <see cref="TlsOptionsValidator"/>
/// </summary>
public sealed class TlsOptionsValidatorTests
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

    /// <summary>
    /// Method used to build valid settings
    /// </summary>
    /// <returns>
    /// The settings
    /// </returns>
    private static TlsOptions CreateValid()
    {
        // Function's return
        return new TlsOptions
        {
            IdentitySuffix = "bgp.internal",
            CertificatePath = "back.pfx",
            CertificatePassword = "secret-value-never-shown",
            TrustedRootsPath = "platform-ca.crt",
            RefusedThumbprints = new string[] { new string('A', 64) }
        };
    }

    #endregion

    #region Publics

    /// <summary>
    /// Valid settings pass
    /// </summary>
    [Fact]
    public void ValidSettingsPass()
    {
        // Check
        new TlsOptionsValidator().Validate(null, CreateValid()).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Every invalid setting is named, and the password never appears
    /// </summary>
    [Fact]
    public void EveryInvalidSettingIsNamed()
    {
        // Breaking every setting
        TlsOptions options = new TlsOptions
        {
            IdentitySuffix = "Not Valid",
            CertificatePath = " ",
            CertificatePassword = string.Empty,
            TrustedRootsPath = string.Empty,
            RefusedThumbprints = new string[] { "ABC" },
            ExpiryWarning = TimeSpan.FromDays(-1)
        };

        // Checks
        ValidateOptionsResult result = new TlsOptionsValidator().Validate(null, options);
        result.Failed.ShouldBeTrue();
        string failures = result.FailureMessage.ShouldNotBeNull();
        foreach (string setting in new string[] { "Tls:IdentitySuffix", "Tls:CertificatePath", "Tls:CertificatePassword", "Tls:TrustedRootsPath", "Tls:RefusedThumbprints", "Tls:ExpiryWarning" })
            failures.ShouldContain(setting);
    }

    /// <summary>
    /// A wrong setting next to a set password never shows the password
    /// </summary>
    [Fact]
    public void ThePasswordIsNeverShown()
    {
        // Breaking a setting while the password is set
        TlsOptions options = CreateValid();
        options.TrustedRootsPath = string.Empty;

        // Check
        new TlsOptionsValidator().Validate(null, options).FailureMessage.ShouldNotBeNull().ShouldNotContain(options.CertificatePassword);
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
