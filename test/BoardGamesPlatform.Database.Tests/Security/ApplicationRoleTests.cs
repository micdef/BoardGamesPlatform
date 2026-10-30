/*
 *	@file	test/BoardGamesPlatform.Database.Tests/Security/ApplicationRoleTests.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks that the application role has exactly the rights it needs, and nothing more
 */

// Using
using System.Data;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests.Security;

/// <summary>
/// Tests used to check the permissions of the database role ApplicationRole
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> least privilege is a rule of the platform: the application may only execute the procedures of the granted schemas.
/// A right added by mistake, directly or through another role, must break the build.</para>
/// <para><b>Behavior:</b> reads the permission matrix and the role memberships of the role, then plays a member of the role with a temporary
/// user without login, against a temporary schema holding a table and a procedure. Every temporary object is dropped at the end of its test,
/// even when a check fails. Skipped when no throwaway database is configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
public sealed class ApplicationRoleTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the application role
    /// </summary>
    private const string RoleName = "ApplicationRole";

    /// <summary>
    /// Error number of SQL Server when a permission is denied on an object
    /// </summary>
    private const int PermissionDeniedError = 229;

    #endregion

    #region Variables

    /// <summary>
    /// The only permission the application role may hold: class, permission, state and schema
    /// </summary>
    private static readonly string[] _expectedPermissions = new string[] { "SCHEMA|EXECUTE|GRANT|ServerCore" };

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
    /// Method used to run a T-SQL batch that returns nothing
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Batch to run; the names it holds are built by the tests from constants and GUIDs, never from input</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the batch has run
    /// </returns>
    private static async Task ExecuteAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the batch
        await using SqlCommand command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run a T-SQL query that returns one value
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="sql">[Cannot be null] Query to run</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// The first column of the first row
    /// </returns>
    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the query
        await using SqlCommand command = new SqlCommand(sql, connection);

        // Function's return
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Method used to run a T-SQL batch that must be refused for lack of permission
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection, acting as the tested user</param>
    /// <param name="sql">[Cannot be null] Batch that must be refused</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// A task that completes when the refusal has been checked
    /// </returns>
    private static async Task ShouldBeDeniedAsync(SqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        // Running the batch: only the "permission denied" error proves the refusal
        SqlException exception = await Should.ThrowAsync<SqlException>(() => ExecuteAsync(connection, sql, cancellationToken));
        exception.Number.ShouldBe(PermissionDeniedError, $"Expected a permission denial for: {sql}");
    }

    #endregion

    #region Publics

    /// <summary>
    /// The application role holds exactly one permission, EXECUTE on the schema ServerCore, and belongs to no other role
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a membership of db_owner or db_datareader would give far more rights without any permission row: both are read.</para>
    /// </remarks>
    [Fact]
    public async Task TheApplicationRoleCanOnlyExecuteTheGrantedSchemaAsync()
    {
        // Reading every permission of the role
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        const string PermissionsSql = """
            SELECT p.[class_desc], p.[permission_name], p.[state_desc], ISNULL(SCHEMA_NAME(p.[major_id]), N'')
            FROM [sys].[database_permissions] p
            JOIN [sys].[database_principals] r ON (r.[principal_id] = p.[grantee_principal_id])
            WHERE r.[name] = @RoleName
            """;
        List<string> permissions = new List<string>();
        await using (SqlCommand command = new SqlCommand(PermissionsSql, connection))
        {
            command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 128).Value = RoleName;
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                permissions.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetString(3)}");
        }

        // Reading the roles the role belongs to
        const string MembershipsSql = """
            SELECT COUNT(*)
            FROM [sys].[database_role_members] m
            JOIN [sys].[database_principals] r ON (r.[principal_id] = m.[member_principal_id])
            WHERE r.[name] = @RoleName
            """;
        int memberships;
        await using (SqlCommand command = new SqlCommand(MembershipsSql, connection))
        {
            command.Parameters.Add("@RoleName", SqlDbType.NVarChar, 128).Value = RoleName;
            memberships = (int)(await command.ExecuteScalarAsync(token))!;
        }

        // Checks
        permissions.ShouldBe(_expectedPermissions);
        memberships.ShouldBe(0, "The application role must belong to no other role");
    }

    /// <summary>
    /// A member of the application role can call the Ping procedure, and is refused everything else: another schema, a table, a creation
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> checks the effective rights of a real member, not only the declared permissions.</para>
    /// <para><b>Behavior:</b> creates a temporary user without login and a temporary schema holding a table and a procedure, acts as the user,
    /// and expects the error 229 (permission denied) for each refused action. The temporary objects are dropped in every case, with batches that
    /// tolerate an object that was never created.</para>
    /// </remarks>
    [Fact]
    public async Task AMemberOfTheRoleCanOnlyCallTheGrantedProceduresAsync()
    {
        // Setting the names of the temporary objects (from GUIDs: safe to put in the batches)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string suffix = Guid.NewGuid().ToString("N");
        string userName = $"TestApplicationUser_{suffix}";
        string schemaName = $"TestOtherSchema_{suffix}";
        try
        {
            // Creating the user, member of the role, and another schema with a table and a procedure
            await ExecuteAsync(connection, $"CREATE USER [{userName}] WITHOUT LOGIN; ALTER ROLE [{RoleName}] ADD MEMBER [{userName}];", token);
            await ExecuteAsync(connection, $"EXEC (N'CREATE SCHEMA [{schemaName}] AUTHORIZATION [dbo];');", token);
            await ExecuteAsync(connection, $"CREATE TABLE [{schemaName}].[Data] ([Id] INT NOT NULL);", token);
            await ExecuteAsync(connection, $"EXEC (N'CREATE PROCEDURE [{schemaName}].[SP_Test_Data_Read] AS SELECT [Id] FROM [{schemaName}].[Data];');", token);

            // Acting as that user
            await ExecuteAsync(connection, $"EXECUTE AS USER = N'{userName}';", token);
            try
            {
                // Allowed: the Ping procedure
                await ExecuteAsync(connection, "DECLARE @Result NVARCHAR(MAX); EXEC [ServerCore].[SP_ServerCore_Server_Ping] @Result = @Result OUTPUT;", token);

                // Refused: a procedure and a table of another schema, a creation
                await ShouldBeDeniedAsync(connection, $"EXEC [{schemaName}].[SP_Test_Data_Read];", token);
                await ShouldBeDeniedAsync(connection, $"SELECT [Id] FROM [{schemaName}].[Data];", token);
                object? canCreateTable = await ScalarAsync(connection, "SELECT HAS_PERMS_BY_NAME(NULL, N'DATABASE', N'CREATE TABLE');", token);
                object? canAlterServerCore = await ScalarAsync(connection, "SELECT HAS_PERMS_BY_NAME(N'ServerCore', N'SCHEMA', N'ALTER');", token);

                // Checks
                canCreateTable.ShouldBe(0);
                canAlterServerCore.ShouldBe(0);
            }
            finally
            {
                await ExecuteAsync(connection, "REVERT;", CancellationToken.None);
            }
        }
        finally
        {
            // Dropping every temporary object that exists, even when a step above failed
            await ExecuteAsync(connection, $"""
                IF OBJECT_ID(N'[{schemaName}].[SP_Test_Data_Read]', N'P') IS NOT NULL DROP PROCEDURE [{schemaName}].[SP_Test_Data_Read];
                IF OBJECT_ID(N'[{schemaName}].[Data]', N'U') IS NOT NULL DROP TABLE [{schemaName}].[Data];
                IF SCHEMA_ID(N'{schemaName}') IS NOT NULL EXEC (N'DROP SCHEMA [{schemaName}];');
                IF USER_ID(N'{userName}') IS NOT NULL
                BEGIN
                    IF IS_ROLEMEMBER(N'{RoleName}', N'{userName}') = 1 ALTER ROLE [{RoleName}] DROP MEMBER [{userName}];
                    DROP USER [{userName}];
                END
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
