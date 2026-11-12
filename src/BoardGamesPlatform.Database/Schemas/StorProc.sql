/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/StorProc.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the public stored procedures: the only entry point of the application to write data
 *
 *	Information :
 *	-------------
 *	Family			=> Object type
 *	Exposure		=> Public (application role)
 *	Owner			=> dbo
 *	Object Types	=> Stored procedures
 *	Object Naming	=> SP_<Schema>_<Table>_<Action> (the schema and table written, not StorProc)
 *	Filegroup		=> #N/A
 *
 *	Purpose :
 *	---------
 *	- Every write of the application (insert, update, delete) goes through a procedure of this schema
 *	- Each procedure checks its inputs again, writes the audit in the same transaction, and returns one JSON result per input row
 *	- Reads are NOT done here: they go through the views of the schema Views
 *
 *	Access :
 *	--------
 *	- Application role: EXECUTE
 *	- No right on the tables: the procedures reach them through ownership chaining (same owner dbo)
 *	- Every call must be schema-qualified ([StorProc].[SP_...]): see SR0016 in StaticCodeAnalysis.SuppressMessages.xml
 */
CREATE SCHEMA [StorProc] AUTHORIZATION [dbo];
