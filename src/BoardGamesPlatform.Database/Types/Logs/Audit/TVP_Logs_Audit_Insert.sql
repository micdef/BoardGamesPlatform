/**
 *	@file @prj/src/BoardGamesPlatform.Database/Types/Logs/Audit/TVP_Logs_Audit_Insert.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Table-valued parameter of the rows to insert in the audit (Logs.Audit), given by the procedures of StorProc
 *
 *	Columns :
 *	---------
 *	*************************************************************************************************************************************
 *	* Name			| Type				| Size	| Null	| Unique	| Key	| Brief														*
 *	*************************************************************************************************************************************
 *	* IdActor		| UNIQUEIDENTIFIER	|		| [ ]	| [ ]		| [ ]	| Actor who asked for the change							*
 *	* SchemaName	| NVARCHAR			| 128	| [ ]	| [ ]		| [ ]	| Schema of the changed table								*
 *	* TableName		| NVARCHAR			| 128	| [ ]	| [ ]		| [ ]	| Changed table												*
 *	* OldValue		| NVARCHAR			| MAX	| [x]	| [ ]		| [ ]	| JSON of the row before the change (NULL for an insert)	*
 *	* NewValue		| NVARCHAR			| MAX	| [x]	| [ ]		| [ ]	| JSON of the row after the change (NULL for a delete)		*
 *	*************************************************************************************************************************************
 *
 *	Constraints :
 *	-------------
 *	*************************************************************************************************
 *	* Type	| Work Column(s)		| Check															*
 *	*************************************************************************************************
 *	* Check	| IdActor				| IdActor is not Guid.Empty										*
 *	* Check	| SchemaName			| SchemaName is not empty or whitespace							*
 *	* Check	| TableName				| TableName is not empty or whitespace							*
 *	* Check	| OldValue, NewValue	| OldValue or NewValue is set, not empty or whitespace			*
 *	*************************************************************************************************
 */
CREATE TYPE [Types].[TVP_Logs_Audit_Insert] AS TABLE
(
	[IdActor] UNIQUEIDENTIFIER NOT NULL,
	[SchemaName] NVARCHAR(128) NOT NULL,
	[TableName] NVARCHAR(128) NOT NULL,
	[OldValue] NVARCHAR(MAX) NULL,
	[NewValue] NVARCHAR(MAX) NULL,

	CHECK ([IdActor] <> '00000000-0000-0000-0000-000000000000'),
	CHECK (NULLIF(TRIM([SchemaName]), N'') IS NOT NULL),
	CHECK (NULLIF(TRIM([TableName]), N'') IS NOT NULL),
	CHECK (NULLIF(TRIM([OldValue]), N'') IS NOT NULL OR NULLIF(TRIM([NewValue]), N'') IS NOT NULL)
);
