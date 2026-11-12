### Added

- Database foundation, specification: `doc/dev/database.md` describes the schemas, the filegroups, the audit and its purge by dates, the write pattern (table-valued parameters checked again, one JSON result per row, audit in the same transaction), the read pattern, the application principal and its permission matrix, the deployment script and the vault of the secrets of the tiers, with their threat model; written and reviewed before the code.

### Changed

- Roadmap: the vault (native to the operating system: DPAPI on Windows, `systemd-creds` on Fedora) and the audit (triggers, never a ledger table, purged in place by dates) are recorded as decisions; the branches of 0.7.0 are ordered.
