/**
 *	@file @prj/src/BoardGamesPlatform.Database/Triggers/Logs/Audit/TR_Logs_Audit_IOfDelete.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Refuses every delete of the audit, except the one of its purge
 *
 *	Information :
 *	-------------
 *	SQL Name	=> TR_Logs_Audit_IOfDelete
 *	SQL Schema	=> Logs
 *	Table On	=> Logs.Audit
 *	Type		=> Instead Of
 *	Command		=> Delete
 *	Actions :
 *	- Throw error 50001 with the message "Delete Prohibited => Audit records are immutable"
 *	- Only exception: when SESSION_CONTEXT(N'AllowAuditPurge') = 1, the rows are really deleted. The key is set, then reset to NULL, only by
 *	  Logs.SP_Logs_Audit_Purge, inside its transaction
 *
 *	Behavior :
 *	----------
 *	The gate only protects against a DELETE by mistake: the session key can be set by anyone, so the real barrier is the right to delete,
 *	which only dbo and the administrators of the database hold. The application role holds no right on the schema Logs.
 *	The key is read once, into a variable, before any statement that touches rows, and the delete runs serially (MAXDOP 1): SQL Server 2025
 *	documents that SESSION_CONTEXT may return wrong results in parallel plans (known issue, workaround trace flag 11042).
 */
CREATE TRIGGER [Logs].[TR_Logs_Audit_IOfDelete]
ON [Logs].[Audit]
INSTEAD OF DELETE
AS
BEGIN
	SET NOCOUNT ON;

	/* Reading the gate once */
	DECLARE @isPurge BIT = IIF(CAST(SESSION_CONTEXT(N'AllowAuditPurge') AS INT) = 1, 1, 0);

	/* Purge: the rows are really deleted */
	IF @isPurge = 1
	BEGIN
		DELETE a
		FROM [Logs].[Audit] a
		INNER JOIN deleted d ON (d.[Id] = a.[Id])
		OPTION (MAXDOP 1);
		RETURN;
	END;

	/* Any other delete */
	THROW 50001, 'Delete Prohibited => Audit records are immutable', 1;
END;
