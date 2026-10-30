/*
 *	@file	src/BoardGamesPlatform.Gateway/BackLink/BackClientOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Startup check of the settings of the "Back" configuration section
 */

// Using
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.BackLink;

/// <summary>
/// Validator of the settings used by the Gateway to reach the Back
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> stops the startup of the Gateway on a wrong setting, rather than failing at the first Ping of a front.</para>
/// <para><b>Behavior:</b> applies <see cref="BackClientOptions.FindProblem"/>; the failure names the setting, never its value.</para>
/// </remarks>
public sealed class BackClientOptionsValidator : IValidateOptions<BackClientOptions>
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

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, BackClientOptions options)
    {
        // Check
        if (options is null)
            throw new ArgumentNullException(nameof(options), $"The value of the parameter: {nameof(options)} cannot be null");

        // Function's return
        string? problem = options.FindProblem();
        return problem is null ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(problem);
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
