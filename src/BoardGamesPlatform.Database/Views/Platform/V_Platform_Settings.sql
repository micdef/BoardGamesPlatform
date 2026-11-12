/**
 *	@file @prj/src/BoardGamesPlatform.Database/Views/Platform/V_Platform_Settings.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Current settings of the platform, as the application reads them
 *
 *	Columns :
 *	---------
 *	*********************************************************************************************************************
 *	* Name		| Type		| Size	| Nullable	| Brief																	*
 *	*********************************************************************************************************************
 *	* Code		| NVARCHAR	| 64	| [ ]		| Code of the setting (unique)											*
 *	* Kind		| NVARCHAR	| 16	| [ ]		| Boolean, Integer or Text												*
 *	* Value		| NVARCHAR	| 4000	| [ ]		| The value, as text, consistent with its kind							*
 *	* ValidFrom	| DATETIME2	| 7		| [ ]		| Since when the value is valid (UTC)									*
 *	*********************************************************************************************************************
 *
 *	Behavior :
 *	-----------
 *	Returns the current rows only: the history of the settings stays in History.Platform_Settings, read by an administrator with FOR SYSTEM_TIME
 *	on the table, never by the application. The identifier and the comment are not exposed: the application reads a setting by its code. The
 *	view belongs to dbo like its table, so the SELECT right of the application role on the schema Views is enough (ownership chaining).
 */
CREATE VIEW [Views].[V_Platform_Settings]
AS
	SELECT s.[Code], s.[Kind], s.[Value], s.[ValidFrom]
	FROM [Platform].[Settings] s;
