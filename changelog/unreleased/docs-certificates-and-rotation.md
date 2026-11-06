### Added

- `doc/adm/certificates.md`: mutual TLS of the links for the administrator: the principles (offline authority of the platform, identity by role, no pinning of a leaf certificate, usages required), the prerequisites (Windows Server 2022 or Windows 11, OpenSSL 3), the commands of the authority tool, how to issue, protect (rights of the PFX for the account of the service, `EnvironmentFile=` rather than `Environment=`, `icacls`), renew (in place, without a restart, checked every hour), withdraw (refused thumbprints) and change the root, the `Tls` settings and the events 9031-9040.

### Changed

- Docs: the security limits of `doc/dev/protocol.md` describe the links as of 0.5.0 (the places of the handshakes and their residual risk behind a NAT, the largest payload deployed on both ends at once); `configuration.md` (certificates outside the repository, `Tls__CertificatePassword`, never `Environment=` in a systemd unit, the user secrets of the development certificates), `back.md`, `very-back.md` and `gateway.md` (mutual TLS, the `Tls` section, the listen address logged as information, the handshake timeout covering TLS, the back-off forgotten only after a response, 64 KiB frames); the roadmap records the decisions on the certificates, the prerequisite of TLS 1.3, and the passwords of the PFX in the vault of 0.7.0.
