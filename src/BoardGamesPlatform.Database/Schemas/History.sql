/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/History.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the history tables of the temporal tables
 *
 *	Information :
 *	-------------
 *	Family			=> Object type
 *	Exposure		=> Internal
 *	Owner			=> dbo
 *	Object Types	=> History tables (made and filled by SQL Server, SYSTEM_VERSIONING = ON)
 *	Object Naming	=> <Schema>_<Table>
 *	Filegroup		=> Core
 *
 *	Purpose :
 *	---------
 *	- Every former version of a row of a temporal table, with the period it was valid (UTC)
 *	- Read by an administrator with FOR SYSTEM_TIME on the temporal table, never by the application
 *
 *	Access :
 *	--------
 *	- Application role: nothing, every right explicitly denied (see Security/ApplicationRole.sql)
 */
CREATE SCHEMA [History] AUTHORIZATION [dbo];
