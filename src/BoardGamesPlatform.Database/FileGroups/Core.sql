/**
 *	@file @prj/src/BoardGamesPlatform.Database/FileGroups/Core.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Filegroup of the persistent data: every table of the modules and their history tables
 *
 *	Purpose :
 *	---------
 *	- Business entities and source-of-truth records (Platform, and the modules of the next versions)
 *	- The audit (Logs) and the history tables of the temporal tables (History)
 *	- Keeps the data out of PRIMARY, which only holds the catalog of the database
 *
 *	Files :
 *	-------
 *	- No path is written here: SSDT adds the file of the filegroup at deployment, under the SQLCMD variables $(DefaultDataPath) and
 *	  $(DefaultFilePrefix), which SqlPackage reads from the target server (see doc/dev/database.md, Filegroups)
 *	- SSDT Warning : do not change the database name variable, it is substituted during build and deployment
 */
ALTER DATABASE [$(DatabaseName)]
	ADD FILEGROUP [Core];
