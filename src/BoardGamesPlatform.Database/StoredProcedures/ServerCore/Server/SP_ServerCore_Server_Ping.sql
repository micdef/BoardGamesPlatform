/**
 *	@file @prj/src/BoardGamesPlatform.Database/StoredProcedures/ServerCore/Server/SP_ServerCore_Server_Ping.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Checks that the database answers, and returns the current date and time of the server with its offset
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************************
 *	* Name		| Type		| Size	| Null	| Readonly	| Output	| Brief											*
 *	*************************************************************************************************************
 *	* Result	| NVARCHAR	| MAX	| [ ]	| [ ]		| [x]		| Result of the check formatted in JSON			*
 *	*************************************************************************************************************
 *
 *	Errors :
 *	--------
 *	#N/A
 *
 *	Response Structure :
 *	--------------------
 *	One JSON object (the check has no input row).
 *
 *	Properties:
 *	- ServerTime : Current date and time of the server (SYSDATETIMEOFFSET), ISO 8601, always with an explicit offset:
 *	               "+hh:mm" or "-hh:mm", or "Z" when the server runs on UTC (FOR JSON writes +00:00 as Z);
 *	               0 to 7 fraction digits (FOR JSON leaves out a zero fraction)
 *
 *	Response Examples :
 *	-------------------
 *	- Server on Central European Time :
 *	{
 *		"ServerTime" : "2026-10-30T09:15:00.1234567+01:00"
 *	}
 *
 *	- Server on UTC :
 *	{
 *		"ServerTime" : "2026-10-30T08:15:00.1234567Z"
 *	}
 *
 *	Behavior :
 *	-----------
 *	The procedure only reads the clock of the server: it touches no table, writes nothing, and therefore opens no transaction and writes
 *	no audit. It is the end of the walking skeleton of version 0.3.0: a front calls the Gateway, which calls the Back, which calls the
 *	Very Back, which calls this procedure.
 */
CREATE PROCEDURE [ServerCore].[SP_ServerCore_Server_Ping]
	@Result NVARCHAR(MAX) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
	BEGIN TRY

		/* Output */
		SET @Result = (SELECT SYSDATETIMEOFFSET() AS [ServerTime] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

	END TRY
	BEGIN CATCH
		THROW
	END CATCH
END
