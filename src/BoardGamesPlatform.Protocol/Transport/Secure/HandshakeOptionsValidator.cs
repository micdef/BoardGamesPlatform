/*
 *	@file	src/BoardGamesPlatform.Protocol/Transport/Secure/HandshakeOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks of the form of the settings of the secure channel, at the startup of a tier
 */

// Using
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Protocol.Transport.Secure;

/// <summary>
/// Validator of <see cref="HandshakeOptions"/>
/// </summary>
/// <remarks>
/// <para><b>Behavior:</b> checks the form of every setting; the files themselves are opened and checked when the keys are loaded
/// (<see cref="SigningKeys.FromOptions"/>), at startup too. A message names the setting, never the value of the password.</para>
/// </remarks>
public sealed class HandshakeOptionsValidator : IValidateOptions<HandshakeOptions>
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
    /// Method used to check the settings of the secure channel of a tier
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure listing every invalid setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, HandshakeOptions options)
    {
        // Checks
        List<string> failures = new List<string>();
        string section = HandshakeOptions.SectionName;
        if (string.IsNullOrWhiteSpace(options.SigningKeyPath))
            failures.Add($"{section}:{nameof(HandshakeOptions.SigningKeyPath)} must give the PEM file of the signing key of the tier");
        if (string.IsNullOrEmpty(options.SigningKeyPassword) || options.SigningKeyPassword.Length < HandshakeOptions.MinPasswordLength)
            failures.Add($"{section}:{nameof(HandshakeOptions.SigningKeyPassword)} must be set, with {HandshakeOptions.MinPasswordLength} characters at least (user secrets in development, the vault of the tier on a server: see doc/adm/configuration.md)");
        if (string.IsNullOrWhiteSpace(options.PeerPublicKeysPath))
            failures.Add($"{section}:{nameof(HandshakeOptions.PeerPublicKeysPath)} must give the file of the public keys of the peer");

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
