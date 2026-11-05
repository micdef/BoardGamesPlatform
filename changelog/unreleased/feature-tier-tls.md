### Added

- Gateway, Back and Very Back: the "Tls" settings (`IdentitySuffix`, `CertificatePath`, `CertificatePassword`, `TrustedRootsPath`, `RefusedThumbprints`, `ExpiryWarning`), checked at startup (`TlsOptionsValidator`); a relative path is taken from the folder of the tier. A tier refuses to start without a valid certificate of its role (fail closed), and the Gateway loads its certificate before it serves a front.
- Development certificates: `tools/certificates/New-DevelopmentCertificates.ps1` creates a development root and the PFX of the three tiers in `.certificates/dev` with the authority tool, and sets their password in the user secrets of each tier, which the `appsettings.Development.json` files point to. The Back and the Gateway get a `UserSecretsId`.
- Back tests: on the Back started with the registrations of Program.cs, the Gateway is served, and a valid certificate of another role is refused; a Back without its TLS settings, or with the certificate of another role, does not start.

### Changed

- The links Gateway → Back and Back → Very Back run over mutual TLS 1.3: the Back accepts the Gateway only and calls the Very Back only, the Very Back accepts the Back only, and the Gateway calls the Back only.
- Back and Very Back: a listen address other than loopback is logged as information, no longer as a warning, since the link is now encrypted and authenticated.
- Tests: the tests that start a tier as Program.cs does give it the certificates of `TestTlsFiles`, written once per run; the fake Back of the Gateway tests listens over mutual TLS.
