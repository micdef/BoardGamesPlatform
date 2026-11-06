### Added

- Developer documentation, `doc/dev/secure-channel.md`: the specification of the secure channel of the Back → Very Back link, written before its code and reviewed by the security reviewer: three fixed-size handshake messages signed with pinned ECDSA P-256 keys, the hashes of both TLS certificates in the transcript, ephemeral ECDH with points checked on both sides, an HKDF-SHA256 key schedule, AES-256-GCM records with an implicit sequence number, a close record, a hard limit and a renewal before it, key files normalized, reloaded cold and hot, a removed key that closes its connections, failures that send nothing; with its prerequisites (.NET 10.0.13 or later), its threat model (in scope, out of scope, residual risks) and its test plan.

### Changed

- Roadmap: the public keys of the peer are pinned in a file named by the settings, taken again cold and hot as the certificates; the branches of 0.6.0 start with the specification and its threat model, reviewed by the security reviewer before any code.
