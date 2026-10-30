/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/ServerCore.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the objects that concern the database server itself, not the data of the platform (health check, server time)
 *
 *	Information :
 *	-------------
 *	Family			=> Server
 *	Exposure		=> Public (application role)
 *	Owner			=> dbo
 *	Object Types	=> Stored procedures
 *	Object Naming	=> SP_ServerCore_<Subject>_<Action>
 *	Filegroup		=> #N/A
 *
 *	Purpose :
 *	---------
 *	- Holds what the Very Back calls to check that the database answers, before any data schema exists
 *	- Named ServerCore so that it can never collide with a schema of the engine (sys, INFORMATION_SCHEMA, dbo, guest, db_*)
 *	- The data schemas, filegroups and audit of the platform come with version 0.7.0
 *
 *	Access :
 *	--------
 *	- Application role : EXECUTE on the schema (see Security/ApplicationRole.sql)
 *	- Nothing else : no table lives in this schema
 *	- Every call must be schema-qualified ([ServerCore].[SP_...])
 */
CREATE SCHEMA [ServerCore] AUTHORIZATION [dbo];
