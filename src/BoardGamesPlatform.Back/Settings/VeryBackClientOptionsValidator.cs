/*
 *	@file	src/BoardGamesPlatform.Back/Settings/VeryBackClientOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "VeryBack" settings of the Back at startup
 */

// Using
using BoardGamesPlatform.Platform.Infrastructure.VeryBack;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Back.Settings;

/// <summary>
/// Validator of <see cref="VeryBackClientOptions"/> for the host of the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> a Back configured with an unusable address of the Very Back must refuse to start (fail closed), like any other
/// invalid setting, instead of failing at the first request.</para>
/// <para><b>Behavior:</b> applies <see cref="VeryBackClientOptions.FindProblem"/>, the single place where the checks are written; the failure
/// names the setting and never repeats its value.</para>
/// </remarks>
public sealed class VeryBackClientOptionsValidator : IValidateOptions<VeryBackClientOptions>
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
    /// Method used to check the settings the Back uses to reach the Very Back
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure naming the invalid setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, VeryBackClientOptions options)
    {
        // Running the checks of the settings themselves
        string? problem = options.FindProblem();
        if (problem is not null)
            return ValidateOptionsResult.Fail(problem);

        // Function's return
        return ValidateOptionsResult.Success;
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
