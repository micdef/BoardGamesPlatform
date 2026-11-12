/**
 *	@file @prj/src/BoardGamesPlatform.Database/StoredProcedures/Platform/Settings/SP_Platform_Settings_Update.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Changes the value of one or more settings of Platform.Settings, and returns one JSON result per input row
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************************************************************************
 *	* Name		| Type								| Size	| Null	| ReadOnly	| Output	| Brief																*
 *	*************************************************************************************************************************************************************
 *	* Rows		| Types.TVP_Platform_Settings_Update|		| [ ]	| [x]		| [ ]		| Codes and new values (1 000 rows at most)							*
 *	* IdActor	| UNIQUEIDENTIFIER					|		| [ ]	| [ ]		| [ ]		| Actor who asks for the change (see Functions.IsValidActorId)		*
 *	* Result	| NVARCHAR							| MAX	| [ ]	| [ ]		| [x]		| Result of every input row, success or reason, formatted in JSON	*
 *	*************************************************************************************************************************************************************
 *
 *	Errors :
 *	--------
 *	*************************************************************************************************************************************************************
 *	* Code	| Trigger								| Brief																										*
 *	*************************************************************************************************************************************************************
 *	* 51000	| Rows holds no row						| At least one row is required: nothing is written															*
 *	* 51000	| Rows holds more than 1 000 rows		| At most 1 000 rows are accepted per call: nothing is written												*
 *	* 51000	| IdActor is NULL, Guid.Empty or unknown| The actor is unknown: nothing is written																	*
 *	*************************************************************************************************************************************************************
 *	- Every other problem is a problem of one row: the row is rejected in the result with its reason, the other rows are written
 *
 *	Response Structure :
 *	--------------------
 *	JSON array holding one result object per input row, in the order of the codes. A row that breaks several rules comes out once, with the
 *	reason of its first failing rule, in this order:
 *	1. Code does not exist
 *	2. Value does not fit the kind of the setting (Functions.IsValidSettingValue)
 *	3. Value is unchanged (an update would only add a history row and an audit row that say nothing)
 *
 *	Properties:
 *	- Id        : Unique identifier of the setting, null when its code does not exist
 *	- Code      : Code of the setting, as given
 *	- Kind      : Kind of the setting, as stored (null when its code does not exist)
 *	- Value     : New value, as given
 *	- IsSuccess : true when the value is changed, false otherwise
 *	- Error     : Reason of the rejection, null on success
 *
 *	Response Examples :
 *	-------------------
 *	- Success :
 *	{
 *		"Id" : "6F9619FF-8B86-D011-B42D-00C04FC964FF",
 *		"Code" : "Registration.IsOpen",
 *		"Kind" : "Boolean",
 *		"Value" : "true",
 *		"IsSuccess" : true,
 *		"Error" : null
 *	}
 *
 *	- Unknown code :
 *	{
 *		"Id" : null,
 *		"Code" : "Registration.IsClosed",
 *		"Kind" : null,
 *		"Value" : "true",
 *		"IsSuccess" : false,
 *		"Error" : "Code does not exist"
 *	}
 *
 *	Behavior :
 *	-----------
 *	The procedure processes the rows independently: the rows that break a rule are rejected with their reason, the valid rows are changed. It
 *	does not stop at the first rejected row. Only the value changes: the kind is fixed at the insert. The table is temporal, so SQL Server keeps
 *	the former value in History.Platform_Settings with the period it was valid. In the same transaction, the procedure writes one audit row per
 *	changed row (Logs.Audit: the row before in OldValue, the row after in NewValue). The messages name the rule, never a value.
 */
