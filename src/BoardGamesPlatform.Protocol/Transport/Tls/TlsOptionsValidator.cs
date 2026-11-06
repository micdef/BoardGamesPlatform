/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Tls/TlsOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "Tls" settings at startup, without ever showing the password
 */

// Using
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Tls;

/// <summary>
/// Validator of <see cref="TlsOptions"/>
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> checks the form of every setting; the files themselves are opened and checked when the credentials are loaded
/// (<see cref="TlsCredentials.FromOptions"/>), at startup too. A message names the setting, never the value of the password.</para>
/// </remarks>
public sealed class TlsOptionsValidator : IValidateOptions<TlsOptions>
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
    /// Method used to check the TLS settings of a tier
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure listing every invalid setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, TlsOptions options)
    {
        // Checks
        List<string> failures = new List<string>();
        string section = TlsOptions.SectionName;
        if (!PlatformIdentity.IsValidSuffix(options.IdentitySuffix))
            failures.Add($"{section}:{nameof(TlsOptions.IdentitySuffix)} must be DNS labels of lower-case letters, digits and inner hyphens, for example bgp.internal, not '{options.IdentitySuffix}'");
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
            failures.Add($"{section}:{nameof(TlsOptions.CertificatePath)} must give the PFX of the tier");
        if (string.IsNullOrEmpty(options.CertificatePassword))
            failures.Add($"{section}:{nameof(TlsOptions.CertificatePassword)} must be set (user secrets in development, the environment variable Tls__CertificatePassword on a server)");
        if (string.IsNullOrWhiteSpace(options.TrustedRootsPath))
            failures.Add($"{section}:{nameof(TlsOptions.TrustedRootsPath)} must give the PEM file of the roots of the platform");
        if (options.RefusedThumbprints is null || options.RefusedThumbprints.Any(thumbprint => thumbprint is null || thumbprint.Length != 64 || !thumbprint.All(char.IsAsciiHexDigit)))
            failures.Add($"{section}:{nameof(TlsOptions.RefusedThumbprints)} must hold SHA-256 thumbprints only, 64 hexadecimal digits each");
        if (options.ExpiryWarning < TimeSpan.Zero || options.ExpiryWarning > TlsOptions.MaxExpiryWarning)
            failures.Add($"{section}:{nameof(TlsOptions.ExpiryWarning)} must be between 0 and {TlsOptions.MaxExpiryWarning.TotalDays} days, not {options.ExpiryWarning}");

        // Function's return
        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
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
