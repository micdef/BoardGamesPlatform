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
 *	      - Boolean : exactly true or false (lower case)
 *	      - Integer : a 64-bit integer in its canonical form (no space, no sign +, no leading zero), so that one value has one text only
 *	      - Text    : not empty or whitespace
 *
 *	Behavior :
 *	-----------
 *	Used by the CHECK constraint of Platform.Settings and by the procedures of the settings, so that the rule lives in one place.
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
		WHEN @Kind = N'Boolean' AND @Value COLLATE Latin1_General_BIN2 IN (N'true', N'false') THEN 1
		WHEN @Kind = N'Integer' AND @Value = CAST(TRY_CAST(@Value AS BIGINT) AS NVARCHAR(20)) THEN 1
		WHEN @Kind = N'Text' AND NULLIF(TRIM(@Value), N'') IS NOT NULL THEN 1
		ELSE 0
	END;
END
