/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/IsValidSettingCode.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Tells whether a text is a valid code of a setting of the platform (Area.Name)
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************
 *	* Name	| Type		| Size	| Null	| Readonly	| Output	| Brief								*
 *	*************************************************************************************************
 *	* Code	| NVARCHAR	| 64	| [x]	| [ ]		| [ ]		| Code to check						*
 *	*************************************************************************************************
 *
 *	Return :
 *	--------
 *	BIT : 1 when the code holds only ASCII letters, digits and dots, starts with a letter, holds a dot, and does not end with a dot; 0 otherwise
 *	      (NULL included)
 *
 *	Behavior :
 *	-----------
 *	The comparison is binary (Latin1_General_BIN2), so that an accented letter or a lookalike character never passes as a letter of the range
 *	A-Z. Used by the CHECK constraint of Platform.Settings and by the procedures of the settings, so that the rule lives in one place.
 */
CREATE FUNCTION [Functions].[IsValidSettingCode]
(
	@Code NVARCHAR(64)
)
	RETURNS BIT
	WITH SCHEMABINDING
AS
BEGIN
	RETURN IIF(@Code COLLATE Latin1_General_BIN2 NOT LIKE N'%[^A-Za-z0-9.]%'
		AND @Code COLLATE Latin1_General_BIN2 LIKE N'[A-Za-z]%.%'
		AND @Code NOT LIKE N'%.', 1, 0);
END
