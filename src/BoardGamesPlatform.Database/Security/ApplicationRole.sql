/**
 *	@file @prj/src/BoardGamesPlatform.Database/Security/ApplicationRole.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Database role of the application: the only rights the Very Back has on the database
 *
 *	Information :
 *	-------------
 *	Family			=> Security
 *	Exposure		=> Application (Very Back only)
 *	Owner			=> dbo
 *	Members			=> The application SQL user of the database (one per database, named after it, random password: version 0.7.0)
 *
 *	Purpose :
 *	---------
 *	- Least privilege: the application can only execute the procedures of the schemas granted to this role
 *	- No right on the tables: the procedures reach them through ownership chaining (same owner dbo)
 *	- Every new schema of procedures is granted here explicitly; nothing is granted on dbo or on the whole database
 *
 *	Access :
 *	--------
 *	- EXECUTE on the schema ServerCore
 *	- Nothing else (checked by the permission test of BoardGamesPlatform.Database.Tests)
 */
CREATE ROLE [ApplicationRole] AUTHORIZATION [dbo];
GO

GRANT EXECUTE ON SCHEMA::[ServerCore] TO [ApplicationRole];
