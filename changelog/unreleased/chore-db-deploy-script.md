### Changed

- Deployment script (`tools/database/Deploy-TestDatabase.ps1`): a `DEV_` or `TEST_` database is deployed from scratch with its application user, a login named after the database with a random password (`CHECK_POLICY = ON`, `DENY VIEW ANY DATABASE`), member of `ApplicationRole`; the connection string, encrypted, goes nowhere, to the user secrets or to the vault of the Very Back (`-SecretTarget`), on a standard input only.

### Added

- Database tests: the application login made by the script executes the procedures but reads no table, and is refused an update of the audit (the done criterion of 0.7.0).
