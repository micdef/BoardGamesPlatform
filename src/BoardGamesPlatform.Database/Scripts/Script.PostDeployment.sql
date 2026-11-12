/**
 *	@file @prj/src/BoardGamesPlatform.Database/Scripts/Script.PostDeployment.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Post-deployment script: writes the settings the platform needs, when they do not exist yet
 *
 *	Settings :
 *	----------
 *	*************************************************************************************************************************************
 *	* Code					| Kind		| Value	| Why																						*
 *	*************************************************************************************************************************************
 *	* Registration.IsOpen	| Boolean	| false	| The registration switch of version 0.8.0: closed until the author opens it				*
 *	*************************************************************************************************************************************
 *
 *	Behavior :
 *	-----------
 *	SSDT runs this script after every publish. It is idempotent: a setting that exists keeps the value an administrator gave it, so a
 *	redeployment never reopens the registration. The settings are written through StorProc.SP_Platform_Settings_Insert, with the platform as
 *	the actor, so that they get their audit rows like any other write. A rejected row stops the deployment: a setting the platform needs must
 *	exist.
 */
SET NOCOUNT ON;

DECLARE @rows AS [Types].[TVP_Platform_Settings_Insert];
DECLARE @result NVARCHAR(MAX);
DECLARE @actor UNIQUEIDENTIFIER = [Functions].[SystemActorId]();

/* Settings to write when they do not exist */
INSERT INTO @rows ([Code], [Kind], [Value], [Comment])
SELECT v.[Code], v.[Kind], v.[Value], v.[Comment]
FROM (VALUES
	(N'Registration.IsOpen', N'Boolean', N'false', N'Whether the registration form accepts new accounts (version 0.8.0)')
) v ([Code], [Kind], [Value], [Comment])
WHERE NOT EXISTS (SELECT 1 FROM [Platform].[Settings] s WHERE s.[Code] = v.[Code]);

/* Write them, and stop the deployment on any rejected row */
IF EXISTS (SELECT 1 FROM @rows)
BEGIN
	EXEC [StorProc].[SP_Platform_Settings_Insert] @Rows = @rows, @IdActor = @actor, @Result = @result OUTPUT;
	IF EXISTS (SELECT 1 FROM OPENJSON(@result) WITH ([IsSuccess] BIT N'$.IsSuccess') WHERE [IsSuccess] = 0)
		THROW 51000, 'A setting needed by the platform was rejected by StorProc.SP_Platform_Settings_Insert', 1;
END;
