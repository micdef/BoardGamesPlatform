/**
 *	@file @prj/src/BoardGamesPlatform.Database/Tables/Logs/Audit.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Table of the immutable audit: one row per row written by a procedure of the platform, with its value before and after
 *
 *	Columns :
 *	---------
 *	*************************************************************************************************************************************************************
 *	* Name			| Type				| Size	| Nullable	| Default			| Unique	| Key	| Brief														*
 *	*************************************************************************************************************************************************************
 *	* Id			| UNIQUEIDENTIFIER	|		| [ ]		| NEWID()			| [x]		| PK	| Unique identifier of the row								*
 *	* IdActor		| UNIQUEIDENTIFIER	|		| [ ]		|					| [ ]		|		| Actor who asked for the change (see Functions.IsValidActorId)	*
 *	* LoginName		| NVARCHAR			| 128	| [ ]		| ORIGINAL_LOGIN()	| [ ]		|		| SQL login of the session that wrote the row				*
 *	* ActionDT		| DATETIME2			| 7		| [ ]		| SYSUTCDATETIME()	| [ ]		| IXC	| Date and time of the change, in UTC						*
 *	* SchemaName	| NVARCHAR			| 128	| [ ]		|					| [ ]		|		| Schema of the changed table								*
 *	* TableName		| NVARCHAR			| 128	| [ ]		|					| [ ]		|		| Changed table												*
 *	* OldValue		| NVARCHAR			| MAX	| [x]		|					| [ ]		|		| JSON of the row before the change (NULL for an insert)	*
 *	* NewValue		| NVARCHAR			| MAX	| [x]		|					| [ ]		|		| JSON of the row after the change (NULL for a delete)		*
 *	*************************************************************************************************************************************************************
 *
 *	Constraints :
 *	-------------
 *	*************************************************************************************************************************************************************
 *	* Name						| Type			| Work Column(s)		| References	| Check																	*
 *	*************************************************************************************************************************************************************
 *	* DF_Logs_Audit_Id			| Default		| Id					| #N/A			| #N/A																	*
 *	* DF_Logs_Audit_LoginName	| Default		| LoginName				| #N/A			| #N/A																	*
 *	* DF_Logs_Audit_ActionDT	| Default		| ActionDT				| #N/A			| #N/A																	*
 *	* PK_Logs_Audit_Id			| Primary Key	| Id					| #N/A			| Non-clustered: random GUIDs would split the pages of a clustered key	*
 *	* CK_Logs_Audit_Id			| Check			| Id					| #N/A			| Id is not Guid.Empty													*
 *	* CK_Logs_Audit_IdActor		| Check			| IdActor				| #N/A			| IdActor is not Guid.Empty (known actor: checked by the insert)		*
 *	* CK_Logs_Audit_LoginName	| Check			| LoginName				| #N/A			| LoginName is not empty or whitespace									*
 *	* CK_Logs_Audit_SchemaName	| Check			| SchemaName			| #N/A			| SchemaName exists (Functions.IsValidSchemaName)						*
 *	* CK_Logs_Audit_TableName	| Check			| SchemaName, TableName	| #N/A			| TableName exists in SchemaName (Functions.IsValidTableName)			*
 *	* CK_Logs_Audit_Values		| Check			| OldValue, NewValue	| #N/A			| OldValue or NewValue is set										*
 *	* CK_Logs_Audit_OldValue	| Check			| OldValue				| #N/A			| OldValue is NULL or valid JSON										*
 *	* CK_Logs_Audit_NewValue	| Check			| NewValue				| #N/A			| NewValue is NULL or valid JSON										*
 *	*************************************************************************************************************************************************************
 *
 *	Triggers :
 *	----------
 *	*************************************************************************************************************************************************
 *	* Name						| Type			| Command	| Action									| Purpose									*
 *	*************************************************************************************************************************************************
 *	* TR_Logs_Audit_IOfUpdate	| Instead of	| Update	| Throw Error => 50001 => Update prohibited	| Audit rows are immutable					*
 *	* TR_Logs_Audit_IOfDelete	| Instead of	| Delete	| Throw Error => 50001 => Delete prohibited	| Audit rows are immutable, except the purge	*
 *	*************************************************************************************************************************************************
 *
 *	Indexes :
 *	---------
 *	*************************************************************************************************************************
 *	* Name						| Type			| Column(s)	| Filegroup	| Purpose											*
 *	*************************************************************************************************************************
 *	* IXC_Logs_Audit_ActionDT	| Clustered		| ActionDT	| Core		| Rows stored by date: appended at the end, purged from the start	*
 *	*************************************************************************************************************************
 *
 *	History :
 *	---------
 *	System Versioning	=> OFF
 *	Temporal Table		=> NO (an audit row never changes, so it has no history)
 *	Start				=> #N/A
 *	Stop				=> #N/A
 *	History Table		=> #N/A
 */
CREATE TABLE [Logs].[Audit]
(
	[Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Logs_Audit_Id] DEFAULT (NEWID()),
	[IdActor] UNIQUEIDENTIFIER NOT NULL,
	[LoginName] NVARCHAR(128) NOT NULL CONSTRAINT [DF_Logs_Audit_LoginName] DEFAULT (ORIGINAL_LOGIN()),
	[ActionDT] DATETIME2(7) NOT NULL CONSTRAINT [DF_Logs_Audit_ActionDT] DEFAULT (SYSUTCDATETIME()),
	[SchemaName] NVARCHAR(128) NOT NULL,
	[TableName] NVARCHAR(128) NOT NULL,
	[OldValue] NVARCHAR(MAX) NULL,
	[NewValue] NVARCHAR(MAX) NULL,

	CONSTRAINT [PK_Logs_Audit_Id] PRIMARY KEY NONCLUSTERED ([Id]) ON [Core],
	CONSTRAINT [CK_Logs_Audit_Id] CHECK ([Id] <> '00000000-0000-0000-0000-000000000000'),
	CONSTRAINT [CK_Logs_Audit_IdActor] CHECK ([IdActor] <> '00000000-0000-0000-0000-000000000000'),
	CONSTRAINT [CK_Logs_Audit_LoginName] CHECK (NULLIF(TRIM([LoginName]), N'') IS NOT NULL),
	CONSTRAINT [CK_Logs_Audit_SchemaName] CHECK ([Functions].[IsValidSchemaName]([SchemaName]) = 1),
	CONSTRAINT [CK_Logs_Audit_TableName] CHECK ([Functions].[IsValidTableName]([SchemaName], [TableName]) = 1),
	CONSTRAINT [CK_Logs_Audit_Values] CHECK ([OldValue] IS NOT NULL OR [NewValue] IS NOT NULL),
	CONSTRAINT [CK_Logs_Audit_OldValue] CHECK ([OldValue] IS NULL OR ISJSON([OldValue]) = 1),
	CONSTRAINT [CK_Logs_Audit_NewValue] CHECK ([NewValue] IS NULL OR ISJSON([NewValue]) = 1)
) ON [Core];