CREATE PROCEDURE [StorProc].[SP_Platform_Settings_Update]
	@Rows [Types].[TVP_Platform_Settings_Update] READONLY,
	@IdActor UNIQUEIDENTIFIER,
	@Result NVARCHAR(MAX) OUTPUT
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;
	BEGIN TRY
		BEGIN TRANSACTION

			/* Declare Types */
			DECLARE @results AS TABLE
			(
				[Id] UNIQUEIDENTIFIER NULL,
				[Code] NVARCHAR(64) NOT NULL PRIMARY KEY,
				[Kind] NVARCHAR(16) NULL,
				[Value] NVARCHAR(4000) NOT NULL,
				[IsSuccess] BIT NOT NULL,
				[Error] NVARCHAR(200) NULL
			);
			DECLARE @written AS TABLE
			(
				[Id] UNIQUEIDENTIFIER NOT NULL,
				[Code] NVARCHAR(64) NOT NULL,
				[Kind] NVARCHAR(16) NOT NULL,
				[OldValue] NVARCHAR(4000) NOT NULL,
				[NewValue] NVARCHAR(4000) NOT NULL,
				[Comment] NVARCHAR(MAX) NULL
			);
			DECLARE @audit AS [Types].[TVP_Logs_Audit_Insert];

			/* Checks of the call */
			IF NOT EXISTS (SELECT 1 FROM @Rows) THROW 51000, 'At least one row is required', 1;
			IF (SELECT COUNT(*) FROM @Rows) > 1000 THROW 51000, 'At most 1000 rows are accepted per call', 1;
			IF [Functions].[IsValidActorId](@IdActor) = 0 THROW 51000, 'The actor is unknown', 1;

			/* Eject unknown codes */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [IsSuccess], [Error])
			SELECT NULL, r.[Code], NULL, r.[Value], 0, N'Code does not exist'
			FROM @Rows r
			WHERE NOT EXISTS (SELECT 1 FROM [Platform].[Settings] s WHERE s.[Code] = r.[Code]);

			/* Eject values that do not fit the kind of their setting */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [IsSuccess], [Error])
			SELECT s.[Id], r.[Code], s.[Kind], r.[Value], 0, N'Value does not fit the kind of the setting'
			FROM @Rows r
			JOIN [Platform].[Settings] s ON (s.[Code] = r.[Code])
			WHERE [Functions].[IsValidSettingValue](s.[Kind], r.[Value]) = 0
				AND NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Eject unchanged values (compared as binary text: true and True are different values) */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [IsSuccess], [Error])
			SELECT s.[Id], r.[Code], s.[Kind], r.[Value], 0, N'Value is unchanged'
			FROM @Rows r
			JOIN [Platform].[Settings] s ON (s.[Code] = r.[Code])
			WHERE s.[Value] COLLATE Latin1_General_BIN2 = r.[Value] COLLATE Latin1_General_BIN2
				AND NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Update the remaining rows */
			UPDATE s
			SET s.[Value] = r.[Value]
			OUTPUT inserted.[Id], inserted.[Code], inserted.[Kind], deleted.[Value], inserted.[Value], inserted.[Comment]
				INTO @written ([Id], [Code], [Kind], [OldValue], [NewValue], [Comment])
			FROM [Platform].[Settings] s
			JOIN @Rows r ON (r.[Code] = s.[Code])
			WHERE NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Record the successes */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [IsSuccess], [Error])
			SELECT w.[Id], w.[Code], w.[Kind], w.[NewValue], 1, NULL
			FROM @written w;

			/* Audit: the row before and the row after */
			IF EXISTS (SELECT 1 FROM @written)
			BEGIN
				INSERT INTO @audit ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
				SELECT @IdActor, N'Platform', N'Settings',
					(SELECT w.[Id], w.[Code], w.[Kind], w.[OldValue] AS [Value], w.[Comment] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES),
					(SELECT w.[Id], w.[Code], w.[Kind], w.[NewValue] AS [Value], w.[Comment] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)
				FROM @written w;

				EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @audit;
			END;

			/* Output */
			SET @Result = (
				SELECT o.[Id], o.[Code], o.[Kind], o.[Value], o.[IsSuccess], o.[Error]
				FROM @results o
				ORDER BY o.[Code]
				FOR JSON PATH, INCLUDE_NULL_VALUES);

		COMMIT TRANSACTION
	END TRY
	BEGIN CATCH
		IF @@TRANCOUNT > 0
			ROLLBACK TRANSACTION;
		THROW
	END CATCH
END
