### Added

- Vault of the secrets (`BoardGamesPlatform.Vault`), native to the operating system behind one interface: the credentials of systemd on Fedora (`LoadCredentialEncrypted=`, read from `CREDENTIALS_DIRECTORY`), a folder of files encrypted with DPAPI `LocalMachine` on Windows (setting `Vault:Path`); read as the last configuration source, so that the tiers read their secrets under their usual keys.
- `tools/vault/Set-VaultSecret.ps1`: writes a secret of a tier into its DPAPI vault, as an administrator, from the standard input or a hidden prompt, with a protected access list and an atomic replacement.
- Tests of the vault (`BoardGamesPlatform.Vault.Tests`): the stores, the source added last that wins, the checks of the sources (a chained configuration, a configuration that cannot be inspected), the warnings 9071 and 9072 with their events, the refusals of the tool (a bad name, a relative folder, an unknown account), and on Linux a secret encrypted and decrypted by `systemd-creds`.
- Tests of the Gateway, the Back and the Very Back: each tier gives exactly its secrets to the vault, and each of them is refused on the command line of a server.

### Changed

- Gateway, Back, Very Back: their secrets (`Tls:CertificatePassword`, `Handshake:SigningKeyPassword`, `ConnectionStrings:Main`) come from the vault on the servers; the messages of a missing secret name the vault instead of an environment variable.
- Dependencies: the Microsoft packages of the line 10.0.x move to 10.0.15 (16 CVEs of .NET listed in the release metadata of 10.0.15); `System.Security.Cryptography.ProtectedData` 10.0.15 is pinned.
- Documentation: `doc/adm/configuration.md` describes the vault on Fedora and on Windows; `doc/adm/certificates.md`, `doc/adm/very-back.md` and `doc/dev/secure-channel.md` point to it.

### Security

- Outside the Development environment, a tier refuses to start when one of its secrets comes from another source than its vault (a JSON file, an environment variable of any prefix, the command line), naming the secret and the source, never the value; in Development it logs a warning (event 9071).
- The vault refuses a folder or a file whose owner or access list trusts another account than SYSTEM, the Administrators and the account of the service (Windows), a secret it does not know, a value with a control character, and a secret copied under another name.
- Very Back: outside Development, a connection string that does not encrypt (`Encrypt=False` or `Optional`) or that trusts the certificate of a remote SQL Server is refused at startup.
