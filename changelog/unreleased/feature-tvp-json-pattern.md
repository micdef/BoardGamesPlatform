### Added

- Database, write pattern: the temporal table `Platform.Settings` (history in `History.Platform_Settings`), its table-valued parameters, and the procedures `StorProc.SP_Platform_Settings_Insert` and `StorProc.SP_Platform_Settings_Update`: each input row is checked again and comes back once in a JSON array, with its identifier or the reason of its first failing rule; the valid rows are written with their audit in the same transaction; 1 000 rows at most per call.
- Database, read pattern: the view `Views.V_Platform_Settings`.
- Database: the registration switch `Registration.IsOpen`, closed, written after every deployment when it does not exist (it keeps the value an administrator gave it).
- Database tests: the procedures of the settings (rules of rejection, one result per row, a failure of the audit that rolls the write back, the temporal history, the contract seen by a member of the application role).

### Security

- Very Back: a failed call of the database leaves as `SqlCallException`, which keeps the number, the state, the procedure and the line of the SQL error, never its message nor the SQL exception, whose text can quote values (a duplicate key, a name of database).
