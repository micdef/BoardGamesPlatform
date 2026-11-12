/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/Types.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the table-valued parameters: the shape of the rows the procedures receive
 *
 *	Information :
 *	-------------
 *	Family			=> Object type
 *	Exposure		=> Public (application role)
 *	Owner			=> dbo
 *	Object Types	=> User-defined table types
 *	Object Naming	=> TVP_<Schema>_<Table>_<Action>
 *	Filegroup		=> #N/A
 *
 *	Purpose :
 *	---------
 *	- First level of control: each type checks its own rows (NOT NULL, UNIQUE, CHECK), before any procedure runs
 *	- The procedures check the rows again, and the tables a third time: the control in depth is intended
 *
 *	Access :
 *	--------
 *	- Application role: EXECUTE and REFERENCES, to declare a variable of a type and pass it to a procedure
 */
CREATE SCHEMA [Types] AUTHORIZATION [dbo];
