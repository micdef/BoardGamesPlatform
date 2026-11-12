/**
 *	@file @prj/src/BoardGamesPlatform.Database/FileGroups/Access.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Filegroup of the access structures: the indexes of the indexed views
 *
 *	Purpose :
 *	---------
 *	- Separates the structures that only serve the reads from the data they read (filegroup Core)
 *	- Holds no object in version 0.7.0: it exists so that the first indexed view does not need a change of the storage
 *
 *	Files :
 *	-------
 *	- No path is written here: SSDT adds the file of the filegroup at deployment, under the SQLCMD variables $(DefaultDataPath) and
 *	  $(DefaultFilePrefix), which SqlPackage reads from the target server (see doc/dev/database.md, Filegroups)
 *	- SSDT Warning : do not change the database name variable, it is substituted during build and deployment
 */
ALTER DATABASE [$(DatabaseName)]
	ADD FILEGROUP [Access];
