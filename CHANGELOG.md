# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.6.1] - 2026-11-11

Tests of time and space complexity for the hot paths of the protocol, and the fixes they found, documented in `doc/dev/complexity.md`. A frame written, a request written or read, and a record of the secure channel no longer allocate a copy of their content: their memory no longer grows with each byte. A peer that sends records of 1 byte can no longer make each one cost the erasure of 16 KiB. The bytes on the wire do not change. The project moves to the .NET SDK 10.0.404 and the runtime 10.0.15, which fixes 17 CVEs; the minimum is now 10.0.15.

### Added

- Tests of time and space complexity: `AllocationMeter` (`test/BoardGamesPlatform.TestKit`) counts the bytes an operation allocates on
  the thread of the test. The operation runs on a stream in memory, so that it completes synchronously; the count never depends on the
  load of the machine. The smaller of two series is kept, and `AllocationMeterTests` checks that the meter sees a known allocation.
- Complexity tests of the frame codec: a frame written allocates the same few bytes whatever its payload; a frame read allocates its
  payload and a fixed cost under 1 KiB; a frame that arrives byte by byte is read in one pass.
- Complexity tests of the envelopes: a request is written once and read without a second copy of its body; a response is written once
  and read into its body only; both ways of writing a request give the same bytes.
- `doc/dev/complexity.md`: how the complexity is tested, the cost of each hot path with its test, and the collections bounded by the
  limits of the links. A rule of the coding conventions: a hot path gets a complexity test.
- Complexity tests of the records: a record written and a record read allocate the same few bytes whatever their length, and a write
  is cut into the fewest records (one write of the stream below per 16 KiB, 18 bytes added per record).
- Tests of the erasure of the records: once a record is given, or refused, no byte of its plaintext stays in the stream; a full record
  then a record of 1 byte, sealed and opened in the same buffers, are both read back whole. A disposed stream keeps no nonce.

### Changed

- Roadmap: the entry of 0.6.1, and a level "Complexity" in the test strategy.
- Socket protocol, frames: a frame written builds its header and its payload in a buffer of the shared pool, instead of a new buffer of
  its size: no allocation per byte of payload (64 KiB less per frame of 64 KiB). The bytes of the frame are erased before the buffer goes
  back to the pool, since a payload may hold a secret.
- Socket protocol, messages: a request whose body is an object with properties is written in one pass, instead of its body first, then
  the envelope: 1 byte allocated per byte of request instead of 2. A body with extension data keeps the checked path, since an
  extension key may repeat a property; a body that the reader would refuse is now always an `ArgumentException` for the caller, never a
  `JsonException`. The body of a request read is checked as UTF-8 on its raw bytes,
  instead of a copy as text of 2 bytes per byte: 1 byte allocated per byte of request instead of 3. The bytes on the wire do not change.
- Secure channel, records: a record written is sealed into buffers kept by the stream (16 KiB per connection, under its write lock),
  instead of a new buffer of its size per record: 16 KiB less per record of 16 KiB. A record read uses buffers of the stream for its
  length, its nonce and the one-byte probe after the close record.
- Secure channel, records: once a record is given, only its own bytes are erased, instead of the whole buffer of 16 KiB. A peer that
  sends records of 1 byte can no longer make each one cost the erasure of 16 KiB. The disposal still erases the whole buffer, and the
  bytes on the wire do not change.
- Secure channel, records: the disposal also erases the nonces kept by the stream, which give the IV of their direction back from the
  sequence number; the ciphers are disposed first, so that no record is sealed with an erased nonce.

### Fixed

- Tests: the renewal test of the link client waits for the log of the renewal before it disposes the client. Otherwise the client
  logged the old connection as closed by the disposal, at random.
- Tests: the heartbeat test of a large request written slowly moves the clock only while the client writes. Once the request is
  written, the bytes left in the buffers of Linux (several MiB) are read without moving it: a client that waits for a silent server
  rightly closes after its timeout. The test failed at random on Linux.

### Security

- .NET: the SDK of `global.json` is 10.0.404, which brings the runtime 10.0.15 (17 CVEs fixed, among them three denials of service over
  the network whose component is not named); the CI uses it. The minimum of `doc/dev/secure-channel.md` and of the README is now
  10.0.15 instead of 10.0.13.

## [0.6.0] - 2026-11-06

An authenticated ECDH handshake on the Back → Very Back link, specified with its threat model in `doc/dev/secure-channel.md`. Inside the mutual TLS session, each end signs an ephemeral P-256 key with a long-term ECDSA key. That key is distinct from its TLS certificate and pinned by its peer. The signatures cover both TLS certificates: an attacker in the middle, even one holding valid certificates (its own, or stolen ones), can neither read nor change the traffic. The traffic then travels in AES-256-GCM records, and a connection is replaced before it reaches the limits of its keys. The signing keys are reloaded at startup and while the tier runs; a key removed from the list closes the connections that used it. Prerequisite: .NET 10.0.13 or later (see the prerequisites of `doc/dev/secure-channel.md`).

### Added

- Developer documentation, `doc/dev/secure-channel.md`: the specification of the secure channel of the Back → Very Back link. It was written before the code and reviewed by the security reviewer. It covers:
  - three handshake messages of fixed size, signed with pinned ECDSA P-256 keys, with the hashes of both TLS certificates in the transcript;
  - ephemeral ECDH, with the points checked on both sides, and an HKDF-SHA256 key schedule;
  - AES-256-GCM records with an implicit sequence number, a close record, a hard limit and a renewal before it;
  - key files in one normalized form, reloaded at startup and while the tier runs, a removed key closing its connections;
  - failures that send nothing to the peer;
  - its prerequisites (.NET 10.0.13 or later), its threat model (in scope, out of scope, residual risks) and its test plan.
- Secure channel, handshake (`Protocol.Transport.Secure`): `SecureHandshake` runs the three messages of the specification on a stream, as the client or as the server.
  - Both signatures (ECDSA P-256, IEEE P1363) cover the hashes of both TLS certificates, both nonces, both ephemeral keys and both key ids. Their labels name the version, the link and the role.
  - Every point received is checked on the curve by the channel itself (`P256`), and every signature value is checked in range, before any import.
  - A pinned key that is the key of the TLS certificate of the peer is refused. The last step checks that the key of the peer is still pinned.
  - `KeySchedule` derives one key and one IV per direction with HKDF-SHA256, and erases every intermediate secret.
  - A failure throws a `SecureChannelException` with its reason from a closed list (`SecureChannelFailure`); nothing is sent to the peer.
