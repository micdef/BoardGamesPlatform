/*
 *	@file	test/BoardGamesPlatform.Gateway.Tests/Support/GatewayStartup.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Starts the Gateway as its Program does, to prove that an invalid setting stops the startup
 */

// Using
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Gateway.Tests.Support;

/// <summary>
/// Startup of the Gateway with settings that must stop it
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> proves that an invalid setting stops the startup, deterministically. <see cref="GatewayFactory"/> is not used for
/// that: when the application under test fails to start, <c>WebApplicationFactory</c> can meet the host already disposed by the failure and
/// throw an <see cref="ObjectDisposedException"/> instead of the real error (seen on the CI runner, never locally).</para>
/// <para><b>Behavior:</b> builds the web host with the registrations of Program.cs that read the settings (<c>AddPlatformGateway</c>, then
/// <c>MapPlatformHub</c>; observability and /health are left out) on the in-memory test server, in the Testing environment, with the
/// settings files of the output folder and the given settings added last, and starts it. The options validation failure is found anywhere
/// in the exception tree (an aggregate holds several); no other failure is accepted.</para>
/// </remarks>
internal static class GatewayStartup
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
    /// Method used to find an options validation failure in an exception tree: its inner exceptions, and every exception of an aggregate
    /// </summary>
    /// <param name="exception">[Can be null] Root of the tree</param>
    /// <returns>
    /// The first options validation failure found, or null when there is none
    /// </returns>
    private static OptionsValidationException? FindValidationFailure(Exception? exception)
    {
        // The exception itself
        if (exception is null)
            return null;
        if (exception is OptionsValidationException validation)
            return validation;

        // Every exception of an aggregate, then the inner exception
        if (exception is AggregateException aggregate)
        {
            foreach (Exception inner in aggregate.InnerExceptions)
            {
                OptionsValidationException? found = FindValidationFailure(inner);
                if (found is not null)
                    return found;
            }
        }

        // Function's return
        return FindValidationFailure(exception.InnerException);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to start the Gateway with settings that must stop it, and get the validation failure (Asynchronous)
    /// </summary>
    /// <param name="settings">[Cannot be null] Settings added last, over the files of the Gateway</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The options validation failure that stopped the startup
    /// </returns>
    /// <exception cref="InvalidOperationException">If the Gateway starts, or fails for another reason (the reason is the inner exception)</exception>
    public static async Task<OptionsValidationException> FindStartupFailureAsync(IDictionary<string, string?> settings, CancellationToken cancellationToken)
    {
        // Building the host as Program.cs does, on the in-memory test server; the settings files are read from the output folder, whatever
        // the current directory of the test run
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = AppContext.BaseDirectory });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddPlatformGateway();
        await using WebApplication app = builder.Build();

        // Mapping the hub (it reads its path) and starting: one of them must fail on the invalid setting
        try
        {
            app.MapPlatformHub();
            await app.StartAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Function's return
            return FindValidationFailure(exception)
                ?? throw new InvalidOperationException("The startup failed, but not on an options validation", exception);
        }

        // The Gateway started: the setting was not refused
        await app.StopAsync(cancellationToken);
        throw new InvalidOperationException("The Gateway started although a setting was invalid");
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
