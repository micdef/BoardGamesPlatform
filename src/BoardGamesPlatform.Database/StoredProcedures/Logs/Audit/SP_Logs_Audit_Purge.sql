/**
 *	@file @prj/src/BoardGamesPlatform.Database/StoredProcedures/Logs/Audit/SP_Logs_Audit_Purge.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Purges the audit of the rows written before a date (UTC), and records the purge itself in the audit; an act of administration
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************************************************
 *	* Name		| Type		| Size	| Null	| Readonly	| Output	| Brief																*
 *	*************************************************************************************************************************************
 *	* Before	| DATE		|		| [ ]	| [ ]		| [ ]		| Rows written before this day (00:00 UTC) are deleted				*
 *	* Result	| NVARCHAR	| MAX	| [ ]	| [ ]		| [x]		| What was purged, formatted in JSON								*
 *	*************************************************************************************************************************************
 *
 *	Errors :
 *	--------
 *	*************************************************************************************************************************************
 *	* Code	| Trigger						| Brief																						*
 *	*************************************************************************************************************************************
 *	* 51000	| Before is NULL				| The date of the purge is required															*
 *	* 51000	| Before is today or later (UTC)| The date of the purge must be earlier than today (UTC): the rows of the current day stay	*
 *	* 51000	| AllowAuditPurge already set	| The gate of the purge is already set in this session: a key set read-only could not be	*
 *	*		|   in the session				|   closed afterwards, so the purge refuses to start										*
 *	* 15664	| AllowAuditPurge set read-only	| The engine refuses to open the gate (a read-only key cannot be changed): the purge fails	*
 *	*		|   to NULL in the session		|   before any delete, and the transaction is rolled back									*
 *	*************************************************************************************************************************************
 *
 *	Response Structure :
 *	--------------------
 *	One JSON object (the purge has no input row).
 *
 *	Properties:
 *	- Before         : The date given (the purged rows were written before it, 00:00 UTC)
 *	- Rows           : Number of audit rows deleted (0 when there was nothing to purge)
 *	- OldestActionDT : Date and time (UTC) of the oldest row deleted; absent when no row was deleted
 *	- NewestActionDT : Date and time (UTC) of the newest row deleted; absent when no row was deleted
 *
 *	Response Examples :
 *	-------------------
 *	- Rows purged :
 *	{
 *		"Before" : "2026-01-01",
 *		"Rows" : 1250,
 *		"OldestActionDT" : "2025-01-01T08:00:00.1234567",
 *		"NewestActionDT" : "2025-12-31T23:59:58.7654321"
 *	}
 *
 *	- Nothing to purge :
 *	{
 *		"Before" : "2026-01-01",
 *		"Rows" : 0
 *	}
 *
 *	Behavior :
 *	----------
 *	In one transaction, the procedure opens the gate of the delete trigger (session key AllowAuditPurge = 1) just before the delete, deletes the
 *	rows written before the date, closes the gate (NULL), then writes one audit row of the purge itself (actor: the platform; login: the
 *	administrator who ran it; new value: the JSON returned). The rows that record a purge (the rows about Logs.Audit itself, which only the purge
 *	writes) are never deleted: the chain of the purges stays complete, and a purge can never hide an earlier one. On any error, everything is
 *	rolled back, the gate is closed if the procedure opened it, and the error is rethrown; a client that cancels the call (an attention, not
 *	caught by CATCH) can leave the gate open in its own session, which the reuse of a pooled connection clears. The procedure lives in the
 *	schema Logs, which the application role cannot execute: a purge is an act of administration, never of the platform (role AuditPurgeRole).
 *	The archive of the rows before their purge is a decision of the project lead (doc/dev/database.md, Decisions left to the project lead).
 */
CREATE PROCEDURE [Logs].[SP_Logs_Audit_Purge]
	@Before DATE,
	@Result NVARCHAR(MAX) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	/* Whether this call opened the gate, so that the CATCH closes only a gate of its own */
	DECLARE @gateOpened BIT = 0;

	BEGIN TRY
		BEGIN TRANSACTION

			/* Declare Types */
			DECLARE @purged AS TABLE
			(
				[ActionDT] DATETIME2(7) NOT NULL
			);
			DECLARE @audit AS [Types].[TVP_Logs_Audit_Insert];

			/* Checks */
			IF @Before IS NULL THROW 51000, 'The date of the purge is required', 1;
			IF @Before >= CAST(SYSUTCDATETIME() AS DATE) THROW 51000, 'The date of the purge must be earlier than today (UTC)', 1;
			IF SESSION_CONTEXT(N'AllowAuditPurge') IS NOT NULL THROW 51000, 'The gate of the purge is already set in this session', 1;

			/* Open the gate */
			SET @gateOpened = 1;
			EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = 1;

			/* Delete, except the rows that record a purge */
			DELETE FROM [Logs].[Audit]
			OUTPUT deleted.[ActionDT] INTO @purged ([ActionDT])
			WHERE [ActionDT] < CAST(@Before AS DATETIME2(7))
				AND NOT ([SchemaName] = N'Logs' AND [TableName] = N'Audit');

			/* Close the gate */
			EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = NULL;
			SET @gateOpened = 0;

			/* Output */
			SET @Result = (
				SELECT @Before AS [Before], COUNT(*) AS [Rows], MIN(p.[ActionDT]) AS [OldestActionDT], MAX(p.[ActionDT]) AS [NewestActionDT]
				FROM @purged p
				FOR JSON PATH, WITHOUT_ARRAY_WRAPPER);

			/* Audit of the purge itself */
			INSERT INTO @audit ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
			VALUES ([Functions].[SystemActorId](), N'Logs', N'Audit', NULL, @Result);
			EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @audit;

		COMMIT TRANSACTION
	END TRY
	BEGIN CATCH
		IF @@TRANCOUNT > 0
			ROLLBACK TRANSACTION;
		IF @gateOpened = 1
			EXEC [sys].[sp_set_session_context] @key = N'AllowAuditPurge', @value = NULL;
		THROW
	END CATCH
END
