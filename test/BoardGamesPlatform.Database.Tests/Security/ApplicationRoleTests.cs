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
/// <para><b>Purpose:</b> least privilege is a rule of the platform: the application executes the procedures, reads the views, passes the
/// table-valued parameters, reads the metadata, and nothing else (doc/dev/database.md, Permission matrix). A right added by mistake, directly or
/// through another role, must break the build.</para>
/// <para><b>Behavior:</b> reads the permission matrix and the role memberships of the role, then plays a member of the role with a temporary user
/// without login, against temporary objects owned by dbo in the public schemas (a procedure of StorProc that writes and reads the audit, a view
/// of Views over the audit) and in another schema. Every temporary object is dropped at the end of its test, even when a check fails. Skipped
/// when no throwaway database is configured (see <see cref="TestDatabase"/>).</para>
/// </remarks>
[Collection(AuditSchemaChanges.Name)]
public sealed class ApplicationRoleTests
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the application role
    /// </summary>
    private const string RoleName = "ApplicationRole";

    #endregion

    #region Variables

    /// <summary>
    /// The schemas the application role may never reach directly
    /// </summary>
    private static readonly string[] _internalSchemas = new string[] { "Logs", "Platform", "History", "Functions", "dbo" };

    /// <summary>
    /// The rights denied on every internal schema
    /// </summary>
    private static readonly string[] _deniedPermissions = new string[] { "SELECT", "INSERT", "UPDATE", "DELETE", "EXECUTE", "ALTER", "REFERENCES", "TAKE OWNERSHIP" };

    /// <summary>
    /// The rights granted to the application role: class, permission, state and schema (empty for the database)
    /// </summary>
    private static readonly string[] _grantedPermissions = new string[]
    {
        "DATABASE|VIEW DEFINITION|GRANT|",
        "SCHEMA|EXECUTE|GRANT|ServerCore",
        "SCHEMA|EXECUTE|GRANT|StorProc",
        "SCHEMA|EXECUTE|GRANT|Types",
        "SCHEMA|SELECT|GRANT|Views",
    };

    /// <summary>
    /// The only permissions of the role AuditPurgeRole: class, permission, state, and schema.object or schema
    /// </summary>
    private static readonly string[] _purgeRolePermissions = new string[]
    {
        "OBJECT_OR_COLUMN|EXECUTE|GRANT|Logs.SP_Logs_Audit_Purge",
        "SCHEMA|VIEW DEFINITION|GRANT|Logs",
    };

    /// <summary>
    /// The permissions of public on the audit table: no user but dbo writes it
    /// </summary>
    private static readonly string[] _publicAuditPermissions = new string[]
    {
        "OBJECT_OR_COLUMN|DELETE|DENY|Logs.Audit",
        "OBJECT_OR_COLUMN|INSERT|DENY|Logs.Audit",
        "OBJECT_OR_COLUMN|UPDATE|DENY|Logs.Audit",
    };

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
    /// Method used to build the full permission matrix the role must hold
    /// </summary>
    /// <returns>
    /// Every expected permission, as class|permission|state|schema, sorted
    /// </returns>
    private static List<string> ExpectedPermissions()
    {
        // Setting the grants, then one deny per right and internal schema
        List<string> expected = new List<string>(_grantedPermissions);
        foreach (string schema in _internalSchemas)
            expected.AddRange(_deniedPermissions.Select(permission => $"SCHEMA|{permission}|DENY|{schema}"));

        // Function's return
        return expected.Order(StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Method used to read the permissions held on objects and on schemas (not on the database) by a principal, outside the schema sys
    /// </summary>
    /// <param name="connection">[Cannot be null] Open connection</param>
    /// <param name="principalName">[Cannot be null] Name of the role</param>
    /// <param name="cancellationToken">Token of the test</param>
    /// <returns>
    /// Every permission, as class|permission|state|schema.object (or class|permission|state|schema for a schema), sorted
    /// </returns>
    private static async Task<List<string>> ReadObjectPermissionsAsync(SqlConnection connection, string principalName, CancellationToken cancellationToken)
    {
        // Reading the permissions on objects and schemas
        const string PermissionsSql = """
            SELECT p.[class_desc], p.[permission_name], p.[state_desc], s.[Securable]
            FROM [sys].[database_permissions] p
            JOIN [sys].[database_principals] r ON (r.[principal_id] = p.[grantee_principal_id])
            CROSS APPLY (SELECT IIF(p.[class_desc] = N'SCHEMA', SCHEMA_NAME(p.[major_id]),
                CONCAT(OBJECT_SCHEMA_NAME(p.[major_id]), N'.', OBJECT_NAME(p.[major_id]))) AS [Securable]) s
            WHERE r.[name] = @PrincipalName AND p.[class_desc] IN (N'OBJECT_OR_COLUMN', N'SCHEMA') AND s.[Securable] NOT LIKE N'sys%'
            """;
        List<string> permissions = new List<string>();
        await using SqlCommand command = new SqlCommand(PermissionsSql, connection);
        command.Parameters.Add("@PrincipalName", SqlDbType.NVarChar, 128).Value = principalName;
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            permissions.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{reader.GetString(2)}|{reader.GetString(3)}");

        // Function's return
        return permissions.Order(StringComparer.Ordinal).ToList();
    }

    #endregion

    #region Publics

    /// <summary>
    /// The application role holds exactly the permissions of the matrix, and belongs to no other role
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> a membership of db_owner or db_datareader would give far more rights without any permission row: both are read.</para>
    /// </remarks>
    [Fact]
    public async Task TheApplicationRoleHoldsExactlyThePermissionMatrixAsync()
    {
        // Reading every permission of the role
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        const string PermissionsSql = """
            SELECT p.[class_desc], p.[permission_name], p.[state_desc], IIF(p.[class_desc] = N'SCHEMA', SCHEMA_NAME(p.[major_id]), N'')
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
        permissions.Order(StringComparer.Ordinal).ToList().ShouldBe(ExpectedPermissions());
        memberships.ShouldBe(0, "The application role must belong to no other role");
    }

    /// <summary>
    /// The role AuditPurgeRole only executes the purge, and public is denied every write of the audit table
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the DENY to public is the barrier of the audit (doc/dev/database.md, The audit): losing it, or a right added to the
    /// purge role, must break the build. The permissions of public on the objects of sys (granted by SQL Server itself) are not read.</para>
    /// </remarks>
    [Fact]
    public async Task ThePurgeRoleAndTheDeniesOfPublicAreExactAsync()
    {
        // Reading the permissions on objects of the purge role and of public
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        List<string> purgeRole = await ReadObjectPermissionsAsync(connection, "AuditPurgeRole", token);
        List<string> publicRole = await ReadObjectPermissionsAsync(connection, "public", token);

        // Checks
        purgeRole.ShouldBe(_purgeRolePermissions);
        publicRole.ShouldBe(_publicAuditPermissions);
    }

    /// <summary>
    /// Every schema of the database is either a public schema of the matrix or an internal schema denied to the application role
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> the matrix above is checked against a list of schemas; a new module (Identity in version 0.8.0) must be added to that
    /// list, with its DENY, or this test fails. The schemas of SQL Server itself (sys, INFORMATION_SCHEMA, guest) and of the fixed roles are not
    /// read.</para>
    /// </remarks>
    [Fact]
    public async Task EverySchemaOfTheDatabaseIsInThePermissionMatrixAsync()
    {
        // Reading the schemas of the database, outside those of SQL Server and of the fixed roles (identifiers from 16384)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        List<string> schemas = new List<string>();
        await using (SqlCommand command = new SqlCommand("SELECT [name] FROM [sys].[schemas] WHERE [schema_id] < 16384 AND [name] NOT IN (N'sys', N'INFORMATION_SCHEMA', N'guest');", connection))
        {
            await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                schemas.Add(reader.GetString(0));
        }
        List<string> known = _grantedPermissions
            .Select(permission => permission.Split('|')[3])
            .Where(schema => schema.Length > 0)
            .Concat(_internalSchemas)
            .ToList();

        // Checks
        schemas.ShouldNotBeEmpty("No schema was read: the check reads nothing");
        schemas.Where(schema => !known.Contains(schema, StringComparer.Ordinal)).ShouldBeEmpty("These schemas are neither granted nor denied to the application role: add them to the matrix of doc/dev/database.md and of this test");
    }

    /// <summary>
    /// A member of the application role uses the procedures, the views and the table-valued parameters, reads the metadata, and is refused
    /// every table, every internal procedure and function, the delete of the audit even with the gate of the purge open, and any creation
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b> checks the effective rights of a real member, not only the declared permissions; in particular that the denies on the
    /// internal schemas do not break the ownership chaining through which a procedure of StorProc writes the audit (whose CHECK constraints call
    /// the functions of the schema Functions).</para>
    /// <para><b>Behavior:</b> creates a temporary user without login, a procedure of StorProc and a view of Views (both owned by dbo, over the
    /// audit), and another schema with a table and a procedure; acts as the user; expects the error 229 for each refused action. The temporary
    /// objects are dropped in every case, with batches that tolerate an object that was never created. The audit row written by the test is
    /// left in the table, as any audit row.</para>
    /// </remarks>
    [Fact]
    public async Task AMemberOfTheRoleCanOnlyUseThePublicSchemasAsync()
    {
        // Setting the names of the temporary objects (from GUIDs: safe to put in the batches)
        CancellationToken token = TestContext.Current.CancellationToken;
        await using SqlConnection connection = await TestDatabase.OpenAsync(token);
        string suffix = Guid.NewGuid().ToString("N");
        string userName = $"TestApplicationUser_{suffix}";
        string procedureName = $"SP_Test_Audit_Write_{suffix}";
        string viewName = $"V_Test_Audit_{suffix}";
        string schemaName = $"TestOtherSchema_{suffix}";
        try
        {
            // Creating the user, member of the role, the public objects over the audit, and another schema with a table and a procedure
            await SqlBatch.ExecuteAsync(connection, $"CREATE USER [{userName}] WITHOUT LOGIN; ALTER ROLE [{RoleName}] ADD MEMBER [{userName}];", token);
            await SqlBatch.ExecuteAsync(connection, $$"""
                EXEC (N'CREATE PROCEDURE [StorProc].[{{procedureName}}] AS
                BEGIN
                    SET NOCOUNT ON;
                    DECLARE @rows [Types].[TVP_Logs_Audit_Insert];
                    INSERT INTO @rows ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
                    VALUES ([Functions].[SystemActorId](), N''Logs'', N''Audit'', NULL, N''{"marker":"{{suffix}}"}'');
                    EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @rows;
                END');
                """, token);
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE VIEW [Views].[{viewName}] AS SELECT [Id] FROM [Logs].[Audit] WHERE [NewValue] LIKE N''%{suffix}%'';');", token);
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE SCHEMA [{schemaName}] AUTHORIZATION [dbo];');", token);
            await SqlBatch.ExecuteAsync(connection, $"CREATE TABLE [{schemaName}].[Data] ([Id] INT NOT NULL);", token);
            await SqlBatch.ExecuteAsync(connection, $"EXEC (N'CREATE PROCEDURE [{schemaName}].[SP_Test_Data_Read] AS SELECT [Id] FROM [{schemaName}].[Data];');", token);

            // Acting as that user
            await SqlBatch.ExecuteAsync(connection, $"EXECUTE AS USER = N'{userName}';", token);
            try
            {
                // Allowed: a procedure of StorProc that writes the audit, a view, the Ping, a table-valued parameter, the metadata
                await SqlBatch.ExecuteAsync(connection, $"EXEC [StorProc].[{procedureName}];", token);
                int rowsSeen = (int)(await SqlBatch.ScalarAsync(connection, $"SELECT COUNT(*) FROM [Views].[{viewName}];", token))!;
                await SqlBatch.ExecuteAsync(connection, "DECLARE @Result NVARCHAR(MAX); EXEC [ServerCore].[SP_ServerCore_Server_Ping] @Result = @Result OUTPUT;", token);
                await SqlBatch.ExecuteAsync(connection, "DECLARE @rows [Types].[TVP_Logs_Audit_Insert];", token);
                int auditTablesSeen = (int)(await SqlBatch.ScalarAsync(connection,
                    "SELECT COUNT(*) FROM [INFORMATION_SCHEMA].[TABLES] WHERE [TABLE_SCHEMA] = N'Logs' AND [TABLE_NAME] = N'Audit';", token))!;

                // Refused: the tables, the internal procedures and functions, the delete with the gate open, another schema, a creation
                await SqlBatch.ShouldFailWithAsync(connection, "SELECT COUNT(*) FROM [Logs].[Audit];", SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection, "DECLARE @rows [Types].[TVP_Logs_Audit_Insert]; EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @rows;",
                    SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection, "DECLARE @result NVARCHAR(MAX); EXEC [Logs].[SP_Logs_Audit_Purge] @Before = '2001-01-01', @Result = @result OUTPUT;",
                    SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection, "SELECT [Functions].[SystemActorId]();", SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection,
                    "EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = 1; DELETE FROM [Logs].[Audit];", SqlBatch.PermissionDenied, token);
                await SqlBatch.ExecuteAsync(connection, "EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = NULL;", token);
                await SqlBatch.ShouldFailWithAsync(connection, $"EXEC [{schemaName}].[SP_Test_Data_Read];", SqlBatch.PermissionDenied, token);
                await SqlBatch.ShouldFailWithAsync(connection, $"SELECT [Id] FROM [{schemaName}].[Data];", SqlBatch.PermissionDenied, token);
                object? canCreateTable = await SqlBatch.ScalarAsync(connection, "SELECT HAS_PERMS_BY_NAME(NULL, N'DATABASE', N'CREATE TABLE');", token);
                object? canAlterStorProc = await SqlBatch.ScalarAsync(connection, "SELECT HAS_PERMS_BY_NAME(N'StorProc', N'SCHEMA', N'ALTER');", token);

                // Checks
                rowsSeen.ShouldBe(1, "The procedure of StorProc must have written the audit through ownership chaining, and the view must read it");
                auditTablesSeen.ShouldBe(1, "The right to read the metadata must show the tables");
                canCreateTable.ShouldBe(0);
                canAlterStorProc.ShouldBe(0);
            }
            finally
            {
                await SqlBatch.ExecuteAsync(connection, "REVERT;", CancellationToken.None);
            }
        }
        finally
        {
            // Dropping every temporary object that exists, even when a step above failed
            await SqlBatch.ExecuteAsync(connection, $"""
                IF OBJECT_ID(N'[StorProc].[{procedureName}]', N'P') IS NOT NULL DROP PROCEDURE [StorProc].[{procedureName}];
                IF OBJECT_ID(N'[Views].[{viewName}]', N'V') IS NOT NULL DROP VIEW [Views].[{viewName}];
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
