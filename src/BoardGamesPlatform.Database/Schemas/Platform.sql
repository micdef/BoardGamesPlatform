/**
 *	@file @prj/src/BoardGamesPlatform.Database/Schemas/Platform.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Schema of the module Platform: the tables of the platform itself, outside any game and any account
 *
 *	Information :
 *	-------------
 *	Family			=> Module
 *	Exposure		=> Internal
 *	Owner			=> dbo
 *	Object Types	=> Tables
 *	Object Naming	=> <Table>
 *	Filegroup		=> Core
 *
 *	Purpose :
 *	---------
 *	- Holds the settings of the platform that an administrator may change while it runs (registration switch, ...)
 *	- The accounts of version 0.8.0 bring their own module (Identity), and each game brings its own module
 *
 *	Access :
 *	--------
 *	- Application role: nothing, every right explicitly denied (see Security/ApplicationRole.sql)
 *	- Writes go through the procedures of StorProc, reads through the views of Views (ownership chaining, same owner dbo)
 */
CREATE SCHEMA [Platform] AUTHORIZATION [dbo];
