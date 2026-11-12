/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/Views.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the public views: the only entry point of the application to read data
 *
 *	Information :
 *	-------------
 *	Family			=> Object type
 *	Exposure		=> Public (application role)
 *	Owner			=> dbo
 *	Object Types	=> Views
 *	Object Naming	=> V_<Schema>_<Table>
 *	Filegroup		=> #N/A (the indexes of the indexed views go to the filegroup Access)
 *
 *	Purpose :
 *	---------
 *	- Every read of the application goes through a view of this schema, with the columns it needs, never SELECT *
 *	- The history of the temporal tables is not exposed here
 *
 *	Access :
 *	--------
 *	- Application role: SELECT
 *	- No right on the tables: the views reach them through ownership chaining (same owner dbo)
 */
CREATE SCHEMA [Views] AUTHORIZATION [dbo];
