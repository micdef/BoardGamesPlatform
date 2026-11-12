/**
 *	@file @prj/src/BoardGamesPlatform.Database/Functions/IsValidActorId.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Tells whether an identifier names a known actor, who may ask for a change of the data
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************************
 *	* Name		| Type				| Size	| Null	| Readonly	| Output	| Brief								*
 *	*************************************************************************************************************
 *	* IdActor	| UNIQUEIDENTIFIER	|		| [x]	| [ ]		| [ ]		| Identifier of the actor to check	*
 *	*************************************************************************************************************
 *
 *	Return :
 *	--------
 *	BIT : 1 when the actor is known, 0 otherwise (NULL and Guid.Empty included)
 *
 *	Behavior :
 *	----------
 *	In version 0.7.0 the only known actor is the platform itself (Functions.SystemActorId). Version 0.8.0 also accepts the identifiers of the
 *	users. Every procedure of StorProc refuses a call whose actor is not known (error 51000): the audit never names an actor that does not exist.
 */
CREATE FUNCTION [Functions].[IsValidActorId]
(
	@IdActor UNIQUEIDENTIFIER
)
	RETURNS BIT
AS
BEGIN
	RETURN IIF(@IdActor IS NOT NULL AND @IdActor = [Functions].[SystemActorId](), 1, 0);
END
