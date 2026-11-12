/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/Functions.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the scalar functions used by the checks of the tables and of the procedures
 *
 *	Information :
 *	-------------
 *	Family			=> Object type
 *	Exposure		=> Internal
 *	Owner			=> dbo
 *	Object Types	=> Scalar functions
 *	Object Naming	=> IsValid<Object>, <Constant>
 *	Filegroup		=> #N/A
 *
 *	Purpose :
 *	---------
 *	- One function per rule that several objects check (a known actor, an existing schema or table), so that the rule lives in one place
 *	- Used by CHECK constraints and by the procedures of StorProc and Logs
 *
 *	Access :
 *	--------
 *	- Application role: nothing, every right explicitly denied (see Security/ApplicationRole.sql)
 *	- IsValidSchemaName and IsValidTableName read INFORMATION_SCHEMA, whose rows are those the caller may see: they need the right to read
 *	  the metadata (VIEW DEFINITION) of the application role
 */
CREATE SCHEMA [Functions] AUTHORIZATION [dbo];
