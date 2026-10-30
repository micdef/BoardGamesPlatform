/*
 *	@file	src/BoardGamesPlatform.VeryBack/Procedures/IPingProcedure.cs
 *	@author	Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief	Call of the Ping stored procedure, seen by the Very Back
 */

// Using
// #N/A

// Static using
// #N/A

// Named using
// #N/A

// Namespace
namespace BoardGamesPlatform.VeryBack.Procedures;

/// <summary>
/// Interface of the call of the Ping stored procedure
/// </summary>
/// <remarks>
/// <para><b>Purpose:</b> lets the Ping operation be tested without a database, with a fake procedure.</para>
/// <para><b>Behavior:</b> asks the database for its current date and time.</para>
/// </remarks>
public interface IPingProcedure
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

    #region Methods

    #region Publics

    /// <summary>
    /// Method used to get the current date and time of the database server
    /// </summary>
    /// <param name="cancellationToken">Token used to stop waiting for the database</param>
    /// <returns>
    /// The date and time of the server, with its offset
    /// </returns>
    /// <exception cref="InvalidDataException">If the database answers something else than the documented result</exception>
    /// <exception cref="OperationCanceledException">If the call is canceled</exception>
    public Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken);

    #endregion

    #endregion
}
