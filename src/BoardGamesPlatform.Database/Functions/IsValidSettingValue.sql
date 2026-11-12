/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/IsValidSettingValue.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Tells whether a value is consistent with the kind of its setting
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************
 *	* Name	| Type		| Size	| Null	| Readonly	| Output	| Brief								*
 *	*************************************************************************************************
 *	* Kind	| NVARCHAR	| 16	| [x]	| [ ]		| [ ]		| Boolean, Integer or Text			*
 *	* Value	| NVARCHAR	| 4000	| [x]	| [ ]		| [ ]		| Value to check, as text			*
 *	*************************************************************************************************
 *
 *	Return :
 *	--------
 *	BIT : 1 when the value fits its kind, 0 otherwise (an unknown kind and NULL included):
 *	      - Boolean : exactly true or false (lower case, no space)
 *	      - Integer : a 64-bit integer in its canonical form (no space, no sign +, no leading zero), so that one value has one text only
 *	      - Text    : not empty or whitespace
 *	      The kind itself is compared exactly: Boolean, Integer or Text, with this case and no space
 *
 *	Behavior :
 *	----------
 *	Used by the CHECK constraint of Platform.Settings and by the procedures of the settings, so that the rule lives in one place. SQL Server
 *	ignores the trailing spaces of a text in = and IN, even with a binary collation, and the collation of the database ignores the case: every
 *	comparison is therefore made on the text followed by the character |, with a binary collation, so that "true " or "boolean" is refused (found
 *	by the review of the code).
 */
CREATE FUNCTION [Functions].[IsValidSettingValue]
(
	@Kind NVARCHAR(16),
	@Value NVARCHAR(4000)
)
	RETURNS BIT
	WITH SCHEMABINDING
AS
BEGIN
	RETURN CASE
		WHEN (@Kind + N'|') COLLATE Latin1_General_BIN2 = N'Boolean|'
			AND (@Value + N'|') COLLATE Latin1_General_BIN2 IN (N'true|', N'false|') THEN 1
		WHEN (@Kind + N'|') COLLATE Latin1_General_BIN2 = N'Integer|'
			AND (@Value + N'|') COLLATE Latin1_General_BIN2 = CAST(TRY_CAST(@Value AS BIGINT) AS NVARCHAR(20)) + N'|' THEN 1
		WHEN (@Kind + N'|') COLLATE Latin1_General_BIN2 = N'Text|'
			AND NULLIF(TRIM(@Value), N'') IS NOT NULL THEN 1
		ELSE 0
	END;
END
