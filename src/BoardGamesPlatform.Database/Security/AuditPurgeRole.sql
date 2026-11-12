/**
 *	@file @prj/src/BoardGamesPlatform.Database/Security/AuditPurgeRole.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Database role of the administrators who purge the audit, without being members of db_owner
 *
 *	Information :
 *	-------------
 *	Family			=> Security
 *	Exposure		=> Administration
 *	Owner			=> dbo
 *	Members			=> None in the project: an administrator adds the account that runs the purge (a SQL Agent job, an operator)
 *
 *	Purpose :
 *	---------
 *	- Least privilege for the purge: its members execute Logs.SP_Logs_Audit_Purge and nothing else
 *	- The purge deletes the rows through ownership chaining, although the table denies DELETE to public
 *
 *	Access :
 *	--------
 *	- EXECUTE on Logs.SP_Logs_Audit_Purge
 *	- VIEW DEFINITION on the schema Logs: the purge writes an audit row of itself, whose CHECK constraints (Functions.IsValidSchemaName,
 *	  Functions.IsValidTableName) read the metadata with the rights of the caller; without it, every purge fails its own audit
 *	- Nothing else (checked by the permission test of BoardGamesPlatform.Database.Tests)
 */
CREATE ROLE [AuditPurgeRole] AUTHORIZATION [dbo];
GO

GRANT EXECUTE ON OBJECT::[Logs].[SP_Logs_Audit_Purge] TO [AuditPurgeRole];
GO

GRANT VIEW DEFINITION ON SCHEMA::[Logs] TO [AuditPurgeRole];
