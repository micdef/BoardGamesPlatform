/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Security/ApplicationLoginTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the application login made by the deployment script: the done criterion of version 0.7.0
 */

// Using
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Security;

/// <summary>
/// Tests used to check the application login and user that tools/database/Deploy-TestDatabase.ps1 makes for a database
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the done criterion of version 0.7.0: the database is deployed from scratch by one script; the application user executes
/// the procedures but cannot read the tables; an update of the audit is rejected (doc/dev/database.md, Application principal).</para>
/// <para><b>Behavior:</b> the login and the user are named after the database, as the script makes them; the tests play the real login with
/// EXECUTE AS LOGIN (the password is kept nowhere by the script when it deploys a test database). A database deployed without its login fails
/// these tests: deploy it with the script. Runs in the collection <see cref="AuditSchemaChanges"/>, never while a test of the settings opens the
/// registration switch for a moment. Skipped when no throwaway database is configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
[Collection(AuditSchemaChanges.Name)]
public sealed class ApplicationLoginTests
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// The only server permissions of the login: class, permission and state
    /// </summary>
    private static readonly string[] _expectedServerPermissions = new string[] { "CONNECT SQL|GRANT", "VIEW ANY DATABASE|DENY" };

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
    /// Method used to read the name of the database of the tests, which is also the name of its application login
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The name, already checked by the guard of the tests (TEST_ or DEV_, letters, digits and underscores: safe in a batch)
    /// </returns>
    private static async Task<string> LoginNameAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        // Reading the name, and checking that the script made the login
        string name = (string)(await SqlBatch.ScalarAsync(connection, "SELECT DB_NAME();", cancellationToken))!;
        object? login = await SqlBatch.ScalarAsync(connection, $"SELECT [name] FROM [sys].[sql_logins] WHERE [name] = N'{name}';", cancellationToken);
        login.ShouldNotBeNull($"The login {name} does not exist: deploy the database with tools/database/Deploy-TestDatabase.ps1");

        // Function's return
        return name;
    }

    #endregion

    #region Publics

    /// <summary>
    /// The login of the application checks the password policy without expiration, holds CONNECT SQL and the DENY VIEW ANY DATABASE only, and
    /// its user is a member of the application role only
    /// </summary>
    [Fact]
    public async Task TheApplicationLoginHasOnlyItsRightsAsync()
    {
        // Reading the login, its server permissions and the roles of its user
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string name = await LoginNameAsync(connection, token);
        string policy = (string)(await SqlBatch.ScalarAsync(connection,
            $"SELECT CONCAT([is_policy_checked], N'|', [is_expiration_checked], N'|', [default_database_name]) FROM [sys].[sql_logins] WHERE [name] = N'{name}';", token))!;
        List<string> permissions = new List<string>();
        await using (SqlCommand command = new SqlCommand($"""
            SELECT p.[permission_name], p.[state_desc]
            FROM [sys].[server_permissions] p
            JOIN [sys].[server_principals] s ON (s.[principal_id] = p.[grantee_principal_id])
            WHERE s.[name] = N'{name}'
            """, connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                permissions.Add($"{reader.GetString(0)}|{reader.GetString(1)}");
        }
        string roles = (string)(await SqlBatch.ScalarAsync(connection, $"""
            SELECT ISNULL(STRING_AGG(r.[name], N','), N'')
            FROM [sys].[database_role_members] m
            JOIN [sys].[database_principals] r ON (r.[principal_id] = m.[role_principal_id])
            JOIN [sys].[database_principals] u ON (u.[principal_id] = m.[member_principal_id])
            WHERE u.[name] = N'{name}'
            """, token))!;

        // Checks
        policy.ShouldBe($"1|0|{name}");
        permissions.Order(StringComparer.Ordinal).ShouldBe(_expectedServerPermissions);
        roles.ShouldBe("ApplicationRole");
    }

    /// <summary>
    /// The done criterion of version 0.7.0: the real login of the application executes the procedures and reads the views, but cannot read a
    /// table, and an update of the audit is rejected; it does not even list the other databases of the server
    /// </summary>
    [Fact]
    public async Task TheApplicationLoginExecutesTheProceduresButCannotReadTheTablesAsync()
    {
        // Acting as the login of the application
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string name = await LoginNameAsync(connection, token);
        await SqlBatch.ExecuteAsync(connection, $"EXECUTE AS LOGIN = N'{name}';", token);
        try
        {
            // Allowed: the procedures (the Ping, a read of the settings through their view), the update of a setting with its own value refused
            // as a row (a call that reaches the table through the procedure, without changing the switch)
            await SqlBatch.ExecuteAsync(connection, "DECLARE @Result NVARCHAR(MAX); EXEC [ServerCore].[SP_ServerCore_Server_Ping] @Result = @Result OUTPUT;", token);
            object? switchValue = await SqlBatch.ScalarAsync(connection, "SELECT [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'Registration.IsOpen';", token);
            string? update = (string?)await SqlBatch.ScalarAsync(connection, $"""
                DECLARE @rows [Types].[TVP_Platform_Settings_Update], @result NVARCHAR(MAX);
                INSERT INTO @rows ([Code], [Value]) SELECT [Code], [Value] FROM [Views].[V_Platform_Settings] WHERE [Code] = N'Registration.IsOpen';
                EXEC [StorProc].[SP_Platform_Settings_Update] @Rows = @rows, @IdActor = '5778C6C3-5A1A-4C0A-8878-F36CEECB523A', @Result = @result OUTPUT;
                SELECT @result;
                """, token);
            int otherDatabases = (int)(await SqlBatch.ScalarAsync(connection,
                $"SELECT COUNT(*) FROM [sys].[databases] WHERE [name] NOT IN (N'master', N'tempdb', N'{name}');", token))!;

            // Refused: the tables, and the update of the audit
            await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [Platform].[Settings];", SqlBatch.PermissionDenied, token);
            await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [Logs].[Audit];", SqlBatch.PermissionDenied, token);
            await SqlBatch.ShouldFailWithAsync(connection, "UPDATE [Logs].[Audit] SET [NewValue] = N'{}';", SqlBatch.PermissionDenied, token);

            // Checks
            switchValue.ShouldNotBeNull();
            update.ShouldNotBeNull().ShouldContain("Value is unchanged");
            otherDatabases.ShouldBe(0, "The login of the application must not list the other databases of the server");
        }
        finally
        {
            await SqlBatch.ExecuteAsync(connection, "REVERT;", CancellationToken.None);
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
