/**
 *	@file @prj/src/BoardGamesPlatform.Database/Types/Platform/Settings/TVP_Platform_Settings_Insert.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Table-valued parameter of the settings to insert in Platform.Settings, given to StorProc.SP_Platform_Settings_Insert
 *
 *	Columns :
 *	---------
 *	*************************************************************************************************************
 *	* Name		| Type		| Size	| Null	| Unique	| Key	| Brief												*
 *	*************************************************************************************************************
 *	* Code		| NVARCHAR	| 64	| [ ]	| [x]		| [ ]	| Code of the setting; the key of its result		*
 *	* Kind		| NVARCHAR	| 16	| [ ]	| [ ]		| [ ]	| Boolean, Integer or Text							*
 *	* Value		| NVARCHAR	| 4000	| [ ]	| [ ]		| [ ]	| The value, as text								*
 *	* Comment	| NVARCHAR	| MAX	| [x]	| [ ]		| [ ]	| What the setting does								*
 *	*************************************************************************************************************
 *
 *	Constraints :
 *	-------------
 *	*********************************************************************************
 *	* Type		| Work Column(s)	| Check												*
 *	*********************************************************************************
 *	* Unique	| Code				| One row per code, compared without case: each result names its row	*
 *	* Check		| Code				| Code is not empty or whitespace					*
 *	* Check		| Kind				| Kind is not empty or whitespace					*
 *	*********************************************************************************
 *	- The rules of the codes, kinds and values are checked again by the procedure, which rejects a row with its reason instead of failing the
 *	  whole call; the type only refuses what no row can be (no code, no kind)
 */
CREATE TYPE [Types].[TVP_Platform_Settings_Insert] AS TABLE
(
	[Code] NVARCHAR(64) NOT NULL UNIQUE,
	[Kind] NVARCHAR(16) NOT NULL,
	[Value] NVARCHAR(4000) NOT NULL,
	[Comment] NVARCHAR(MAX) NULL,

	CHECK (NULLIF(TRIM([Code]), N'') IS NOT NULL),
	CHECK (NULLIF(TRIM([Kind]), N'') IS NOT NULL)
);
