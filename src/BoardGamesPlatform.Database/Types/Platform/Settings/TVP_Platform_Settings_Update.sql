/**
 *	@file @prj/src/BoardGamesPlatform.Database/Types/Platform/Settings/TVP_Platform_Settings_Update.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Table-valued parameter of the new values of settings, given to StorProc.SP_Platform_Settings_Update
 *
 *	Columns :
 *	---------
 *	*************************************************************************************************************
 *	* Name		| Type		| Size	| Null	| Unique	| Key	| Brief												*
 *	*************************************************************************************************************
 *	* Code		| NVARCHAR	| 64	| [ ]	| [x]		| [ ]	| Code of the setting to change; the key of its result	*
 *	* Value		| NVARCHAR	| 4000	| [ ]	| [ ]		| [ ]	| The new value, as text							*
 *	*************************************************************************************************************
 *
 *	Constraints :
 *	-------------
 *	*********************************************************************************
 *	* Type		| Work Column(s)	| Check												*
 *	*********************************************************************************
 *	* Unique	| Code				| One row per code: each result names its row		*
 *	* Check		| Code				| Code is not empty or whitespace					*
 *	*********************************************************************************
 *	- Only the value changes: the kind of a setting is fixed at its insert, and its comment is not part of an update
 */
CREATE TYPE [Types].[TVP_Platform_Settings_Update] AS TABLE
(
	[Code] NVARCHAR(64) NOT NULL UNIQUE,
	[Value] NVARCHAR(4000) NOT NULL,

	CHECK (NULLIF(TRIM([Code]), N'') IS NOT NULL)
);
