/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/IsValidTableName.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Tells whether a table exists in a given schema of the database
 *
 *	Parameters :
 *	------------
 *	*****************************************************************************************************************
 *	* Name			| Type		| Size	| Null	| Readonly	| Output	| Brief									*
 *	*****************************************************************************************************************
 *	* SchemaName	| NVARCHAR	| 128	| [x]	| [ ]		| [ ]		| Name of the schema of the table		*
 *	* TableName		| NVARCHAR	| 128	| [x]	| [ ]		| [ ]		| Name of the table to check			*
 *	*****************************************************************************************************************
 *
 *	Return :
 *	--------
 *	BIT : 1 when a base table of that name exists in that schema, 0 otherwise (a view, a NULL name included)
 *
 *	Behavior :
 *	----------
 *	Reads INFORMATION_SCHEMA.TABLES, whose rows are those the caller may see: the caller needs the right to read the metadata (VIEW DEFINITION),
 *	which the application role holds. Used by the CHECK constraint of Logs.Audit and by Logs.SP_Logs_Audit_Insert, so that an audit row
 *	always names a real table.
 */
CREATE FUNCTION [Functions].[IsValidTableName]
(
	@SchemaName NVARCHAR(128),
	@TableName NVARCHAR(128)
)
	RETURNS BIT
AS
BEGIN
	RETURN IIF(EXISTS (SELECT 1 FROM [INFORMATION_SCHEMA].[TABLES] WHERE [TABLE_SCHEMA] = @SchemaName AND [TABLE_NAME] = @TableName AND [TABLE_TYPE] = N'BASE TABLE'), 1, 0);
END
