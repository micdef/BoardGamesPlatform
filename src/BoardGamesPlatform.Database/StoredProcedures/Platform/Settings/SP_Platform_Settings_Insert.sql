/**
 *	@file @prj/src/BoardGamesPlatform.Database/StoredProcedures/Platform/Settings/SP_Platform_Settings_Insert.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Inserts one or more settings into Platform.Settings, and returns one JSON result per input row
 *
 *	Parameters :
 *	------------
 *	*************************************************************************************************************************************************************
 *	* Name		| Type								| Size	| Null	| ReadOnly	| Output	| Brief																*
 *	*************************************************************************************************************************************************************
 *	* Rows		| Types.TVP_Platform_Settings_Insert|		| [ ]	| [x]		| [ ]		| Settings to insert (1000 rows at most)							*
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
 *	* 51000	| Rows holds more than 1000 rows		| At most 1000 rows are accepted per call: nothing is written												*
 *	* 51000	| IdActor is NULL, Guid.Empty or unknown| The actor is unknown: nothing is written																	*
 *	*************************************************************************************************************************************************************
 *	- Every other problem is a problem of one row: the row is rejected in the result with its reason, the other rows are written
 *
 *	Response Structure :
 *	--------------------
 *	JSON array holding one result object per input row, in the order of the codes. A row that breaks several rules comes out once, with the
 *	reason of its first failing rule, in this order:
 *	1. Code is not a valid code (Functions.IsValidSettingCode)
 *	2. Kind is not Boolean, Integer or Text
 *	3. Value does not fit its kind (Functions.IsValidSettingValue)
 *	4. Code already exists
 *
 *	Properties:
 *	- Id        : Unique identifier of the inserted row, null on failure
 *	- Code      : Code of the setting, as given
 *	- Kind      : Kind of the setting, as given
 *	- Value     : Value of the setting, as given
 *	- Comment   : Comment of the setting, as given (null when none)
 *	- IsSuccess : true when the row is inserted, false otherwise
 *	- Error     : Reason of the rejection, null on success
 *
 *	Response Examples :
 *	-------------------
 *	- Success :
 *	{
 *		"Id" : "6F9619FF-8B86-D011-B42D-00C04FC964FF",
 *		"Code" : "Registration.IsOpen",
 *		"Kind" : "Boolean",
 *		"Value" : "false",
 *		"Comment" : "Whether the registration form accepts new accounts",
 *		"IsSuccess" : true,
 *		"Error" : null
 *	}
 *
 *	- Code already existing :
 *	{
 *		"Id" : null,
 *		"Code" : "Registration.IsOpen",
 *		"Kind" : "Boolean",
 *		"Value" : "true",
 *		"Comment" : null,
 *		"IsSuccess" : false,
 *		"Error" : "Code already exists"
 *	}
 *
 *	- Value that does not fit its kind :
 *	{
 *		"Id" : null,
 *		"Code" : "Lobby.MaxPlayers",
 *		"Kind" : "Integer",
 *		"Value" : "ten",
 *		"Comment" : null,
 *		"IsSuccess" : false,
 *		"Error" : "Value does not fit its kind"
 *	}
 *
 *	Behavior :
 *	-----------
 *	The procedure processes the rows independently: the rows that break a rule are rejected with their reason, the valid rows are inserted. It
 *	does not stop at the first rejected row. In the same transaction, it writes one audit row per inserted row (Logs.Audit: actor, SQL login,
 *	UTC time, the values of the row in NewValue), so that no setting exists without its audit, and no audit row without its setting. The
 *	messages name the rule, never a value. The table checks the rows a third time (CHECK constraints): a check that looks redundant is intended.
 */
