/**
 *	@file @prj/src/BoardGamesPlatform.Database/Security/AuditTablePermissions.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Denies every write of the audit table to every user of the database but dbo
 *
 *	Information :
 *	-------------
 *	Family			=> Security
 *	Securable		=> Logs.Audit (table)
 *	Grantee			=> public (every user of the database is a member of it)
 *
 *	Purpose :
 *	---------
 *	- The barrier of the audit: no user but dbo can insert, update or delete a row directly, members of db_datawriter and db_owner included
 *	  (a DENY wins over any grant, explicit or through a fixed role)
 *	- The gate of the purge (SESSION_CONTEXT) is not a barrier: anyone can set its key. With this DENY, a user who sets it still gets the
 *	  error 229 on a DELETE (proved by the security review of doc/dev/database.md)
 *	- The procedures still write and purge the table: ownership chaining (same owner dbo) checks no permission on the table it reaches
 *
 *	Access :
 *	--------
 *	*****************************************************************************************
 *	* Securable		| Permission				| Grantee	| Why								*
 *	*****************************************************************************************
 *	* Logs.Audit	| DENY INSERT				| public	| No forged audit row				*
 *	* Logs.Audit	| DENY UPDATE				| public	| Audit rows are immutable			*
 *	* Logs.Audit	| DENY DELETE				| public	| Only the purge deletes, by dates	*
 *	*****************************************************************************************
 *	- dbo, db_owner (who can revoke this DENY) and sysadmin stay administrators: what they do is seen by a server-level audit, not prevented
 */
DENY INSERT, UPDATE, DELETE ON OBJECT::[Logs].[Audit] TO [public];
