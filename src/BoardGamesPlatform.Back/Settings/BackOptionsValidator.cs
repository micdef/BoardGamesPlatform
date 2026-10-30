/*
 *	@file	src/BoardGamesPlatform.Back/Settings/BackOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "Back" settings at startup
 */

// Using
using System.Net;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Settings;

/// <summary>
/// Validator of <see cref="BackOptions"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a Back configured with an unusable endpoint must refuse to start (fail closed), not listen somewhere else.</para>
/// <para><b>Behavior:</b> fails when the address is not an IP address, or when the port is outside 1-65535. The messages name the setting and
/// the expected form, and repeat the value of the address and of the port, which are not secrets.</para>
/// </remarks>
public sealed class BackOptionsValidator : IValidateOptions<BackOptions>
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
    /// Method used to check the settings of the Back
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure listing every invalid setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, BackOptions options)
    {
        // Checks
        List<string> failures = new List<string>();
        if (!IPAddress.TryParse(options.ListenAddress, out _))
            failures.Add($"{BackOptions.SectionName}:{nameof(BackOptions.ListenAddress)} must be an IPv4 or IPv6 address, not '{options.ListenAddress}'");
        if (options.Port < 1 || options.Port > IPEndPoint.MaxPort)
            failures.Add($"{BackOptions.SectionName}:{nameof(BackOptions.Port)} must be between 1 and {IPEndPoint.MaxPort}, not {options.Port}");

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