- Secure channel, keys:
  - `SigningPublicKey` reads a public key in one normalized form (named curve P-256, uncompressed point) and names it by the SHA-256 of that form: its key id.
  - `SigningKeys` loads the signing key of a tier (encrypted PKCS#8 PEM) and the file of the public keys of its peer. The tier refuses to start (fail closed) on a wrong password, a password equal to the one of the PFX, 0 or more than 4 keys of the peer, a duplicate, its own key, or a signing key equal to its TLS key.
  - The files are read again before each new connection and every minute, by modification time and SHA-256 of their content. Files that stay refused keep the current keys, and are logged again every 5 minutes with the time since those keys were taken.
  - A removed key raises `PeerKeysRemoved`. Each listener is told on its own; a failing one is logged (event 9063), and the periodic check of the register closes its connections (see the transport below).
  - The file of the peer is read as UTF-8 (a byte order mark is accepted, UTF-16 is refused). An unknown key id is carried by the `SecureChannelException`, for the logs.
  - The "Handshake" settings (`SigningKeyPath`, `SigningKeyPassword`, `PeerPublicKeysPath`) and their validator. Event ids 9057-9060 and 9063.
- Certificate authority tool: `signing-key --role <back|veryback> --out <folder>` creates the signing key of a tier, encrypted with `BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD`, and the line of its public key for the peer. The password of the PFX is refused.
- Secure channel, records: `SecureRecordStream` carries the bytes of a connection in AES-256-GCM records of 16 KiB at most.
  - The nonce is built from the IV of the direction and an implicit sequence number; the length is the associated data; a record of length 0 is the close record.
  - A normal disposal of a connection sends its close record first, so that the peer never takes it for a truncation.
  - At most 2^24 records per direction, the last one kept for the close record. A write that would pass that limit is refused before anything is written, and the stream stays usable.
  - A changed, replayed, reordered, dropped or cut record, a byte after the close, or a record beyond the hard limit closes the connection. The first record fails as `KeyConfirmationFailed`.
  - After a failed read, all later reads fail; after a failed write, all later writes fail, as on a lost connection. A stream whose reads are broken still sends its close record.
  - Writes are serialized inside the stream.
- Secure channel, transport: `SecureTransportListener` and `SecureTransportConnector` run the handshake inside the TLS session of the connection, within the handshake places and timeout of the link server and the connect timeout of the link client.
  - A refused handshake is logged with its reason (event ids 9051-9053); nothing is sent to the peer. The refusals of the server are paced per reason by `LogPace`, so that a frequent one never hides a rare one; those of the client follow the back-off of the link client. An unknown key id is logged in hexadecimal.
  - If the keys of the Back cannot be used, its calls fail with `link.unavailable`.
  - A removed key closes the connections of the peer, its close record first (`SecureConnectionRegistry`, event 9061), including a handshake that ends after the removal; each connection is closed once.
  - Every minute, the register closes a connection whose key is no longer pinned: a safety net when a removal could not be told (9063; a failed check is logged as 9067). The Very Back closes a connection 48 hours old (9062).
  - Other events: record failures (9054-9055), a close record that cannot be sent (9064), a connection that cannot be closed after a refused handshake (9065), a connection that cannot be closed cleanly (9066), a close record of a normal close that cannot be sent (9068, Debug), keys of a refused handshake that cannot be erased (9069, Debug). 9056 stays reserved.
- Links, renewal: `ITransportConnection.IsRenewalDue` tells that a connection should be replaced (on the secure channel: 2^23 records on a direction, or 24 hours).
  - The link client replaces such a connection in the background; the callers never wait for it.
  - The old connection stops taking new requests (`RequestClient.StopAccepting`). A request refused for that reason was never sent (`RequestNotSentException`), so it is sent again on the new connection (event 9020, Debug).
  - The old connection is closed once its requests ended (within the request timeout), or when the link client is disposed.
  - If the new connection cannot be opened, the old one is kept and the renewal is tried again after one minute.
  - Event ids 9008-9010: renewed, failed, refused by a key, a certificate or the protocol.
- Back and Very Back: the "Handshake" settings, checked at startup with the keys (fail closed). The Back calls the Very Back over mutual TLS and the secure channel; the Very Back accepts the Back only through it. The development settings point to the signing keys of `.certificates/dev`, which `New-DevelopmentCertificates.ps1` creates with the authority tool; their password is set in the user secrets of the two tiers, distinct from the one of the PFX.
- Tests, handshake and keys:
  - the primitives, the key schedule, both labels and the composition of the transcript, against vectors computed by the Python package `cryptography` and an HKDF written from RFC 5869 (`generate_vectors.py`, never the code under test), and RFC 5869 case A.1;
  - the handshake over loopback sockets: agreement; a key not pinned, other certificates, on each side; a signing key that is the TLS key; a key removed during the handshake; no certificate;
  - changed first, second and third messages; an ephemeral key replaced in the middle; the signature of the server sent back; random and mutated messages with fixed seeds; a replayed handshake;
  - the load and the reload of the key files: refused files told again at their pace, files readable again unchanged no longer refused, refused files still refused after a read failure that passed.
- Tests, channel:
  - the first two records against values sealed by the Python package `cryptography`; round trips of random writes; every change of a record; the hard limit and the renewal threshold; writes at the borders of a record;
  - over real mutual TLS, the done criterion of 0.6.0: a tier with a valid certificate but a signing key not pinned is refused on both sides; an attacker in the middle that ends TLS with valid certificates of its own makes the signatures fail; one that holds the stolen certificates only relays records it cannot read;
  - a server without the channel, and a client without it (refused at the magic); a client silent after TLS, or that never sends its third message, closed within the handshake timeout of the link server, its place freed;
  - a removed key closing an established connection; a key removed before, or while, its connection is attached; the age limit;
  - a normal close read as a clean end; a close record that cannot be written; failed reads and writes; a write refused at the hard limit leaving the stream usable; a record changed during a session giving `link.unavailable`; the pace of the refusals per reason;
  - the renewal by the link client: replaced, a request in flight answered, the old connection kept on a failure, over a real secure connection, at 24 hours; the draining request client; a disposed request client ending its connection cleanly; a disposed link client closing its replaced connections at once;
  - a Back and a Very Back without valid keys do not start; the password of the PFX refused by `signing-key`.

### Changed

- Roadmap: the public keys of the peer are pinned in a file named by the settings, reloaded at startup and every minute, as the certificates are; the branches of 0.6.0 start with the specification and its threat model, reviewed by the security reviewer before any code.
- Transport: a TLS connection keeps the certificate this end presented in its session, so that the secure channel hashes it (`SslStream.LocalCertificate` is null on a TLS 1.3 client).
- Test kit: `TestTlsFiles` also writes the signing keys of the Back and the Very Back, adds their "Handshake" settings, and gives the Back a connector of the Very Back over the secure channel.
- Docs: `doc/dev/protocol.md` (the layer of the secure channel, the heartbeat on authenticated records, the renewal, the event ids), `doc/dev/secure-channel.md` (the certificates taken from the session, the event ids, the pace of the refusals), `doc/adm/certificates.md` (protecting the key files; setting up, rotating and revoking the signing keys; the events of the range 9051-9069, and 9020), `back.md`, `very-back.md`, `configuration.md`, `tools/certificates/README.md`, the coding conventions.

## [0.5.0] - 2026-11-06

Mutual TLS 1.3 on both socket links, documented in `doc/adm/certificates.md`. The Gateway → Back and Back → Very Back links run over TLS 1.3 only, each end presenting the certificate of its role, issued by the offline authority of the platform. A tier refuses to start without a valid certificate, and takes a renewed one without a restart. The four limits deferred by the security review of 0.4.0 are in: places of the handshakes per source, 64 KiB frames per link, a deadline on the server writes, and a back-off forgotten only after a response. Prerequisite: Windows Server 2022 or Windows 11, or Linux with OpenSSL 3.

### Added

- `doc/adm/certificates.md`: mutual TLS of the links for the administrator: the principles (offline authority of the platform, identity by role, no pinning of a leaf certificate, usages required), the prerequisites (Windows Server 2022 or Windows 11, OpenSSL 3), the commands of the authority tool, how to issue, protect (rights of the PFX for the account of the service, `EnvironmentFile=` rather than `Environment=`, `icacls`), renew (in place, without a restart, checked every hour), withdraw (refused thumbprints) and change the root, the `Tls` settings and the events 9031-9040.
- Certificate authority of the platform (`src/BoardGamesPlatform.CertificateAuthority`): an offline command-line tool, run by hand on the machine that keeps the root key, which opens no port. `init` creates the root (ECDSA P-256, self-signed, allowed to sign end certificates only), `request` creates the key of a tier on its own machine with its request (PKCS#10), `sign` signs a request for a role, `install` joins the certificate and the key into the PFX the tier loads, after checking that it comes from the authority, and `list` shows the certificates issued, recorded in `issued.csv`. The key of a tier never leaves its machine; the authority takes only the public key from a request, checks its signature, refuses a request of another role or with another kind of key (the curve P-256 is checked by name, not only by size), and decides everything else: the name of the role, its usages, a validity of at most 397 days within the one of the root, starting 5 minutes before the signature so that a tier whose clock is a little late accepts it. A certificate is recorded in `issued.csv` before its file is written, so that the register never misses one. Keys are written encrypted (PKCS#8 with AES-256 and 600 000 iterations, PFX with AES-256, SHA-256 and 300 000 iterations, the most .NET loads with its default limits), never over an existing file, readable by their owner only on Linux; passwords come from `BOARDGAMESPLATFORM_CA_PASSWORD` and `BOARDGAMESPLATFORM_CERTIFICATE_PASSWORD` or a hidden prompt, never from the command line. Exit codes: 0 on success, 1 when the command fails (any failure, a file that is not a certificate named), 2 when the command line is wrong.
- Socket protocol, identity of the tiers (`PlatformIdentity`, `PlatformRole`): the name a certificate carries for each role, `gateway`, `back` or `veryback` followed by the suffix of the deployment (for example `back.bgp.internal`), and its usages (TLS server for the Back and the Very Back, TLS client for the Gateway and the Back), shared by the authority and the tiers that check the certificates.
- Tests (`BoardGamesPlatform.CertificateAuthority.Tests`): what the root and the certificate of each role hold, the requests refused (tampered, another role, another kind of key, invalid validity, an authority without its key or that is not one), and the commands from the root to the PFX of a tier.
- Socket protocol, link server: `LinkServerLimits` (served connections, connections in their handshake, connections in their handshake per source, pace of the refusal logs), with a constructor of `LinkServer` that takes it and the clock of the logs. Event ids 9041-9043.
- Back and Very Back tests: a client that offers the 1 MiB of the protocol is refused in the handshake (`protocol.limitsMismatch`), and a frame one byte larger than the link allows closes the connection without an answer.
- Socket protocol, mutual TLS 1.3 (`Protocol.Transport.Tls`): `TlsTransportListener` and `TlsTransportConnector` open a TLS 1.3 session over TCP, both ends presenting the certificate of their role. A certificate is accepted when it chains to the roots of the platform only (never the trust store of the system), is valid now and allowed for the usage of its side, holds the DNS name of the role expected and nothing else, and is not in the refused list (`PeerCertificateValidator`). The Back accepts the Gateway only, the Very Back the Back only, so that the key of one tier never opens a link meant for another one. TLS 1.3 only, no renegotiation, no session resumption. A certificate without extended usages is refused. A refused peer is logged with the reason and the thumbprint of its certificate (event ids 9031-9035), never with a text the peer chose, once every 10 seconds at most (the next entry gives the number held back), so that failed handshakes in a loop cannot flood the logs.
- `TlsCredentials`: the certificate of a tier (a PFX), the roots it trusts and the thumbprints it refuses. A tier refuses to start on Windows before Windows Server 2022 and Windows 11 (no TLS 1.3), or with a certificate its peers would refuse (no key, another authority, expired, another role, a missing usage, refused). Before each new connection and every hour, a renewed PFX or file of roots is taken without restarting the tier; files that fail the check are logged and the current certificate is kept, files that cannot be read yet are tried again at the next check. A certificate that expires within 30 days is logged as a warning when loaded and once a day at most by the hourly check, an expired one as an error (event ids 9036-9039).
- Transport: `ITransportListener.EstablishAsync` authenticates an accepted connection in the task that serves it, `ITransportConnection.CloseSendAsync` closes the sending side only (close_notify then FIN on TLS), and `ITransportConnection.PeerIdentity` gives the identity proven by the certificate of the peer.
- Test kit: `TestCertificates` builds a root and the certificates of the roles at run time, with the code of the authority; no private key is committed.
- Protocol tests: `GatedWriteConnection`, whose writes go through only when the test lets them; a response never read ends the service at the deadline, and a large response read slowly keeps it.
- Gateway, Back and Very Back: the "Tls" settings (`IdentitySuffix`, `CertificatePath`, `CertificatePassword`, `TrustedRootsPath`, `RefusedThumbprints`, `ExpiryWarning`), checked at startup (`TlsOptionsValidator`); a relative path is taken from the folder of the tier. A tier refuses to start without a valid certificate of its role (fail closed), and the Gateway loads its certificate before it serves a front.
- Development certificates: `tools/certificates/New-DevelopmentCertificates.ps1` creates a development root and the PFX of the three tiers in `.certificates/dev` with the authority tool, and sets their password in the user secrets of each tier (piped, never on a command line), which the `appsettings.Development.json` files point to; with `-Force`, the tool is built before the current certificates are replaced. The Back and the Gateway get a `UserSecretsId`.
- Back tests: on the Back started with the registrations of Program.cs, the Gateway is served, and a valid certificate of another role is refused; a Back without its TLS settings, or with the certificate of another role, does not start.
- Protocol tests: `KillablePeer.CrashOnPing`; a peer in a crash loop is reached once per back-off delay, the delay doubles, and the peer is served again once it answers.
- Socket protocol, messages: property-based and fuzz tests of the JSON readers (request and response envelopes, control messages: random bytes, mutated messages, truncation at every offset, broken structures, excessive nesting, round trips, no peer text in the refusals), run with fixed seeds so that every failure can be replayed.

### Changed

- Roadmap: the decisions on the largest frame per link (64 KiB, why, and checked again against the game states of 0.13.0), on the signing keys of the handshake of 0.6.0 (public keys pinned in the settings, as a list for rotation, no second authority) and on the instances (one per tier until 1.0.0, several in 1.1); 0.11.0 gets the paging rule (a list that crosses a socket link is answered in pages, never above 64 KiB), with the matching tests of 0.11.0 and 0.13.0.
- Docs: the security limits of `doc/dev/protocol.md` describe the links as of 0.5.0 (the places of the handshakes and their residual risk behind a NAT, the largest payload deployed on both ends at once); `configuration.md` (certificates outside the repository, `Tls__CertificatePassword`, never `Environment=` in a systemd unit, the user secrets of the development certificates), `back.md`, `very-back.md` and `gateway.md` (mutual TLS, the `Tls` section, the listen address logged as information, the handshake timeout covering TLS, the back-off forgotten only after a response, 64 KiB frames); the roadmap records the decisions on the certificates, the prerequisite of TLS 1.3, and the passwords of the PFX in the vault of 0.7.0.
- Socket protocol, link server: a connection in its handshake (TLS, then protocol) takes one of 4 places of its own, 2 of them at most from one IP address, and a served place only once its handshake is done; a silent or unknown peer can no longer take the places of the connections the tier serves. When the places of the handshakes are taken, the oldest connection in its handshake (of the same source when it holds its part, else of all) is closed to make room for the new one, so that a peer must open connections without pause to keep the client tier out. A connection whose handshake ends when every served place is taken is closed.
- Socket protocol, link server: a refused connection, or one closed to make room, is logged at most once every 10 seconds (`LogPace`, shared with the TLS listener); the next log gives the number of refusals in between, so that a flood of connections does not flood the logs.
- Tests: the socket protocol tests get a second block of test ports (17600-17699), locked only once the first one is used up (`TestPortBlockSet`).
- Socket protocol, links Gateway → Back and Back → Very Back: the largest payload of a frame is 64 KiB instead of 1 MiB (`BackOperations.LinkMaxPayloadLength`, `VeryBackOperations.LinkMaxPayloadLength`), used by both ends and by the dispatcher of the responses, so that the memory a peer can make a server hold (connections × requests in progress × largest payload) drops from about 512 MiB to about 32 MiB. `LinkClient` takes the largest payload of its link.
- Tests: the fake tiers and the clients of the tier tests use the largest payload of their link (`TestRequestClients.ConnectAsync` takes it).
- Links, link server: the connection is established by the listener (the TLS handshake), then the protocol is agreed, both within the one handshake timeout, in the task of the connection, so that a slow or silent peer never holds the accept loop. A connection is logged as accepted once established; a failed establishment, logged by the listener, is only traced (Debug, event id 9040).
- Links, link client: a peer refused by its certificate, or that refuses this tier, is `link.unavailable` for the caller, instead of an exception of the TLS layer.
- Socket protocol, request server: a write that makes no progress (no piece of 64 KiB written) for the idle timeout of the heartbeat (45 seconds) ends the service of the connection with a `TimeoutException`; the deadline moves with each piece, so a large frame read slowly is not cut. A client that stops reading while it keeps the connection alive with cancellations can no longer hold the write lock, its slots and its responses for ever.
- The links Gateway → Back and Back → Very Back run over mutual TLS 1.3: the Back accepts the Gateway only and calls the Very Back only, the Very Back accepts the Back only, and the Gateway calls the Back only.
- Back and Very Back: a listen address other than loopback is logged as information, no longer as a warning, since the link is now encrypted and authenticated.
- Tests: the tests that start a tier as Program.cs does give it the certificates of `TestTlsFiles`, written once per run; the fake Back of the Gateway tests listens over mutual TLS.
- Socket protocol, link client: the failures in a row are only forgotten once a connection answers a request (an error code of the peer included), no longer as soon as it opens. A connection lost before any response counts as a failure to open, so that a peer in a crash loop (it accepts the handshake, then dies at the first request) is reconnected with the back-off, doubled each time, instead of on every call. Event id 9007 (`LinkClient` now uses 9001-9007).

### Fixed

- Socket protocol, request server: the cancellation asked by a client runs in its own task, no longer in the read loop, so that a callback of a handler that blocks never stops the server from receiving the pings, the other cancellations and the next requests of the connection. The service waits for these cancellations before it ends, and a failing callback still ends it with its exception.
- Database tests: the check of the names of the stored procedures no longer fails on Linux (the paths of the SQL project use "\").
- Socket protocol, heartbeat: any byte received now counts as a sign of life, not only a whole frame, on the client and on the server, so that a large frame on a slow link (1 MiB at less than about 35 KB/s) no longer closes the connection. The frame codec writes a frame larger than 64 KiB in pieces and tells the writer after each one: the request client counts a piece of a large frame written as a sign of life (a short frame written never counts, so short requests sent often never keep a silent server alive), and skips its ping while another frame is being written instead of waiting for the write lock until its timeout.
- Links, link server: when the listener is lost without a stop request, or the accept failures repeat, the service of every connection is canceled, so that the run fails and the tier stops. Until now it waited for ever for connections that nothing ended any more.

## [0.4.0] - 2026-11-05

Robust socket protocol, specified in `doc/dev/protocol.md`. Every connection opens with a version handshake. A failed request is answered with an error code, and the connection that every caller shares stays open. A cancellation crosses the tiers, down to the database call. A heartbeat detects a silent peer. Requests are bounded in time and connections in number. The links reconnect with back-off. Development only: the links are still plain TCP, and mutual TLS 1.3 comes in 0.5.0.

### Added

- Developer documentation, `doc/dev/protocol.md`: the specification of version 1 of the socket protocol (frames, handshake, control messages, envelopes, error codes and what closes a connection, cancellation, heartbeat, limits, reconnection, reserved log event ids).
- Socket protocol, heartbeat: the request client sends `{"control":"ping"}` every 10 seconds, and the request server answers `{"control":"pong"}`. The client closes the connection when it has received no frame at all for 30 seconds (its pending requests fail with a `TimeoutException` as the cause). The server ends the service of a connection on which it has received nothing for 45 seconds. Any frame counts. No read is ever canceled without closing the connection, and the write of a ping is bounded by the client timeout. A ping sent by the server is a protocol violation.
- `HeartbeatSettings`: the three timings, validated (both timeouts longer than the interval). The request client and the request server take them, with a clock (`TimeProvider`), so that the tests run on a fake clock.
- Socket protocol, error codes: a response holds either its body or an error, `{"error":{"code":"…"}}`, never both. The codes are short dotted names, never display texts and never internal details (`ErrorCodes`: `request.unknownOperation`, `request.invalid`, `internal`, `link.unavailable`). The caller receives a `RemoteErrorException` holding the code, and the connection stays open.
- Socket protocol, `LinkFailureException`: thrown by the client of a link when the peer cannot be reached or the connection is lost before the response, with the code `link.unavailable`. The cause is kept as the inner exception, for the logs of the tier.
- Links, request timeout: a request that gets no response within the request timeout fails with a `LinkFailureException` whose code is `link.timeout`, and it is canceled on the peer. The setting is `Back:RequestTimeout` in the Gateway (8 seconds by default) and `VeryBack:RequestTimeout` in the Back (5 seconds by default), greater than zero and at most 5 minutes, checked at startup. The timeouts decrease along the chain (website 10 s > Gateway 8 s > Back 5 s), so that each tier still answers a code before its caller gives up.
- Links, maximum of connections: a link server serves at most 8 connections at the same time. A new one beyond that is closed at once and logged as a warning (event id 9019).
- Socket protocol, control messages: frames on the reserved correlation identifier 0 carry control messages, JSON objects whose first property `control` names their kind. They are read strictly, and an unknown kind closes the connection.
- Socket protocol, handshake (`ProtocolHandshake`): every connection opens with a `hello` from the client (the range of protocol versions it speaks and its largest payload). The server answers `helloAck`, with the newest common version (1 for now) and its limit of requests handled at the same time, or `helloRefused`, with the code `protocol.versionUnsupported` or `protocol.limitsMismatch`, and closes. A request before the hello is a protocol violation. A server allows 5 seconds for the handshake. A client bounds it with its connect timeout and checks the answer again.
- Test kit: `TestRequestClients.ConnectAsync` opens a request client on a listening tier with the handshake, as a real client does.
- Links, reconnection with back-off: a lost connection is opened again by the next call, at once. When opening fails, the next attempt waits 0.5 second, doubled after each failure up to 30 seconds, spread at random by 20 % either way. Until then, calls fail at once with `link.unavailable` without touching the network (event id 9006 tells when the next attempt is due). The first connection that opens resets the delay. A failed request is never retried automatically. Done criterion of 0.4.0, proven by a test peer that goes down in the middle of a session and comes back on the same port: the request in flight fails with the code, the calls while it is down fail at once, and the first call once it is back succeeds.
- Request client, `IsOpen`: false from the very start of the close, so that the link client never picks a connection that is closing.
- Socket protocol, request cancellation: when the caller of a request gives up, the request client sends the control message `{"control":"cancel","correlationId":N}` once the request is written. The request server cancels the token of that request only, and answers nothing. A cancellation of a request that already ended, or never existed, is ignored. Through the tiers, a front that disconnects stops the work of its call in the Back and in the Very Back, down to the call to the database (end-to-end test).
- Socket protocol, error code `server.busy`: a request beyond the limit of requests in progress of the connection is answered at once with this code, without being started.
- Socket protocol, request client: it never has more requests in progress than the limit the server announced in the handshake. Extra calls wait for a place.
- Socket protocol, framing: property-based and fuzz tests of the frame codec (random round trips in random chunks, random bytes, truncation at every offset, oversized lengths), run with fixed seeds so that every failure can be replayed.

### Changed

- Roadmap: the done criterion of 0.4.0 is rewritten to what a test can prove (in-flight requests fail with a code, calls fail at once while the Very Back is down, the first call once it is back succeeds), the branches of 0.4.0 are listed as delivered, and the decisions on the protocol version, the error responses and the robustness of the protocol are recorded.
- Socket protocol, operation dispatcher: an unknown operation, a body that does not match its operation, or a failing handler is answered with an error code instead of closing the connection that every caller shares. A tier behind that cannot be reached is answered `link.unavailable`, any other failure `internal`. The details go to the log of the tier only (event ids 9021-9024). A malformed envelope still closes the connection. The dispatcher now takes the logger of the tier.
- Back: when the Very Back cannot be reached, a `Ping` of the Gateway is answered `link.unavailable`, and the connection of the Gateway stays open.
- Links: the link server runs the handshake on each accepted connection before serving it. The link client runs it after connecting, within the connect timeout. A refusal reaches the caller as a `LinkFailureException` that keeps the code of the refusal; any other failure of the handshake gives `link.unavailable`.
- Socket protocol, request server: it never stops reading while the connection lives, so that a cancellation is received even when every slot is busy (until now it stopped reading when its slots were full). A request whose identifier is still in progress, or a control message other than a cancellation, is a protocol violation. The slot of a request is given back just before its response is written, so that the next request of the client always finds it free.

### Fixed

- Socket protocol, operation dispatcher: a response larger than the link can carry, or that cannot be written, is answered `internal` and logged (event id 9025). It no longer closes the connection that every caller shares, which then failed them all with `link.unavailable`.
- Socket protocol, operation dispatcher: a code answered by a tier behind is no longer logged as a handler failure. It is logged as a warning (event id 9026), `server.busy` is passed on, and any other code becomes `internal`. A handler that fails with any exception once its request is canceled is no longer answered.
- Socket protocol, serializer: a request whose operation name or body the reader would refuse is refused before it is written, with an `ArgumentException` to the caller, instead of closing the shared connection. The errors of a malformed message no longer copy any text of the peer (property names, values), nor keep an inner exception that could, so that a peer cannot write fake lines or control characters into the logs.
- Socket protocol, request server: the failure of a response write is recorded while the write lock is still held, so that the end of the service always reports it. After a client closes its sending side, the requests still running are canceled once the idle timeout has passed, instead of holding the connection for ever.
- Socket protocol, request client: a failure to close the connection met while sending a cancellation is thrown by `DisposeAsync` instead of being lost. The reason of a close is exposed (`CloseReason`) and logged with the loss of the connection (event id 9002).
- Links: the request timeout now covers the whole call, the wait for the connection included, so that the timeouts of the tiers add up. The back-off counts on a monotonic clock, never waits more than 30 seconds, the random spread included, and counts an opening cut by the request timeout as a failure. Requests canceled at the end of the drain after a client closed are reported as a failure of the service, logged by the link server. The client never keeps more than 64 requests in progress, whatever the server announces. A connection that cannot be closed is logged (event id 9016) instead of stopping the accept loop, and a failure to close after a failed handshake no longer hides the failure of the handshake.

## [0.3.0] - 2026-10-30

Walking skeleton: a button on a Blazor page shows the time of SQL Server, obtained through the five tiers, and an end-to-end test proves it. Development only: the links between the tiers are plain TCP and nothing is authenticated yet.

### Added

- Socket protocol, messages: JSON envelopes (`{"operation":…,"body":…}` for a request, `{"body":…}` for a response), read strictly (unknown members, missing parts, null bodies and deep nesting are protocol violations); an operation dispatcher that routes each request to the handler of its operation; typed requests on the request client.
- Ping contracts: the `Ping` operation and its request and response bodies in the Very Back and Back contracts, and the platform hub path and `Ping` method in the Gateway contract.
- Database: the `ServerCore` schema, the `ServerCore.SP_ServerCore_Server_Ping` procedure (the server time as JSON, with its offset) and the `ApplicationRole` role, allowed to execute the procedures of `ServerCore` only; accepted static analysis warnings documented in `StaticCodeAnalysis.SuppressMessages.xml`.
- SQL tests (`BoardGamesPlatform.Database.Tests`): conventions of the SQL files (declared in the project, headers, procedure sections and naming), run everywhere; the Ping procedure and the permission matrix of the application role, on a `TEST_` or `DEV_` database, skipped without one.
- `Microsoft.Data.SqlClient` 7.1.1, with its transitive `System.Security.Cryptography.Pkcs` pinned to 10.0.14 (CVE-2026-84008, CVE-2026-70162).
- Socket protocol, request/response correlation: a request client that sends several requests on one connection and pairs each response with its request (identifiers 1, 2, 3…; 0 is reserved), and a request server that answers them, several at a time up to a limit per connection. Any close of the connection fails the pending requests; a response to an unknown identifier closes the link; a canceled request never interrupts a frame being written.
- Socket protocol, transport: interfaces of a connection (a byte stream), a listener and a connector, so that TLS can wrap the stream in 0.5.0; their plain TCP implementation (development only), with the Nagle delay disabled; a listener on port 0 gets its port from the system. First loopback integration tests.
- Database tooling (`tools/database/Deploy-TestDatabase.ps1`): builds the SQL project into a DACPAC and deploys it to a throwaway database on the local SQL Server, dropping and recreating it; refuses any database name that does not start with `DEV_` or `TEST_` (upper case), before any build or connection.
- CI: a separate job builds the SQL project, in its original format, with the MSBuild of Visual Studio of the runner image, and publishes the DACPAC as the `database-dacpac` artifact.
- Socket protocol, framing: frames with a 12-byte big-endian header (payload length, correlation identifier) and an opaque payload, read whole whatever the way the stream delivers the bytes; a frame larger than the maximum (1 MiB by default, 16 MiB at most) is refused before its payload is read; a stream ending inside a frame is refused; a frame is written in a single call. After any failure or cancellation the connection is closed, never reused. Tested by `BoardGamesPlatform.Protocol.Tests`.
- Changelog fragments: each pull request describes its changes in `changelog/unreleased/<branch>.md`; `tools/changelog/Merge-ChangelogFragments.ps1` moves them into `CHANGELOG.md` for the release; the CI refuses a pull request other than `docs/release-*` that edits `CHANGELOG.md`.
- The CI runs the tests of the changelog merge script on every pull request and push, so that a change that breaks it is caught before the release step uses it.
- Back: listener of the Gateway on plain TCP (development only), configured by the `Back` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7821), answering the `Ping` operation with the time the Very Back gives, offset included; one client of the Very Back, shared by every connection, configured by the `VeryBack` section (`Host`, `Port` 7822, `ConnectTimeout`). The host refuses to start on an invalid value, before it listens.
- Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Back tests (`BoardGamesPlatform.Back.Tests`): settings validation, the host setup, and the listener of the Gateway on loopback sockets with a fake Very Back.
- `VeryBackClient` in `BoardGamesPlatform.Platform.Infrastructure`: the client the Back uses to call the Very Back. It opens one shared connection on first use (within a connect timeout), opens a new one on the next call when the connection has closed, never retries a failed request, and offers the Ping operation.
- `VeryBackClientOptions`: the `VeryBack` settings (`Host`, `Port` 7822 by default, `ConnectTimeout` 5 seconds by default, at most 5 minutes), checked at startup so that the Back refuses to start on an invalid value.
- `BoardGamesPlatform.Platform.Infrastructure.Tests`: tests of the Very Back client against a fake Very Back on a loopback socket.
- Website, ping page (`/ping`): a button calls the Ping method of the Gateway and shows the time of SQL Server with its offset, formatted with the culture of the request, with a localized "in progress" state and a localized generic error (never the text of the exception).
- Website, client of the Gateway: `IGatewayClient` and its `SignalRGatewayClient`, a scoped service (one SignalR connection per circuit, started on first use, disposed with the circuit). Setting `Gateway:HubUrl` (also `Gateway__HubUrl`, default `http://localhost:5273/hubs/platform`), validated at startup: an invalid value stops the website and the message names the setting, never its value. Setting `Gateway:RequestTimeout` (default 10 seconds, from zero excluded to one minute) bounds every call: a Gateway that never answers gives the generic error. Disposing the client cancels the calls in progress and the calls waiting to connect. Plain HTTP until authentication (0.10.0).
- Website, languages: every text comes from `.resx` resources (`WebTexts`, English by default and French); the supported cultures are `en` and `fr` (that the culture chosen by the request localization middleware reaches the circuit is not verified end to end).
- Website tests (`BoardGamesPlatform.Web.Tests`): the page rendered by bUnit with a fake client (time, progress, error without the exception message, English and French), the SignalR client against the real Gateway started in process with a fake Back client (restart after a loss, single start for parallel calls, cancellation, timeout, disposal), and the `Gateway:HubUrl` setting.
- `bunit` 2.13.3, for the website tests only.
- Administrator documentation: `doc/adm/website.md`.
- End-to-end tests (`BoardGamesPlatform.EndToEnd.Tests`, test ports 17500-17599): a headless SignalR front calls the real Gateway, which calls the real Back, which calls the real Very Back, started in one process (settings in memory, start in dependency order, stop in the reverse order, each stop bounded and every failure collected). The front reaches the Gateway through the in-memory test server by long polling, not through a socket: only the hops Gateway to Back to Very Back are real TCP connections. With a fake Ping procedure (CI): the front receives exactly the fake time with its non-local offset; a failing procedure and a stopped Very Back give the generic SignalR error only, and the Gateway stays usable. With the real `SqlPingProcedure`: the same Ping on the database named by `BOARDGAMESPLATFORM_TEST_DATABASE`, skipped without it.
- TestKit, `TestDatabase`: the single guard of the tests that use a real SQL Server (the variable, the skip reason, and the refusal of any database whose real name does not start with `TEST_` or `DEV_`), with unit tests of the name rule.
- Gateway, `BackClient`: the real client of the Back, built on the `LinkClient` of the protocol, with plain TCP (development only until TLS in 0.5.0). It replaces the temporary `UnavailableBackClient`, which is removed: `Ping` of the hub now reaches the Back.
- Gateway settings of the link to the Back, section `Back` (`Back:Host` default `127.0.0.1`, `Back:Port` default 7821, `Back:ConnectTimeout` default 5 seconds), validated at startup: an invalid value stops the Gateway and the message names the setting, never its value.
- Gateway tests: the `Back` settings, `BackClient` against a fake Back on a loopback socket (time with offset, unreachable Back, disposal), and the hub with the real client (Ping through the hub, generic error when the Back does not answer, invalid `Back:Port` stops the startup).
- Gateway tests use their block of fixed test ports (17400-17499, `TestPortBlocks.Gateway`), never a port handed out by the system.
- Gateway, SignalR hub: `PlatformHub` on the path `/hubs/platform` (setting `Gateway:HubPath`, validated at startup, must start with a slash) with the `Ping` method, which asks the Back through `IBackClient` and returns its time with its offset. Detailed errors are off: a front never receives an internal message. The hub is not authenticated until 0.10.0.
- Gateway tests (`BoardGamesPlatform.Gateway.Tests`): the Gateway started in process with a fake Back client, called by a SignalR client (Ping, offset of the time, failing Back without internal message, hub path setting, `/health`).
- `Microsoft.AspNetCore.Mvc.Testing` and `Microsoft.AspNetCore.SignalR.Client` 10.0.14, for the Gateway tests only.
- Very Back: listener of the Back on plain TCP (development only), configured by the `VeryBack` section (`ListenAddress`, default `127.0.0.1`, another address accepted with a warning; `Port`, default 7822), answering the `Ping` operation with the time of SQL Server; a failing connection is closed without stopping the others.
- Very Back: `ConnectionStrings:Main` (empty in `appsettings.json`, set by user secrets or an environment variable); the host refuses to start without it, with a string the SQL client cannot read (the error, which may quote a piece of the string, is not repeated), or with an invalid address or port.
- Very Back: call of `ServerCore.SP_ServerCore_Server_Ping`, whose JSON result is read strictly (one `ServerTime` property, with an explicit offset).
- Very Back: a lost listener, or 50 accept failures in a row, stop the host with a critical log and the exit code 1, so that a service manager can restart it.
- Very Back tests (`BoardGamesPlatform.VeryBack.Tests`): settings validation, reading of the Ping result, the host setup, the exit code, and the listener on loopback sockets with fake procedures and a fake listener.
- `tools/changelog/tests/Test-MergeChangelogFragments.ps1`: plain PowerShell tests of the merge script, run on throwaway copies (one case per defect above, `-WhatIf`, and the real `CHANGELOG.md`).
- Website tests: every key exists in both languages, no text is empty, every key asked in the source exists, no .razor file holds a visible literal text (a heuristic, not a Razor parser), and the not found page, the error banner, the error page and the reconnection dialog rendered by bUnit in English and in French.
- Very Back: its SQL sessions are named `BoardGamesPlatform.VeryBack` (`program_name` on SQL Server) when the connection string sets no `Application Name`; a value set by the administrator is kept.
- Very Back: documentation of the connections to SQL Server (pool, `Max Pool Size`, `Load Balance Timeout` / `Connection Lifetime`, `Pooling=False`).

### Changed

- Roadmap, 0.3.0: socket protocol decided (byte-stream transport, length-prefixed frames with a correlation identifier and a JSON payload, maximum frame size, several requests in flight), default ports 7821 (Gateway → Back) and 7822 (Back → Very Back) on the loopback address, ordered branch list, end-to-end test against a fake procedure executor in the CI; the SQL project keeps its original format.
- Contribution guide: two pull requests open at the same time never touch the same file.
- Roadmap and administrator documentation: the website uses the Blazor Interactive Server render mode only (no WebAssembly, no static rendering), and its server process is the only SignalR client of the gateway; the gateway is never reachable from the Internet. The decision and its reasons are in the decisions table.
- Roadmap: every executable runs on Kestrel as a service, without IIS; the website is exposed behind a reverse proxy (Nginx, IIS possible); the network flows separate the Internet, the reverse proxy, the website and the gateway.
- Roadmap 0.10.0: the gateway authenticates the website, counts the rate limit per player, receives the IP address of the player from the website only, and the storage of the session token in the website is decided.
- Observability: `HostExitCode` gives the exit code of any executable once its host has run (moved from the Very Back, which uses it too).
- Platform.Infrastructure: `VeryBackClient.PingAsync` returns the time of the database server itself, so that no message type of the Very Back reaches the Back.
- Back and Very Back: the startup log is written once the listener is bound.
- Tests: the network tests use fixed ports of the block 17000-17999 (one block of 100 ports per test project, locked for its run, moved with `BOARDGAMESPLATFORM_TEST_PORT_BASE`), never a port handed out by the system; the Back and the Very Back are also started with their delivered configuration, to prove they listen on 7821 and 7822. The rule is written in the roadmap and in `test/README.md`.
- Website: the Interactive Server render mode is set for the whole application (`Routes` and `HeadOutlet`), as decided for 0.3.0.
- Settings of the Very Back client: `VeryBackClientOptions.FindProblem()` returns the first problem (or null) and the startup validator of the Back uses it,
  with no exception used for control flow; `Validate()` remains for the constructor of the client. Same rules, same messages.
- Protocol: the hardened accept loop of the Very Back moves to `LinkServer` (`BoardGamesPlatform.Protocol.Links`), and the connection management of the Back's Very Back client moves to `LinkClient`, so that the Back and the Gateway reuse them instead of a copy per tier. No change of behavior (the tests of the Very Back and of the client run unchanged), except two texts: the stop of a listener is logged as "Listener of the Very Back stopped", and a call after the disposal says "The client of the Very Back has been disposed". The events of these classes take the ids 9001-9005 and 9011-9018, reserved for them.
- TestKit: the logger that keeps the messages and the listener whose accepts fail on demand move there from the Very Back tests; the Protocol tests check the link server and the link client directly, under another tier name.

### Removed

- `BoardGamesPlatform.Web.Client` (WebAssembly project of the template) and the WebAssembly packages: the website runs in the Interactive Server render mode only.

### Fixed

- The Very Back Ping test on a real SQL Server now refuses a database that is not a `TEST_` or `DEV_` one, like the other SQL tests (it never checked the name).
- `tools/changelog/Merge-ChangelogFragments.ps1` no longer loses lines of `CHANGELOG.md` while reporting success: the summary under `[Unreleased]` and the continuation lines of its entries are kept; the last or first lines are no longer repeated when `[Unreleased]` ends or starts the file; an unknown heading or a paragraph inside `[Unreleased]` stops the script before it writes; an indented line that follows no entry of its own fragment, or a blank line inside an entry, stop it too; a fragment that cannot be deleted stops it with an error, so that it is not merged twice; the changelog is written through a temporary file, and a failed write is reported; a fragment without any entry gives a warning.
- Website: the texts of the Blazor template (error page with its "Request ID" label, not found page, error banner of the layout, reconnection dialog, title of the home page) now come from the resources (`WebTexts.resx` and `WebTexts.fr.resx`) instead of being written in the pages; the `lang` attribute of the document follows the language of the request. The script of the reconnection dialog writes no text: the texts are in the markup, rendered from the resources.
- Website: the error page no longer shows the template advice about the Development environment.
- Website: the not found page has a title and a level 1 heading (was a level 3 heading), so the router focuses it.
- Very Back: no SQL session stays open on SQL Server after a stop or a redeployment: the pool of its connection string is cleared once the listener has stopped.
- Gateway tests: the checks that an invalid setting stops the startup build the host directly instead of going through `WebApplicationFactory`, which could report a disposed service provider instead of the validation failure on the CI runner.
- CI, changelog check: the promotions `release/*` → `dev` and `dev` → `main` may carry `CHANGELOG.md`, finalized on the release branch; they were refused like any other pull request.

## [0.2.0] - 2026-10-29

Solution skeleton and test infrastructure: every tier starts and logs its startup, and the CI enforces the architecture. No game code yet.

### Added

- Roadmap to v1.0.0 (`doc/analyse/roadmap.md`): scope, versions, test strategy, network flows, risks and decisions, including internationalization and the arcade neon design system.
- README preview: mock-ups of the home page and of a Battleships match.
- Coding conventions for C# and SQL: file headers (legal author, no version history: Git keeps it), regions, intent-first documentation, code rules, SQL object headers and naming.
- Commit scopes (`release`, project names), `chore/` branches for repository maintenance, and branch protection rules in the contribution guide.
- Solution layout: 15 projects (shared libraries, Blazor Auto website, Gateway, Back platform modules and host, Very Back, SQL project), grouped by tier in solution folders; every host starts and logs its startup.
- Observability: the Back, the Very Back, the Gateway and the website write their logs to the system log (Windows Event Log or systemd journal), and can send their logs, traces and metrics to an OpenTelemetry collector configured in their settings; nothing is sent without one (`doc/adm/observability.md`).
- Configuration and secrets conventions (`doc/adm/configuration.md`): default .NET configuration sources, no secret in a committed file, user secrets in development, service environment variables on the servers until the vault of 0.7.0.
- Test infrastructure: xUnit v3 on Microsoft.Testing.Platform (selected in `global.json`), Shouldly, coverage and TRX extensions shared by every `*.Tests` project; a TestKit library (repository paths, fake clock).
- Architecture tests: project references, dependencies between layers (ArchUnitNET), committed settings that hold no secret value, connection strings declared by the Very Back only.
- CI solution filter that builds every .NET project and leaves out the SQL project.
- CI checks: `dotnet format --verify-no-changes` for the `.editorconfig` rules the build does not enforce (line endings, naming); the run fails when the CI solution filter no longer matches the solution, or when a test project runs no test.
- CI reports: every run publishes the TRX and Cobertura coverage reports of the test projects; the coverage leaves out the test tooling (`test/CodeCoverage.config`).

### Changed

- Code style rules aligned with the conventions: explicit types instead of `var`, explicit object creation, braces optional for single-line statements, block-bodied members, classic constructors; SQL files indented with tabs.
- Roadmap: a TestKit helper is added by the version that first needs it; the seeded random source moves to 0.13.0, and network tests bind port 0 instead of reserving a free port in advance (0.3.0).
- The solution is renamed from SocialGamesPlatform to BoardGamesPlatform (projects, namespaces, documentation).

## [0.1.0] - 2026-10-29

Repository bootstrap: no game code yet.

### Added

- Solution baseline: .NET 10 SDK, central package management, Roslynator analyzers, build-time code style.
- Repository layout: `src/`, `test/` and `doc/` (analysis, developer, user and administrator documentation).
- Doxygen configuration generating the developer documentation from the C# XML comments.
- Bilingual README (English and French) and PolyForm Strict 1.0.0 license.
- Contribution guide describing the branching model, commit and pull request conventions, versioning and releases.
- Continuous integration workflow building and testing every pull request.

[Unreleased]: https://github.com/micdef/BoardGamesPlatform/compare/v0.6.1...HEAD
[0.6.1]: https://github.com/micdef/BoardGamesPlatform/compare/v0.6.0...v0.6.1
[0.6.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.5.0...v0.6.0
[0.5.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/micdef/BoardGamesPlatform/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/micdef/BoardGamesPlatform/releases/tag/v0.1.0
