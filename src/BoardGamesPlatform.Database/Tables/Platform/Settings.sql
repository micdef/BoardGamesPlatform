/**
 *	@file @prj/src/BoardGamesPlatform.Database/Tables/Platform/Settings.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Table of the settings of the platform that an administrator may change while it runs (first one: the registration switch)
 *
 *	Columns :
 *	---------
 *	*************************************************************************************************************************************************************
 *	* Name			| Type				| Size	| Nullable	| Default			| Unique	| Key	| Brief														*
 *	*************************************************************************************************************************************************************
 *	* Id			| UNIQUEIDENTIFIER	|		| [ ]		| NEWID()			| [x]		| PK	| Unique identifier of the row								*
 *	* Code			| NVARCHAR			| 64	| [ ]		|					| [x]		| UK	| Code of the setting: Area.Name (letters, digits and dots)	*
 *	* Kind			| NVARCHAR			| 16	| [ ]		|					| [ ]		|		| What Value holds: Boolean, Integer or Text				*
 *	* Value			| NVARCHAR			| 4000	| [ ]		|					| [ ]		|		| The value, as text, consistent with Kind					*
 *	* Comment		| NVARCHAR			| MAX	| [x]		|					| [ ]		|		| What the setting does										*
 *	* ValidFrom		| DATETIME2			| 7		| [ ]		| TEMPORAL SYSTEM	| [ ]		|		| TEMPORAL SYSTEM : Start (UTC)								*
 *	* ValidTo		| DATETIME2			| 7		| [ ]		| TEMPORAL SYSTEM	| [ ]		|		| TEMPORAL SYSTEM : Stop (UTC)								*
 *	*************************************************************************************************************************************************************
 *
 *	Constraints :
 *	-------------
 *	*************************************************************************************************************************************************************
 *	* Name						| Type			| Work Column(s)	| References	| Check																		*
 *	*************************************************************************************************************************************************************
 *	* DF_Platform_Settings_Id	| Default		| Id				| #N/A			| #N/A																		*
 *	* PK_Platform_Settings_Id	| Primary Key	| Id				| #N/A			| #N/A																		*
 *	* UK_Platform_Settings_Code	| Unique		| Code				| #N/A			| #N/A																		*
 *	* CK_Platform_Settings_Id	| Check			| Id				| #N/A			| Id is not Guid.Empty														*
 *	* CK_Platform_Settings_Code	| Check			| Code				| #N/A			| Functions.IsValidSettingCode: Area.Name, letters, digits and dots			*
 *	* CK_Platform_Settings_Kind	| Check			| Kind				| #N/A			| Kind is exactly Boolean, Integer or Text (case and trailing spaces count)	*
 *	* CK_Platform_Settings_Value| Check			| Kind, Value		| #N/A			| Functions.IsValidSettingValue: the value fits its kind					*
 *	*************************************************************************************************************************************************************
 *
 *	Triggers :
 *	----------
 *	#N/A
 *
 *	Indexes :
 *	---------
 *	#N/A (the unique constraint on Code indexes the reads by code)
 *
 *	History :
 *	---------
 *	System Versioning	=> ON
 *	Temporal Table		=> YES
 *	Start				=> ValidFrom
 *	Stop				=> ValidTo
 *	History Table		=> History.Platform_Settings (made by SQL Server; every former value of a setting with the period it was valid)
 */
CREATE TABLE [Platform].[Settings]
(
	[Id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Platform_Settings_Id] DEFAULT (NEWID()),
	[Code] NVARCHAR(64) NOT NULL,
	[Kind] NVARCHAR(16) NOT NULL,
	[Value] NVARCHAR(4000) NOT NULL,
	[Comment] NVARCHAR(MAX) NULL,

	[ValidFrom] DATETIME2(7) GENERATED ALWAYS AS ROW START NOT NULL,
	[ValidTo] DATETIME2(7) GENERATED ALWAYS AS ROW END NOT NULL,
	PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo]),

	CONSTRAINT [PK_Platform_Settings_Id] PRIMARY KEY ([Id]),
	CONSTRAINT [UK_Platform_Settings_Code] UNIQUE ([Code]),
	CONSTRAINT [CK_Platform_Settings_Id] CHECK ([Id] <> '00000000-0000-0000-0000-000000000000'),
	CONSTRAINT [CK_Platform_Settings_Code] CHECK ([Functions].[IsValidSettingCode]([Code]) = 1),
	CONSTRAINT [CK_Platform_Settings_Kind] CHECK (([Kind] + N'|') COLLATE Latin1_General_BIN2 IN (N'Boolean|', N'Integer|', N'Text|')),
	CONSTRAINT [CK_Platform_Settings_Value] CHECK ([Functions].[IsValidSettingValue]([Kind], [Value]) = 1)
) ON [Core]
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = [History].[Platform_Settings]));