CREATE PROCEDURE [StorProc].[SP_Platform_Settings_Insert]
	@Rows [Types].[TVP_Platform_Settings_Insert] READONLY,
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
				[Kind] NVARCHAR(16) NOT NULL,
				[Value] NVARCHAR(4000) NOT NULL,
				[Comment] NVARCHAR(MAX) NULL,
				[IsSuccess] BIT NOT NULL,
				[Error] NVARCHAR(200) NULL
			);
			DECLARE @written AS TABLE
			(
				[Id] UNIQUEIDENTIFIER NOT NULL,
				[Code] NVARCHAR(64) NOT NULL,
				[Kind] NVARCHAR(16) NOT NULL,
				[Value] NVARCHAR(4000) NOT NULL,
				[Comment] NVARCHAR(MAX) NULL
			);
			DECLARE @audit AS [Types].[TVP_Logs_Audit_Insert];

			/* Checks of the call */
			IF NOT EXISTS (SELECT 1 FROM @Rows) THROW 51000, 'At least one row is required', 1;
			IF (SELECT COUNT(*) FROM @Rows) > 1000 THROW 51000, 'At most 1000 rows are accepted per call', 1;
			IF [Functions].[IsValidActorId](@IdActor) = 0 THROW 51000, 'The actor is unknown', 1;

			/* Eject invalid codes */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [Comment], [IsSuccess], [Error])
			SELECT NULL, r.[Code], r.[Kind], r.[Value], r.[Comment], 0, N'Code is not valid'
			FROM @Rows r
			WHERE [Functions].[IsValidSettingCode](r.[Code]) = 0;

			/* Eject unknown kinds */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [Comment], [IsSuccess], [Error])
			SELECT NULL, r.[Code], r.[Kind], r.[Value], r.[Comment], 0, N'Kind is not Boolean, Integer or Text'
			FROM @Rows r
			WHERE (r.[Kind] + N'|') COLLATE Latin1_General_BIN2 NOT IN (N'Boolean|', N'Integer|', N'Text|')
				AND NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Eject values that do not fit their kind */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [Comment], [IsSuccess], [Error])
			SELECT NULL, r.[Code], r.[Kind], r.[Value], r.[Comment], 0, N'Value does not fit its kind'
			FROM @Rows r
			WHERE [Functions].[IsValidSettingValue](r.[Kind], r.[Value]) = 0
				AND NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Eject codes that already exist */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [Comment], [IsSuccess], [Error])
			SELECT NULL, r.[Code], r.[Kind], r.[Value], r.[Comment], 0, N'Code already exists'
			FROM @Rows r
			WHERE EXISTS (SELECT 1 FROM [Platform].[Settings] s WHERE s.[Code] = r.[Code])
				AND NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Insert the remaining rows */
			INSERT INTO [Platform].[Settings] ([Code], [Kind], [Value], [Comment])
			OUTPUT inserted.[Id], inserted.[Code], inserted.[Kind], inserted.[Value], inserted.[Comment]
				INTO @written ([Id], [Code], [Kind], [Value], [Comment])
			SELECT r.[Code], r.[Kind], r.[Value], r.[Comment]
			FROM @Rows r
			WHERE NOT EXISTS (SELECT 1 FROM @results o WHERE o.[Code] = r.[Code]);

			/* Record the successes */
			INSERT INTO @results ([Id], [Code], [Kind], [Value], [Comment], [IsSuccess], [Error])
			SELECT w.[Id], w.[Code], w.[Kind], w.[Value], w.[Comment], 1, NULL
			FROM @written w;

			/* Audit: the values of each inserted row */
			IF EXISTS (SELECT 1 FROM @written)
			BEGIN
				INSERT INTO @audit ([IdActor], [SchemaName], [TableName], [OldValue], [NewValue])
				SELECT @IdActor, N'Platform', N'Settings', NULL,
					(SELECT w.[Id], w.[Code], w.[Kind], w.[Value], w.[Comment] FOR JSON PATH, WITHOUT_ARRAY_WRAPPER, INCLUDE_NULL_VALUES)
				FROM @written w;

				EXEC [Logs].[SP_Logs_Audit_Insert] @Rows = @audit;
			END;

			/* Output */
			SET @Result = (
				SELECT o.[Id], o.[Code], o.[Kind], o.[Value], o.[Comment], o.[IsSuccess], o.[Error]
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
