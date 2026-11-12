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
 *	Members			=> The application SQL user of the database (one per database, named after it, random password), added by
 *					   tools/database/Deploy-TestDatabase.ps1 after the publish: a user mapped to a login is not part of the DACPAC
 *
 *	Purpose :
 *	---------
 *	- Least privilege: the application executes the procedures, reads the views, passes the table-valued parameters, and nothing else
 *	- No right on the tables: the procedures and views reach them through ownership chaining (same owner dbo), which checks no permission
 *	  on the objects it reaches
 *	- Every right on the internal schemas is denied explicitly, so that a grant added by mistake later (directly, or to public) cannot open them
 *
 *	Access :
 *	--------
 *	*************************************************************************************************************************************
 *	* Securable				| Permission								| Why														*
 *	*************************************************************************************************************************************
 *	* Schema StorProc		| GRANT EXECUTE								| The writes												*
 *	* Schema ServerCore		| GRANT EXECUTE								| The Ping													*
 *	* Schema Views			| GRANT SELECT								| The reads													*
 *	* Schema Types			| GRANT EXECUTE, REFERENCES					| To declare and pass the table-valued parameters			*
 *	* Database				| GRANT VIEW DEFINITION						| To read the metadata; the checks of the audit depend on it	*
 *	* Schemas Logs,			| DENY SELECT, INSERT, UPDATE, DELETE,		| Internal: reached only through the procedures and views	*
 *	*   Platform, History,	|   EXECUTE, ALTER, REFERENCES,				| CONTROL is NOT denied: it would also deny VIEW DEFINITION	*
 *	*   Functions, dbo		|   TAKE OWNERSHIP							|   on the schema, and break the checks of the audit		*
 *	*************************************************************************************************************************************
 *	- Checked by the permission test of BoardGamesPlatform.Database.Tests (Security/ApplicationRoleTests)
 */
CREATE ROLE [ApplicationRole] AUTHORIZATION [dbo];
GO

/* Public schemas */
GRANT EXECUTE ON SCHEMA::[StorProc] TO [ApplicationRole];
GO
GRANT EXECUTE ON SCHEMA::[ServerCore] TO [ApplicationRole];
GO
GRANT SELECT ON SCHEMA::[Views] TO [ApplicationRole];
GO
GRANT EXECUTE ON SCHEMA::[Types] TO [ApplicationRole];
GO
GRANT REFERENCES ON SCHEMA::[Types] TO [ApplicationRole];
GO

/* Metadata */
GRANT VIEW DEFINITION TO [ApplicationRole];
GO

/* Internal schemas */
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP ON SCHEMA::[Logs] TO [ApplicationRole];
GO
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP ON SCHEMA::[Platform] TO [ApplicationRole];
GO
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP ON SCHEMA::[History] TO [ApplicationRole];
GO
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP ON SCHEMA::[Functions] TO [ApplicationRole];
GO
DENY SELECT, INSERT, UPDATE, DELETE, EXECUTE, ALTER, REFERENCES, TAKE OWNERSHIP ON SCHEMA::[dbo] TO [ApplicationRole];
