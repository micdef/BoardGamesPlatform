### Added

- `doc/adm/certificates.md`: mutual TLS of the links for the administrator: the principles (offline authority of the platform, identity by role, no pinning of a leaf certificate), the prerequisites (Windows Server 2022 or Windows 11, OpenSSL 1.1.1), the commands of the authority tool, how to issue, renew (in place, without a restart), withdraw (refused thumbprints) and change the root, the `Tls` settings and the events 9031-9038.

### Changed

- Docs: the security limits of `doc/dev/protocol.md` describe the links as of 0.5.0; `configuration.md` (certificates outside the repository, `Tls__CertificatePassword`, the user secrets of the development certificates), `back.md`, `very-back.md` and `gateway.md` (mutual TLS, the `Tls` section, the listen address logged as information, the handshake timeout covering TLS, the back-off forgotten only after a response, 64 KiB frames); the roadmap records the decisions on the certificates and the prerequisite of TLS 1.3.
