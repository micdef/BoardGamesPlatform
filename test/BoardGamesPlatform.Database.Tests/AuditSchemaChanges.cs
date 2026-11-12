/*
 *	@file	test/BoardGamesPlatform.Database.Tests/AuditSchemaChanges.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Collection of the test classes that change the schema around the audit, so that they never run at the same time
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.Database.Tests;

/// <summary>
/// Collection used to run one after the other the test classes that create objects on or over Logs.Audit
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> two tests that create a temporary trigger on Logs.Audit at the same time make SQL Server fail one of them ("The referenced
/// entity 'inserted' was modified during DDL execution"), and a temporary procedure or view over the audit can meet the same race. xUnit runs
/// the classes of one collection in sequence; the other classes keep running in parallel.</para>
/// <para><b>Behavior:</b> holds no shared state: the collection only orders the classes.</para>
/// </remarks>
[CollectionDefinition(Name)]
public sealed class AuditSchemaChanges
{
    #region Members

    #region Constants

    /// <summary>
    /// Name of the collection
    /// </summary>
    public const string Name = "Schema changes around the audit";

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
