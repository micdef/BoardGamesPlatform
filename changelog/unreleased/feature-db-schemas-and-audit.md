### Added

- Database: the modules `Platform` and `Logs`, the schemas per type of object (`StorProc`, `Views`, `Types`, `Functions`, `History`) and the filegroups `Core` and `Access`, whose files go under the default paths of the server.
- Database, audit: the table `Logs.Audit` (actor, SQL login, UTC time, the row before and after the change in JSON), its internal insert, and its purge by dates `Logs.SP_Logs_Audit_Purge`, which records itself and never deletes the rows of earlier purges; the role `AuditPurgeRole` purges without being a member of `db_owner`.
- Database tests: the procedures of the audit, its immutability, the purge and its gate (a gate left open is cleared when a pooled connection is reused), the permission matrix of the application role and of the purge role.

### Changed

- Database: the application role executes the procedures of `StorProc` and `ServerCore`, reads the views of `Views`, passes the table-valued parameters of `Types` and reads the metadata; every right on the internal schemas is denied.
- Database tests: the convention tests require one test class per group of procedures, the tables on the filegroup `Core` and the history of the temporal tables in `History`; the class of the Ping is renamed `ServerProceduresTests`.

### Security

- Database, audit: every write of the audit table is denied to `public`, so that no user but `dbo` can insert, update or delete a row, even with the key of the gate of the purge set; `UPDATE` and `DELETE` are also refused by `INSTEAD OF` triggers (error 50001).
