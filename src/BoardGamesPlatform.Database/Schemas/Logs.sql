/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/Logs.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the module Logs: the immutable audit of every change made through the platform
 *
 *	Information :
 *	-------------
 *	Family			=> Module
 *	Exposure		=> Internal
 *	Owner			=> dbo
 *	Object Types	=> Tables, internal stored procedures, triggers
 *	Object Naming	=> <Table>, SP_Logs_<Table>_<Action>, TR_Logs_<Table>_IOf<Command>
 *	Filegroup		=> Core
 *
 *	Purpose :
 *	---------
 *	- Audit: who changed which table and when, with the row before and after the change (JSON), and the SQL login that wrote it
 *	- Immutability: INSTEAD OF triggers refuse UPDATE and DELETE; only Logs.SP_Logs_Audit_Purge deletes rows, by dates
 *	- The triggers live here and not in a dedicated schema: a DML trigger belongs to the schema of its table
 *
 *	Access :
 *	--------
 *	- Application role: nothing, every right explicitly denied (see Security/ApplicationRole.sql)
 *	- Logs.SP_Logs_Audit_Insert is called by the procedures of StorProc only (ownership chaining, same owner dbo)
 *	- Logs.SP_Logs_Audit_Purge is an act of administration, never of the platform
 */
CREATE SCHEMA [Logs] AUTHORIZATION [dbo];
