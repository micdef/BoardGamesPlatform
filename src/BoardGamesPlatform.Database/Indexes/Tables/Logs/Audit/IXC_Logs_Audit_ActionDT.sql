/**
 *	@file @prj/src/BoardGamesPlatform.Database/Indexes/Tables/Logs/Audit/IXC_Logs_Audit_ActionDT.sql
 *	@author Defraene Michaël (AKA Sonic) (co-written by Claude)
 *
 *	@brief Clustered index of the audit, by date of the change
 *
 *	Information :
 *	-------------
 *	SQL Name	=> IXC_Logs_Audit_ActionDT
 *	Table On	=> Logs.Audit
 *	Type		=> Clustered, not unique (SQL Server adds a hidden uniquifier to rows of the same instant)
 *	Column(s)	=> ActionDT
 *	Filegroup	=> Core
 *
 *	Purpose :
 *	---------
 *	- New rows are written at the end of the index, in date order: no page split
 *	- The purge deletes a range from the start of the index (ActionDT before a date) without reading the whole table
 */
CREATE CLUSTERED INDEX [IXC_Logs_Audit_ActionDT]
	ON [Logs].[Audit] ([ActionDT])
	ON [Core];
