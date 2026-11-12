/**
 *	@file @prj/src/BoardGamesPlatform.Database/StoredProcedures/Logs/Audit/SP_Logs_Audit_Insert.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Inserts rows in the audit (Logs.Audit); internal, called by the procedures of StorProc in their own transaction
 *
 *	Parameters :
 *	------------
 *	*********************************************************************************************************************
 *	* Name	| Type							| Size	| Null	| Readonly	| Output	| Brief								*
 *	*********************************************************************************************************************
 *	* Rows	| Types.TVP_Logs_Audit_Insert	|		| [ ]	| [x]		| [ ]		| Audit rows to insert				*
 *	*********************************************************************************************************************
 *
 *	Errors :
 *	--------
 *	*********************************************************************************************************************************************
 *	* Code	| Trigger											| Brief																			*
 *	*********************************************************************************************************************************************
 *	* 51000	| Rows holds no row									| At least one row is required													*
 *	* 51000	| An IdActor is Guid.Empty or not a known actor		| One or more actors are unknown												*
 *	* 51000	| A SchemaName or TableName is empty or whitespace	| One or more schema or table names are empty									*
 *	* 51000	| A row has no OldValue and no NewValue				| One or more rows hold neither an old nor a new value							*
 *	* 51000	| An OldValue or NewValue is not valid JSON			| One or more values are not valid JSON											*
 *	* 51000	| A SchemaName does not exist						| One or more schema names do not exist in the database						*
 *	* 51000	| A TableName does not exist in its schema			| One or more table names do not exist in their schema							*
 *	*********************************************************************************************************************************************
 *
 *	Response Structure :
 *	--------------------
 *	#N/A: the procedure returns nothing. Every row is written, or none (the error rolls back the transaction of the caller).
 *
 *	Response Examples :
 *	-------------------
 *	#N/A
 *
 *	Behavior :
 *	----------
 *	The procedure is the only way rows enter the audit. It checks every row again, although the table-valued parameter and the CHECK constraints
 *	of the table check them too (control in depth): an audit row that cannot be trusted is never written. The date (UTC) and the SQL login of
 *	the session are set by the defaults of the table, never by the caller. It runs inside the transaction of the calling procedure, so a write
 *	and its audit are committed or rolled back together.
 */
CREATE PROCEDURE [Logs].[SP_Logs_Audit_Insert]
	@Rows [Types].[TVP_Logs_Audit_Insert] READONLY
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
	BEGIN TRY
		BEGIN TRANSACTION

			/* Checks */
			IF NOT EXISTS (SELECT 1 FROM @Rows) THROW 51000, 'At least one row is required', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE [Functions].[IsValidActorId]([IdActor]) = 0) THROW 51000, 'One or more actors are unknown', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE NULLIF(TRIM([SchemaName]), N'') IS NULL OR NULLIF(TRIM([TableName]), N'') IS NULL) THROW 51000, 'One or more schema or table names are empty', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE NULLIF(TRIM([OldValue]), N'') IS NULL AND NULLIF(TRIM([NewValue]), N'') IS NULL) THROW 51000, 'One or more rows hold neither an old nor a new value', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE ISJSON(ISNULL([OldValue], N'{}')) = 0 OR ISJSON(ISNULL([NewValue], N'{}')) = 0) THROW 51000, 'One or more values are not valid JSON', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE [Functions].[IsValidSchemaName]([SchemaName]) = 0) THROW 51000, 'One or more schema names do not exist in the database', 1;
			IF EXISTS (SELECT 1 FROM @Rows WHERE [Functions].[IsValidTableName]([SchemaName], [TableName]) = 0) THROW 51000, 'One or more table names do not exist in their schema', 1;

			/* Insert */
			INSERT INTO [Logs].[Audit] ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
			SELECT r.[IdActor], r.[SchemaName], r.[TableName], r.[OldValue], r.[NewValue]
			FROM @Rows r;

		COMMIT TRANSACTION
	END TRY
	BEGIN CATCH
		IF @@TRANCOUNT > 0
			ROLLBACK TRANSACTION;
		THROW
	END CATCH
END
