/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/IsValidSchemaName.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Tells whether a schema exists in the database
 *
 *	Parameters :
 *	------------
 *	*****************************************************************************************************
 *	* Name			| Type		| Size	| Null	| Readonly	| Output	| Brief						*
 *	*****************************************************************************************************
 *	* SchemaName	| NVARCHAR	| 128	| [x]	| [ ]		| [ ]		| Name of the schema to check	*
 *	*****************************************************************************************************
 *
 *	Return :
 *	--------
 *	BIT : 1 when the schema exists (exact name, as the collation of the database compares it), 0 otherwise (NULL included)
 *
 *	Behavior :
 *	-----------
 *	Reads INFORMATION_SCHEMA.SCHEMATA, whose rows are those the caller may see: the caller needs the right to read the metadata (VIEW DEFINITION),
 *	which the application role holds. Used by the CHECK constraint of Logs.Audit and by Logs.SP_Logs_Audit_Insert, so that an audit row
 *	always names a real schema.
 */
CREATE FUNCTION [Functions].[IsValidSchemaName]
(
	@SchemaName NVARCHAR(128)
)
	RETURNS BIT
AS
BEGIN
	RETURN IIF(EXISTS (SELECT 1 FROM [INFORMATION_SCHEMA].[SCHEMATA] WHERE [SCHEMA_NAME] = @SchemaName), 1, 0);
END
