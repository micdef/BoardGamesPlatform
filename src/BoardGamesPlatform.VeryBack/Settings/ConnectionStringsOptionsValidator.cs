/*
 *	@file	src/BoardGamesPlatform.VeryBack/Settings/ConnectionStringsOptionsValidator.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Checks the "ConnectionStrings" settings at startup
 */

// Using
using System.Buffers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Settings;

/// <summary>
/// Validator of <see cref="ConnectionStringsOptions"/>
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> fail closed: a Very Back without a connection string refuses to start, instead of failing at the first request.</para>
/// <para><b>Behavior:</b> fails when the main connection string is missing, empty or white spaces, or when the SQL client cannot read it.
/// The message names the setting and never repeats its value nor the error of the SQL client: for a malformed string, that error quotes the
/// unknown keyword, which can be a piece of a password holding a semicolon. Whether the server, the database and the login are right is
/// only known when a connection opens. Outside the Development environment, the connection must also be encrypted (doc/dev/database.md,
/// Application principal): <c>Encrypt</c> is <c>Mandatory</c> (the default) or <c>Strict</c>, and the certificate of the server is checked,
/// unless the server is the local machine, whose SQL Server presents a self-signed certificate (the rule of the deployment script).</para>
/// </remarks>
public sealed class ConnectionStringsOptionsValidator : IValidateOptions<ConnectionStringsOptions>
{
    #region Members

    #region Constants

    // #N/A

    #endregion

    #region Variables

    /// <summary>
    /// Names of the local machine, whose self-signed certificate may be trusted (with or without the prefix tcp: and a port or an instance)
    /// </summary>
    private static readonly string[] _localServers = new string[] { "localhost", ".", "(local)", "127.0.0.1", "[::1]", "::1" };

    /// <summary>
    /// Characters after which a data source names a port (",1433") or an instance ("\GAMES"), not the machine
    /// </summary>
    private static readonly SearchValues<char> _serverSeparators = SearchValues.Create(",\\");

    /// <summary>
    /// Whether the rules of a developer machine apply (no rule on the encryption)
    /// </summary>
    private readonly bool _isDevelopment;

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

    /// <summary>
    /// Constructor used to check the connection strings with the rules of a server (the encryption required)
    /// </summary>
    public ConnectionStringsOptionsValidator()
    {
        // Setting
        _isDevelopment = false;
    }

    /// <summary>
    /// Constructor used to check the connection strings with the rules of the environment of the host
    /// </summary>
    /// <param name="svcEnvironment">[Cannot be null] Environment of the host</param>
    /// <exception cref="ArgumentNullException">If the environment is null</exception>
    public ConnectionStringsOptionsValidator(IHostEnvironment svcEnvironment)
    {
        // Check
        if (svcEnvironment is null)
            throw new ArgumentNullException(nameof(svcEnvironment), $"The value of the parameter: {nameof(svcEnvironment)} cannot be null");

        // Setting
        _isDevelopment = svcEnvironment.IsDevelopment();
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to know whether a data source names the local machine
    /// </summary>
    /// <param name="dataSource">[Cannot be null] Data source of the connection string ("tcp:localhost,1433", "sql01\\GAMES"...)</param>
    /// <returns>
    /// True  ==> the server is the local machine
    /// False ==> it is another machine
    /// </returns>
    private static bool IsLocalServer(string dataSource)
    {
        // Keeping the name of the machine only: without the protocol prefix, the port and the instance
        string server = dataSource.Trim();
        if (server.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            server = server[4..];
        int end = server.AsSpan().IndexOfAny(_serverSeparators);
        if (end >= 0)
            server = server[..end];

        // Function's return
        return _localServers.Contains(server.Trim(), StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region Publics

    /// <summary>
    /// Method used to know whether the SQL client can read a connection string
    /// </summary>
    /// <remarks>
    /// <para><b>Behavior:</b> builds a connection object with the string, as the call of a procedure does, without opening it: nothing
    /// reaches the network. The error of the SQL client is dropped on purpose, since its message can quote a piece of the string.</para>
    /// </remarks>
    /// <param name="connectionString">[Cannot be null] Connection string to read</param>
    /// <returns>
    /// True if the SQL client reads it, false otherwise
    /// </returns>
    public static bool IsWellFormed(string connectionString)
    {
        // Reading the string with the parser of the SQL client
        try
        {
            using SqlConnection probe = new SqlConnection(connectionString);

            // Function's return
            return true;
        }
        catch (ArgumentException)
        {
            // Function's return
            return false;
        }
    }

    /// <summary>
    /// Method used to check the connection strings of the Very Back
    /// </summary>
    /// <param name="name">Name of the options instance (unused: the settings are not named)</param>
    /// <param name="options">[Cannot be null] Settings to check</param>
    /// <returns>
    /// Success, or a failure naming the missing or unreadable setting
    /// </returns>
    public ValidateOptionsResult Validate(string? name, ConnectionStringsOptions options)
    {
        // Check
        if (string.IsNullOrWhiteSpace(options.Main))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is missing: set it with " +
                "'dotnet user-secrets' in development or in the vault of the Very Back on a server (see doc/adm/configuration.md)");
        }
        if (!IsWellFormed(options.Main))
        {
            return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} is not a connection string " +
                "the SQL client can read (unknown keyword, invalid value or unbalanced quote; a value holding a semicolon must be quoted)");
        }

        // Checking the encryption on a server (the builder reads the string the check above accepted)
        if (!_isDevelopment)
        {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder(options.Main);
            if (builder.Encrypt == SqlConnectionEncryptOption.Optional)
            {
                return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} must encrypt the " +
                    "connection outside Development: Encrypt=Mandatory (the default) or Encrypt=Strict, never False or Optional");
            }
            if (builder.TrustServerCertificate && (!IsLocalServer(builder.DataSource) || (!string.IsNullOrEmpty(builder.FailoverPartner) && !IsLocalServer(builder.FailoverPartner))))
            {
                return ValidateOptionsResult.Fail($"{ConnectionStringsOptions.SectionName}:{nameof(ConnectionStringsOptions.Main)} trusts the certificate " +
                    "of a remote SQL Server (or failover partner) outside Development: remove TrustServerCertificate, and give the server a certificate the machine trusts");
            }
        }

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
