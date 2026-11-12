/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Security/DeploymentScriptTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the guard of tools/database/Deploy-TestDatabase.ps1, and a deployment from scratch followed by a second run
 */

// Using
using System.ComponentModel;
using System.Diagnostics;
using BoardGamesPlatform.TestKit;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Security;

/// <summary>
/// Tests used to check tools/database/Deploy-TestDatabase.ps1
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the script drops and recreates a database and a login: its guard must refuse every name that is not a throwaway one
/// before anything is built or opened, and a deployment must leave the login, the user and the role of the matrix, with a new password at each
/// run, never printed (doc/dev/database.md, Deployment script).</para>
/// <para><b>Behavior:</b> the guard is always run with -WhatIf, so that a regression of the guard can never drop a real database. The deployment
/// test deploys a database of its own (DEV_Test_ followed by a GUID) on the server of the throwaway database, twice, then drops the database and
/// its login in every case; it takes about a minute and a half. It needs Windows (MSBuild, SqlPackage) and is skipped when no throwaway database
/// is configured (see <see cref="TestDatabase"/>). The password itself is kept nowhere (-SecretTarget None): that it opens a session is checked
/// by hand (doc/dev/database.md, Tests).</para>
/// </remarks>
public sealed class DeploymentScriptTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Longest time one run of the script may take (build and publish)
    /// </summary>
    private const int ScriptTimeoutMilliseconds = 600_000;

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
    /// Method used to run the script with pwsh
    /// </summary>
    /// <param name="arguments">[Cannot be null] Arguments of the script</param>
    /// <returns>
    /// The exit code and what the script printed (output and errors), or null when pwsh cannot be started
    /// </returns>
    private static async Task<(int ExitCode, string Output)?> RunScriptAsync(params string[] arguments)
    {
        // Setting the call
        string script = Path.Combine(RepositoryPaths.GetRoot(), "tools", "database", "Deploy-TestDatabase.ps1");
        ProcessStartInfo start = new ProcessStartInfo("pwsh")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new string[] { "-NoProfile", "-NonInteractive", "-File", script }.Concat(arguments))
            start.ArgumentList.Add(argument);

        // Running the script
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            // Function's return: pwsh is not installed
            return null;
        }
        using (process)
        {
            Task<string> output = process!.StandardOutput.ReadToEndAsync();
            Task<string> error = process.StandardError.ReadToEndAsync();
            using CancellationTokenSource timeout = new CancellationTokenSource(ScriptTimeoutMilliseconds);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                // Never leaving a script running after the test: killing it, and waiting for its streams to end
                process.Kill(entireProcessTree: true);
                await Task.WhenAll(output, error);
                throw;
            }

            // Function's return
            return (process.ExitCode, await output + await error);
        }
    }

    /// <summary>
    /// Method used to read the password hash and the policy of a login
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="name">[Cannot be null] Name of the login (DEV_Test_ and a GUID: safe in a batch)</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The hash in hexadecimal and the policy (policy checked|expiration checked|default database), or null when the login does not exist
    /// </returns>
    private static async Task<(string Hash, string Policy)?> ReadLoginAsync(SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        // Reading the login
        await using SqlCommand command = new SqlCommand($"""
            SELECT CONVERT(NVARCHAR(512), [password_hash], 2), CONCAT([is_policy_checked], N'|', [is_expiration_checked], N'|', [default_database_name])
            FROM [sys].[sql_logins] WHERE [name] = N'{name}';
            """, connection);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        // Function's return
        return await reader.ReadAsync(cancellationToken) ? (reader.GetString(0), reader.GetString(1)) : null;
    }

    #endregion

    #region Publics

    /// <summary>
    /// The guard refuses every name that is not a throwaway database, before anything is built or opened
    /// </summary>
    /// <param name="name">Name given to the script</param>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Theory]
    [InlineData("master")]
    [InlineData("TEST")]
    [InlineData("DEV_")]
    [InlineData("test_X")]
    [InlineData("Dev_x")]
    [InlineData("DEV_x;DROP")]
    [InlineData("DEV_x]")]
    [InlineData("DEV_x\n")]
    public async Task TheGuardRefusesAWrongNameBeforeAnyBuildAsync(string name)
    {
        // Running the script with -WhatIf, so that nothing can ever be dropped
        (int ExitCode, string Output)? result = await RunScriptAsync("-DatabaseName", name, "-WhatIf");
        if (result is null)
            Assert.Skip("pwsh is not installed");

        // Checks
        result.Value.ExitCode.ShouldBe(1);
        result.Value.Output.ShouldContain("Refused");
    }

    /// <summary>
    /// A deployment from scratch makes the login with the password policy and without expiration, its user member of the application role;
    /// a second run replaces the password; no run prints a password or a connection string
    /// </summary>
    /// <returns>
    /// A task that completes when the check is done
    /// </returns>
    [Fact]
    public async Task ADeploymentFromScratchThenASecondRunReplacesThePasswordAsync()
    {
        // Skipping outside Windows, and without a throwaway database (its server is the server of the deployment)
        if (!OperatingSystem.IsWindows())
            Assert.Skip("The script builds with the MSBuild of Visual Studio and publishes with SqlPackage: Windows only");
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string name = $"DEV_Test_{Guid.NewGuid():N}";
        try
        {
            // Deploying twice, the second time without building
            (int ExitCode, string Output)? first = await RunScriptAsync("-DatabaseName", name, "-ServerInstance", connection.DataSource);
            first.ShouldNotBeNull("pwsh is not installed");
            first.Value.ExitCode.ShouldBe(0, first.Value.Output);
            (string Hash, string Policy)? afterFirst = await ReadLoginAsync(connection, name, token);
            (int ExitCode, string Output)? second = await RunScriptAsync("-DatabaseName", name, "-ServerInstance", connection.DataSource, "-SkipBuild");
            second.ShouldNotBeNull();
            second.Value.ExitCode.ShouldBe(0, second.Value.Output);
            (string Hash, string Policy)? afterSecond = await ReadLoginAsync(connection, name, token);
            string roles = (string)(await SqlBatch.ScalarAsync(connection, $"""
                SELECT ISNULL(STRING_AGG(r.[name], N','), N'')
                FROM [{name}].[sys].[database_role_members] m
                JOIN [{name}].[sys].[database_principals] r ON (r.[principal_id] = m.[role_principal_id])
                JOIN [{name}].[sys].[database_principals] u ON (u.[principal_id] = m.[member_principal_id])
                WHERE u.[name] = N'{name}';
                """, token))!;

            // Checks
            afterFirst.ShouldNotBeNull().Policy.ShouldBe($"1|0|{name}");
            afterSecond.ShouldNotBeNull().Policy.ShouldBe($"1|0|{name}");
            afterSecond.Value.Hash.ShouldNotBe(afterFirst.Value.Hash, "The second run must give the login a new password");
            roles.ShouldBe("ApplicationRole");
            (first.Value.Output + second.Value.Output).ShouldNotContain("Password=", Case.Insensitive);
            (first.Value.Output + second.Value.Output).ShouldNotContain("PASSWORD =", Case.Insensitive);
        }
        finally
        {
            // Dropping the database and its login, whatever happened
            await SqlBatch.ExecuteAsync(connection, $"""
                IF DB_ID(N'{name}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{name}];
                END;
                IF SUSER_ID(N'{name}') IS NOT NULL DROP LOGIN [{name}];
                """, CancellationToken.None);
        }
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
