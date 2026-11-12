/*
 *	@file	src/BoardGamesPlatform.VeryBack/Procedures/SqlCallException.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Error of a call of the database, which carries the facts of the SQL error but never its message
 */

// Using
using System.Globalization;
using Microsoft.Data.SqlClient;

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Procedures;

/// <summary>
/// Exception used when a call of the database fails, without the message of the SQL error
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> the messages of SQL Server quote values: a duplicate key (2627, 2601), a truncated value (2628), a failed conversion
/// (245), a piece of a connection string. From version 0.8.0 these values are e-mail addresses and nicknames. Whatever logs an exception (the
/// dispatcher of the operations logs the exception of a failed handler, with its inner exceptions) must therefore never receive the
/// <see cref="SqlException"/> itself (doc/dev/database.md, Write pattern, Errors).</para>
/// <para><b>Behavior:</b> keeps the number, the state, the procedure and the line of the first error of the SQL exception, which name what failed
/// without any value, and the name of the procedure called by the Very Back. It holds no inner exception. The message of SQL Server is kept for
/// the errors of the platform only (<see cref="AuditImmutable"/> and <see cref="CheckFailed"/>): the procedures raise them with a literal that
/// names the rule and never a value, so a refusal such as "The actor is unknown" stays readable without depending on a line number.</para>
/// </remarks>
public sealed class SqlCallException : Exception
{
    #region Members

    #region Constants

    /// <summary>
    /// Error number of the triggers of the audit, when a change of an audit row is refused
    /// </summary>
    public const int AuditImmutable = 50001;

    /// <summary>
    /// Error number of the procedures, when a call is refused by one of their checks
    /// </summary>
    public const int CheckFailed = 51000;

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

    /// <summary>
    /// Gets the name of the procedure the Very Back called
    /// </summary>
    public string CalledProcedure { get; }

    /// <summary>
    /// Gets the number of the SQL error (51000 for a check of a procedure, 50001 for the audit, 229 for a permission...)
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets the state of the SQL error
    /// </summary>
    public byte State { get; }

    /// <summary>
    /// Gets the procedure, trigger or function in which the SQL error was raised (empty when it was raised outside one, for a connection)
    /// </summary>
    public string ServerProcedure { get; }

    /// <summary>
    /// Gets the line of the SQL error in that procedure
    /// </summary>
    public int LineNumber { get; }

    #endregion

    #region Delegates

    // #N/A

    #endregion

    #endregion

    #region Constructors

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194), with a general message and no fact of a SQL error
    /// </summary>
    public SqlCallException()
        : this("The call of the database failed")
    {
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194), with a message and no fact of a SQL error (empty names, zero numbers)
    /// </summary>
    /// <param name="message">[Can be null] Message of the exception, which must not quote a value of the database</param>
    public SqlCallException(string? message)
        : base(message)
    {
        // Setting empty facts
        CalledProcedure = string.Empty;
        ServerProcedure = string.Empty;
    }

    /// <summary>
    /// Constructor required by the exception conventions (RCS1194), with a message, an inner exception, and no fact of a SQL error
    /// </summary>
    /// <param name="message">[Can be null] Message of the exception, which must not quote a value of the database</param>
    /// <param name="innerException">[Can be null] Inner exception; never a <see cref="SqlException"/>, whose message this type exists to drop</param>
    /// <exception cref="ArgumentException">If the inner exception is a <see cref="SqlException"/></exception>
    public SqlCallException(string? message, Exception? innerException)
        : base(message, innerException)
    {
        // Check: a SQL exception inside would bring back its message to whatever logs this exception
        if (innerException is SqlException)
            throw new ArgumentException("A SQL exception is never kept inside: use the constructor that takes its facts", nameof(innerException));

        // Setting empty facts
        CalledProcedure = string.Empty;
        ServerProcedure = string.Empty;
    }

    /// <summary>
    /// Constructor used to keep the facts of a SQL error, and drop its message
    /// </summary>
    /// <param name="calledProcedure">[Cannot be null] Name of the procedure the Very Back called</param>
    /// <param name="error">[Cannot be null] The SQL error; neither its message nor the exception itself are kept</param>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    public SqlCallException(string calledProcedure, SqlException error)
        : base(BuildMessage(calledProcedure, error))
    {
        // Setting the facts of the first error (the one SqlException itself reports)
        CalledProcedure = calledProcedure;
        Number = error.Number;
        State = error.State;
        ServerProcedure = error.Procedure ?? string.Empty;
        LineNumber = error.LineNumber;
    }

    #endregion

    #region Destructors

    // #N/A

    #endregion

    #region Methods

    #region Privates

    /// <summary>
    /// Method used to build the message of the exception from the facts of the SQL error only
    /// </summary>
    /// <param name="calledProcedure">[Cannot be null] Name of the procedure the Very Back called</param>
    /// <param name="error">[Cannot be null] The SQL error</param>
    /// <returns>
    /// The message, which names the call, the number, the state, the procedure and the line, and the literal message of an error of the
    /// platform; never the message of another error, which can quote values
    /// </returns>
    /// <exception cref="ArgumentNullException">If a parameter is null</exception>
    private static string BuildMessage(string calledProcedure, SqlException error)
    {
        // Checks
        ArgumentNullException.ThrowIfNull(calledProcedure);
        ArgumentNullException.ThrowIfNull(error);

        // Setting the reason: the literal of the platform, or nothing
        string reason = error.Number is AuditImmutable or CheckFailed
            ? $"\"{error.Message}\""
            : "the message of SQL Server is not kept: it can quote values";

        // Function's return
        return string.Create(CultureInfo.InvariantCulture,
            $"The call of {calledProcedure} failed: SQL error {error.Number}, state {error.State}, in '{error.Procedure}' at line {error.LineNumber} ({reason})");
    }

    #endregion

    #region Publics

    // #N/A

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
