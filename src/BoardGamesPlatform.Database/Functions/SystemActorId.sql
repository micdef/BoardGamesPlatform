/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/SystemActorId.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Returns the identifier of the actor "the platform itself", the only actor known before the accounts of version 0.8.0
 *
 *	Parameters :
 *	------------
 *	#N/A
 *
 *	Return :
 *	--------
 *	UNIQUEIDENTIFIER : 5778C6C3-5A1A-4C0A-8878-F36CEECB523A, fixed forever: audit rows written with it must keep their meaning
 *
 *	Behavior :
 *	----------
 *	The platform is the actor of the changes that no player asks for: the deployment, the settings written by an administrator through the
 *	platform, the purge of the audit. From version 0.8.0, it is also a row of the users, so that a foreign key can reach it.
 */
CREATE FUNCTION [Functions].[SystemActorId]()
	RETURNS UNIQUEIDENTIFIER
	WITH SCHEMABINDING
AS
BEGIN
	RETURN CAST('5778C6C3-5A1A-4C0A-8878-F36CEECB523A' AS UNIQUEIDENTIFIER);
END
