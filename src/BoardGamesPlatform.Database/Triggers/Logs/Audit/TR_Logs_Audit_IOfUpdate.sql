/**
 *	@file @prj/src/BoardGamesPlatform.Database/Triggers/Logs/Audit/TR_Logs_Audit_IOfUpdate.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Refuses every update of the audit
 *
 *	Information :
 *	-------------
 *	SQL Name	=> TR_Logs_Audit_IOfUpdate
 *	SQL Schema	=> Logs
 *	Table On	=> Logs.Audit
 *	Type		=> Instead Of
 *	Command		=> Update
 *	Actions :
 *	- Throw error 50001 with the message "Update Prohibited => Audit records are immutable", whoever the caller is
 *	- No exception: an audit row is never changed, not even by the purge
 *
 *	Behavior :
 *	----------
 *	The trigger runs instead of the UPDATE, so the rows are never touched; the error rolls the statement back (and the whole transaction of a
 *	procedure, which runs with XACT_ABORT ON). A DML trigger does not see TRUNCATE, ALTER or DISABLE TRIGGER: they need ALTER on the table,
 *	which only dbo and the administrators of the database hold (doc/dev/database.md, The audit).
 */
CREATE TRIGGER [Logs].[TR_Logs_Audit_IOfUpdate]
ON [Logs].[Audit]
INSTEAD OF UPDATE
AS
BEGIN
	SET NOCOUNT ON;
	THROW 50001, 'Update Prohibited => Audit records are immutable', 1;
END;
