# Secure channel: authenticated ECDH handshake

This document specifies the secure channel of the Back → Very Back link (port 7822), delivered in 0.6.0, and records its threat model.
It is written **before the code**, and was reviewed by the security reviewer before any line of it: the roadmap names a design error in the handshake as a risk of the project.

The channel runs **inside** the mutual TLS 1.3 session of the link ([protocol.md](protocol.md#transport-security-mutual-tls-13)) and **under** the socket protocol: the `hello` of the protocol and every frame after it travel in the channel.
The Gateway → Back link keeps mutual TLS only.

## Why a second layer

TLS proves that a peer holds a certificate of the platform authority for its role.
The secure channel proves that it also holds a **signing key pinned by the other tier**, which no authority issues.
A valid certificate alone never opens the link: a stolen PFX of the Back, or a certificate issued by mistake or by a compromised authority, is not enough to read or send a request to the Very Back, which holds the database.

## Layers

| Order | Layer | Role |
|-------|-------|------|
| 1 | TCP | The byte stream |
| 2 | Mutual TLS 1.3 | Certificates of the roles, encryption of the session |
| 3 | **Secure channel** | Pinned signing keys, ephemeral ECDH, AES-GCM records |
| 4 | Socket protocol | Frames, handshake (`hello`), control messages, requests |

The channel is a decorator of the transport (`ITransportListener`, `ITransportConnector`, `ITransportConnection`):

- on the server, `EstablishAsync` runs the TLS handshake, then the handshake of the channel, in the task of the connection. The handshake of the channel therefore takes the same place of the handshakes (4, 2 per address) and the same handshake timeout (5 seconds for TLS, the channel and the `hello`) as the rest of the opening of a connection;
- on the client, `ConnectAsync` runs both handshakes, within the connect timeout of the link;
- the connection it hands over gives a byte stream, like TLS: nothing changes above it.

There is no setting to turn the channel off: on the Back → Very Back link it is always on, so that no configuration error can remove it (fail closed).
The points the tests use to fix the ephemeral keys, the nonces or a smaller limit of records are `internal`, never reachable from the settings.

## Prerequisites

- **.NET 10.0.13 or later**, the latest patch recommended: 10.0.13 fixes CVE-2026-96957, an improper verification of a cryptographic signature in .NET (CVSS 8.8), whose component the public advisory does not name.
- **Linux**: OpenSSL 3, kept up to date by the distribution (`dnf upgrade`; check with `rpm -q openssl-libs`). Fedora backports its fixes without changing the version number, so a minimum version number alone would be misleading.
- **Windows**: Windows Server 2022 or Windows 11 (TLS 1.3), with the latest cumulative update.

## Algorithms

One suite, with no negotiation:

| Use | Algorithm | .NET primitive |
|-----|-----------|----------------|
| Key agreement | ECDH over NIST P-256, ephemeral keys, one pair per connection and end | `ECDiffieHellman`, `DeriveRawSecretAgreement` |
| Signature | ECDSA over NIST P-256 with SHA-256, signature in the fixed IEEE P1363 format (64 bytes, `r` then `s`) | `ECDsa.SignData`, `VerifyData` |
| Key derivation | HKDF with SHA-256 | `HKDF.Extract`, `HKDF.Expand` |
| Records | AES-256-GCM, 12-byte nonce, 16-byte tag | `AesGcm(key, 16)` |
| Hashes (transcript, `keyId`, certificates) | SHA-256 | `SHA256.HashData` |

The raw ECDH secret is never used as a key: it only goes into HKDF.
A future suite would take a new version number (the version byte below), never a negotiation inside version 1.

### Validation of the public values

- An ephemeral key (`ephC`, `ephS`) is the uncompressed point: the byte `0x04`, then `x` and `y` on 32 bytes each. Each end refuses another first byte, a coordinate not below the prime of the curve, a point not on the curve and the point at infinity (P-256 has a cofactor of 1, so a point on the curve is enough). The channel checks the equation of the curve itself, before the import: the documentation of .NET does not say that the import does it, on Windows (CNG) or on Linux (OpenSSL). The tests also show what the import does on both systems.
- A signature is refused when `r` or `s` is not in [1, n − 1].
- A public signing key, in the settings or a file, is imported, checked to be on the named curve P-256 (not explicit parameters), and exported again by .NET in one **normalized form** (DER SubjectPublicKeyInfo, named curve, uncompressed point). Its `keyId` is the SHA-256 of that normalized form, and every comparison (duplicates, own key, key of a certificate) uses it, so that another encoding of the same key changes nothing.

## Handshake

Three messages, the client first, in a fixed binary format: every field has a fixed size, so that nothing is parsed and nothing signed has two encodings.
Multi-byte numbers are big-endian.

| Message | Direction | Fields | Size |
|---------|-----------|--------|------|
| M1 | client → server | `magic` "BGPS" (4), `version` = 1 (1), `suite` = 1 (1), `nonceC` (32, random), `ephC` (65), `keyIdC` (32) | 135 |
| M2 | server → client | `magic` "BGPS" (4), `version` = 1 (1), `suite` = 1 (1), `nonceS` (32, random), `ephS` (65), `keyIdS` (32), `sigS` (64) | 199 |
| M3 | client → server | `sigC` (64) | 64 |

- `keyId` names which pinned key checks the signature of its sender, so that a key is replaced without a cut (see [Keys](#keys)).
- The server answers M2 only once M1 is whole and valid: right magic, version and suite, a valid point, a `keyIdC` in its list of pinned keys.

### Context of the TLS session

Each end also puts in the transcript what it sees of the TLS session below, without sending it: `certS` = SHA-256 of the certificate of the server, `certC` = SHA-256 of the certificate of the client: the DER of each **leaf** certificate as the TLS session of the connection used it: for its own end, the certificate of the context taken for that connection (`SslStream.LocalCertificate` is null on a TLS 1.3 client), for the peer, the one the session received (`RemoteCertificate`); never as read again from the settings, which a reload may have changed already.
When the session gives no certificate for one of the ends, the connection is refused (`noCertificate`); with mutual TLS and no resumption of sessions, it never should.
Both ends see the same two certificates when TLS is end to end; a party in the middle that ends the TLS sessions with valid certificates of its own makes them differ, and the signatures fail.
This binds the channel to the certificates of the session (as `tls-server-end-point` of RFC 5929, for both ends), not to the session itself (see [Out of scope](#out-of-scope-residual-risks)).

### What is signed

`T2` = SHA-256(`certS` ‖ `certC` ‖ M1 ‖ M2 without `sigS`), `T3` = SHA-256(`certS` ‖ `certC` ‖ M1 ‖ M2).

- `sigS` = signature by the server, with its signing key, of `"BGPS v1 back-veryback server signature"` ‖ `0x00` ‖ `T2`.
- `sigC` = signature by the client, with its signing key, of `"BGPS v1 back-veryback client signature"` ‖ `0x00` ‖ `T3`.

The labels name the version, the link and the role, so that a signature is never valid for another role, another link that would take the channel one day, or another version.
The client checks `sigS` with the pinned key named by `keyIdS`, before it sends M3; the server checks `sigC` with the pinned key named by `keyIdC`, before it reads any record.
Each end also refuses a pinned key of its peer that is the public key of the TLS certificate the peer presented: the two keys must stay distinct on both sides.
Any failure closes the connection, with nothing sent (see [Failures](#failures)).

### Key schedule

- `Z` = ECDH secret of the two ephemeral keys (32 bytes).
- `TH` = SHA-256(`certS` ‖ `certC` ‖ M1 ‖ M2 ‖ M3).
- `PRK` = HKDF-Extract(salt = `nonceC` ‖ `nonceS`, input = `Z`).
- Each key and IV = HKDF-Expand(`PRK`, info, length), with info = length (2 bytes) ‖ length of the label (1 byte) ‖ label ‖ length of `TH` (1 byte) ‖ `TH`, as the `HkdfLabel` of RFC 8446 (section 7.1):

| Label | Length | Use |
|-------|--------|-----|
| `"BGPS v1 back-veryback c2s key"` | 32 | Key of the records client → server |
| `"BGPS v1 back-veryback s2c key"` | 32 | Key of the records server → client |
| `"BGPS v1 back-veryback c2s iv"` | 12 | IV of the records client → server |
| `"BGPS v1 back-veryback s2c iv"` | 12 | IV of the records server → client |

The nonces are fresh on each side and inside the signed transcript, so no party can choose the salt (RFC 5869, section 3.4).
`Z`, `PRK` and the ephemeral private keys are erased (`CryptographicOperations.ZeroMemory`, disposal of the keys) once the four values are derived.

There is no explicit "finished" message: the authentication is explicit (the signatures), and the keys come from `TH`, so the first record each end receives (the `hello` of the protocol, then its `helloAck`) only decrypts with the right keys, which confirms them.
A failure of that **first** record has its own reason, `keyConfirmationFailed`, to tell a relay or a bug from a corruption later on.

## Records

After the handshake, each direction is a sequence of records:

| Field | Size | Content |
|-------|------|---------|
| `length` | 2 | Length of the plaintext, 0 to 16 384 |
| `ciphertext` | `length` | The plaintext encrypted with AES-256-GCM |
| `tag` | 16 | Authentication tag |

- **Nonce**: the IV of the direction XOR (four zero bytes ‖ the 64-bit sequence number of the record), as in TLS 1.3. The sequence number starts at 0 in each direction, grows by one per record, and is never sent: a record replayed, reordered or dropped fails its tag.
- **Associated data**: the two bytes of `length`, so that a changed length fails the tag.
- **Writes**: a write of zero bytes sends no record; a write of more than 16 384 bytes is cut into records.
- **Close**: a record of length 0 announces the end of the direction. `CloseSendAsync` writes it, then closes the sending side of TLS; disposing a connection writes it first too (within one second), so that a normal close is never taken for a truncation by the peer. The last sequence number of a direction is kept for it, so that a connection at its hard limit still ends cleanly. After a failure of a read, a cancellation included, the reads are broken for good, and after a failure of a write the writes are: the connection is closed, a broken read still sending the close record. A write that would pass the hard limit is refused before anything is written. A stream that ends without it is a truncation, not a clean close; any byte after it is a violation.
- **Size**: a frame of 64 KiB (the largest payload of the link) takes 5 records. Each end holds one buffer of 16 KiB per direction.
- **Hard limit**: a direction sends at most 2²⁴ records (about 16.7 million, up to 256 GiB), below the 2^24.5 full-size records that RFC 8446 (section 5.5) allows for AES-GCM with one key. A write beyond it fails, and the connection is closed; the receiving end also refuses a record beyond it.
- **Renewal before the limit**: the connection tells when it should be replaced, once either direction reached 2²³ records, or once it is 24 hours old. The link client then opens a new connection in the background (the callers keep using the old one meanwhile, and never wait for the renewal) and sends the next requests on it; the old one stops taking new requests (a request refused that way, never sent, is sent again on the new one), lets the requests in flight end, within the request timeout of the link, and is closed, so that no request fails because of the limit. A renewal refused by a key, a certificate or the protocol is logged as an error (9010), a failure of the network as a warning (9009). When the new connection cannot be opened, the old one stays in use until its hard limit. During the overlap, the old and the new connection each take a served place of the server (8 of them): one client never needs more than 2. Nothing is renewed inside a connection: a new connection makes new keys.
- **Age limit on the server**: the server closes a connection 48 hours old, in case a client never renews it.
- **Heartbeat**: the protocol above only receives bytes once their record is authenticated, so "any byte received counts" ([protocol.md](protocol.md#heartbeat)) becomes "any authenticated record counts", with no change in the code of the heartbeat: a party in the middle can no longer fake a sign of life.

What closes the connection: a tag that fails, a length above 16 384, a stream that ends inside a record or without the close record, a byte after the close record, a write beyond the hard limit, and the removal of the key of the peer (see [Reload](#reload-and-rotation)).

## Keys

Each end has **one signing key** of its own and a **list of the public keys of its peer**.

- **Signing key**: ECDSA P-256, in a PEM file of an encrypted PKCS#8 private key (AES-256-CBC, PBKDF2 with SHA-256, 600 000 iterations, as the keys of the authority). It is made on the machine of its tier, and never leaves it. Its password has 12 characters at least.
- **Public keys of the peer**: a text file, one public key per line, in Base64 of its DER SubjectPublicKeyInfo; blank lines and lines starting with `#` are ignored. 1 to 4 keys. **This file is a trust anchor**: whoever can write it can add a key, taken at the next check.
- **Rights of the files**: the account of the service reads both files and writes neither; only the administrators (Windows) or `root` (Linux) write them. On Windows, `icacls` as for the PFX ([certificates.md](../adm/certificates.md)); on Linux, `chown root:<group of the service>` and `chmod 640` for the key, `644` or `640` for the list.
- **Distinct from the certificates**: the signing key is never the key of the TLS certificate of the tier, and its password is never the one of the PFX; a tier refuses to start when one of them is.
- **Development keys**: made by the development script, they never serve on a server, as the development certificates.
- **Tool**: the authority tool gets a command `signing-key --role <role> --out <folder>`, run on the machine of the tier, which writes `<role>-signing.key` (the private key, readable by its owner only on Linux) and `<role>-signing.pub` (the normalized public key, one line), and shows the `keyId`. Its password comes from `BOARDGAMESPLATFORM_SIGNING_KEY_PASSWORD` or a hidden prompt, never from the command line. The development script makes the keys of the Back and the Very Back, and their files of public keys.

### Settings

| Setting | Meaning |
|---------|---------|
| `Handshake:SigningKeyPath` | The PEM file of the signing key |
| `Handshake:SigningKeyPassword` | Its password, a secret: `Handshake__SigningKeyPassword` in the environment of the service, distinct from the password of the PFX, until the vault (0.7.0) |
| `Handshake:PeerPublicKeysPath` | The file of the public keys of the peer |

A relative path is taken from the folder of the tier, as for the `Tls` settings.

### Checks at load

A tier refuses to start (fail closed), naming the setting at fault, when:

- the signing key cannot be read with its password, has no private part, or is not on P-256;
- the file of the public keys of the peer is missing or empty, holds a line that is not a P-256 public key, a key twice (in normalized form), or more than 4 keys;
- that file holds the tier's own public key (a reflected key);
- the signing key is the key of the TLS certificate of the tier, or its password is the one of the PFX.

The same checks run at each reload, and the check against the TLS certificate runs again when the certificate is reloaded.
At startup, the tier logs its own `keyId` and the ones of its peer.

### Reload and rotation

The key files are taken again **cold**, at a restart, and **hot**, while the tier runs:

- before each new connection, and every minute (the files are small), a tier checks whether its signing key or its file of public keys changed: time of the last write **and** SHA-256 of the content, so that a copy that keeps the time is still seen. If so, it loads and checks them again, with the checks at load, and uses them for the new connections;
- a change of the list is logged as a warning, with the `keyId` added and removed;
- files that fail the checks are logged as errors, again every 5 minutes while they stay refused (with the time since the last keys taken), and the current keys are kept until the files change again; files that cannot be read yet, or are missing, are tried again at the next check. A missing file never removes a key: to revoke one, remove its line, never the file;
- each connection takes **one snapshot** (signing key and list) before its handshake; a key replaced is never disposed while a connection may still use it;
- the connections whose peer used a key **removed** from the list are closed at once: removing a key revokes it, established connections included. The tier that removed the key sends the close record first (the keys of the session are still valid), then closes the connection after 5 seconds at most; it only cuts it at once when that fails. A connection is registered before the end of its handshake, and the handshake checks, as its last step, that the key of the peer is still in the **current** list (by the generation of the list), so that a reload that happens during a handshake never misses it;
- the password is read at startup only: a new signing key taken hot must have the same password, or the tier is restarted.

Replace a file **atomically**: write the new content to a temporary file in the same folder, then move it over the old one (`mv` on Linux, `Move-Item -Force` on Windows), so that a tier never reads a file half written.
A move keeps the owner, the mode, the access list and the SELinux context **of the temporary file**: create it with the right ones first (`install -o root -g <group of the service> -m 640` on Linux, then `restorecon` after the move; the rights of the folder on Windows), so that the file moved over has the rights it must have.

Rotation of the key of a tier, with no cut of the link:

1. make the new key on the tier (`signing-key`), with the same password as the current one;
2. add its public key to the file of the peer, which takes it at its next check;
3. replace the signing key of the tier, which takes it at its next check;
4. once the tier uses the new key (its log), remove the old public key from the file of the peer, which closes the connections still opened with it; the link client opens new ones.

Each step can also be done with a restart instead of waiting for the check.

## Failures

A failed handshake or record closes the connection. **Nothing is sent to the peer**: a refusal that is not authenticated could be forged, and it would tell an attacker which check failed.
For the caller of the link, it is `link.unavailable`, like any peer that cannot be reached.

The reason is logged on the tier, from a closed list; nothing of the peer is logged but the fixed-size values the protocol defines (a `keyId`, in hexadecimal):

| Reason | Meaning | Level |
|--------|---------|-------|
| `malformed` | A message of the handshake has a wrong magic, size, point or signature value | Warning |
| `unsupportedVersion` | Another version or suite | Warning |
| `unknownKey` | A `keyId` that is not pinned (logged, useful during a rotation) | Warning |
| `peerKeyIsCertificateKey` | The pinned key named is the key of the TLS certificate of the peer | Error |
| `badSignature` | A signature that does not verify | Warning |
| `keyConfirmationFailed` | The first record does not decrypt | Error |
| `recordAuthenticationFailed` | A later record whose tag fails, a truncation, a byte after the close | Error |
| `recordLimitReached` | The hard limit of records of a direction | Warning |
| `peerKeyRemoved` | The key of the peer was removed from the list | Information |
| `noCertificate` | The TLS session gives no certificate for one of the ends | Error |
| `peerClosed` | The peer closed the connection during the handshake | Warning |

A handshake that lasts beyond the handshake timeout is canceled by the link server or the link client, and logged by them as a timeout, as for TLS.

The failed handshakes are logged once every 10 seconds at most (`LogPace`, the next entry giving the number held back), so that a peer that fails in a loop cannot flood the logs.
The events take the range 9051-9069 of `Protocol.Transport.Secure`:

| Events | What |
|--------|------|
| 9051-9053 | Handshakes refused by the server, by the client, and opened (with the `keyId` of the peer) |
| 9054-9055 | Records: refused (tag, truncation, byte after the close), hard limit reached |
| 9056 | Reserved; the renewal itself is logged by the link client (9008 renewed, 9009 renewal failed, 9010 renewal refused, the current connection kept; 9020, Debug, a request sent again on the new connection) |
| 9057-9059 | Keys: loaded at startup (own `keyId` and the ones of the peer), list changed (added and removed), signing key taken |
| 9060-9062 | Keys: changed files refused, connection closed for a removed key, connection closed for its age |
| 9063-9065 | A removed key not told to the connections at once (the periodic check closes them), a close record that could not be sent, a connection not closed after a refused handshake |
| 9066-9069 | A connection not closed cleanly (its socket released), a periodic check of the connections that failed, the close record of a normal close not sent (a broken connection, or a peer gone or that does not read; Debug), the keys of a refused handshake not erased (Debug) |

## Threat model

### Assets

- The requests and responses between the Back and the Very Back, which reach the database.
- The signing keys of both tiers, their passwords, and the files of pinned public keys.

### Assumptions

- Mutual TLS 1.3 is in place on the link (0.5.0); the channel adds to it, it never replaces it.
- The machine of a tier, its operating system and the .NET runtime are not compromised, and only its administrators can write its key files.
- The cryptography of the system (CNG on Windows, OpenSSL 3 on Linux) is correct.

### In scope

| Threat | Protection |
|--------|------------|
| A party that holds a valid TLS certificate of the role (stolen PFX, certificate issued by mistake, compromised authority) but not the signing key | It cannot produce the signature of M2 or M3: the handshake fails (done criterion of 0.6.0) |
| A party in the middle that relays the TLS sessions | It sees only records; replacing an ephemeral key breaks the signature |
| A party in the middle that ends the TLS sessions with valid certificates of its own | The hashes of the certificates in the transcript differ on the two ends: the signatures fail |
| Replay of a handshake | Each end brings a fresh nonce and ephemeral key, both inside the signed transcript: a replayed M1 or M3 does not match the new M2 |
| Replay, reorder, drop or truncation of records | Implicit sequence number in the nonce, close record |
| Tampering with a record | AES-GCM tag over the length and the content; the connection is closed at the first failure, so no forgery is tried twice |
| Downgrade (older version, other suite, removal of the channel) | One version and one suite, both signed; no fallback; no setting to turn the channel off; a peer without the channel is refused at the magic |
| Reflection (a signature sent back to its author) | Distinct labels for the two roles; a key per direction; a tier refuses its own key in the list of its peer |
| Unknown key share, identity misbinding | Both `keyId` are in the signed transcript, with both ephemeral keys and nonces (the signed variant of SIGMA) |
| Key compromise impersonation | Not possible with signatures: the stolen key of the client does not let anyone pass for the server |
| Invalid points, malleable signatures | Points checked on both sides; `r` and `s` in range; the transcript holds the signatures as sent, so a changed signature only makes the handshake fail |
| Another encoding of a pinned key | Every key normalized before its `keyId` and its comparisons |
| A signing key that is also the TLS key | Refused at load on the tier, and on the peer |
| A revoked key used by an established connection | Removing the key closes the connections that used it |
| A connection that reaches the limit of records | Renewed before the limit, without failing a request |
| A file of keys written by someone else than an administrator | Rights of the files; each change of the list logged with the `keyId` added and removed |
| Mistakes during a rotation | Checks at load, current keys kept on a failure, the unknown `keyId` logged, atomic replacement |
| Flood of failed handshakes | The places and timeout of the handshakes of 0.5.0; only a peer that passed mutual TLS reaches the channel; paced logs |
| A peer that only holds a certificate and sends M1 to learn things | It gets `sigS` over a transcript with a fresh `nonceS` and `ephS`, useless elsewhere, and learns `keyIdS` (public) and that the server runs: nothing that helps it finish a handshake |

### Out of scope, residual risks

- **A compromised machine of a tier**: its signing key and the password of it sit on the same machine, until the vault of 0.7.0. The channel protects against a stolen PFX or a compromised authority, not against an attacker who controls the tier.
- **A stolen signing key**: the thief can pass for the tier until its key is removed from the file of the peer, which then also closes its connections within a minute (at once with a restart). Past sessions stay protected (forward secrecy: the ephemeral keys are erased; whether OpenSSL clears their memory at disposal is not verified).
- **No binding to the TLS session itself**: no exporter of keying material was found for `SslStream` in .NET 10 (not proven absent), so the channel binds to the certificates of the session, not to its keys. Its keys are its own, and the binding to the certificates detects a party in the middle that ends TLS; a binding to the session can come when .NET offers an exporter.
- **Denial of service** before TLS: the role of the firewall and of the limits of 0.5.0.
- **Traffic analysis**: the sizes and the timing of the records are visible to a party in the middle of TLS (no padding).
- **Side channels** of the cryptography of the system, and **post-quantum** attackers.
- **The Gateway → Back link**, the identity of the players (0.10.0) and several instances of a tier (1.1).

## Tests

- **Done criterion**: a proxy with valid certificates of both roles, made at run time. With signing keys of its own, the handshake fails on both sides. Ending the TLS sessions with its own valid certificates but relaying the channel, the signatures fail. As a pure relay of TLS, the link works, and the bytes of the `hello` appear nowhere in what it captured. Replacing an ephemeral key breaks the signature.
- **Replay**: M1, M2 and M3 replayed from another connection; records duplicated, reordered or dropped.
- **Downgrade**: changed version or suite bytes; a TLS-only client against the server; a server without the channel against the client.
- **Reflection and unknown key share**: `sigS` sent back as M3; swapped `keyId`.
- **Public values**: invalid points on each side (other first byte, coordinate out of range, point off the curve), `r` or `s` out of range, another encoding of a pinned key; run on Windows and on Fedora.
- **Tampering**: bits flipped in the length, the ciphertext and the tag; a truncated stream; a length above 16 384; a stream that ends without the close record; a byte after the close record; a write of zero bytes; the first record broken (`keyConfirmationFailed`).
- **Limits**: a hard limit set small for the test, on the sender and on the receiver; the renewal threshold, a request in flight on the old connection still answered; a new connection that cannot be opened, the old one kept; the age limit of the server.
- **Revocation**: a removed key closes the connections that used it, with a close record first; a reload during a handshake (snapshot taken before, list changed before its end) still closes it.
- **Startup and reload**: each wrong setting or file refused, naming it; a changed key taken by the next connection; a file copied with its old time still seen; a broken file keeps the current keys; a removed key closes the connections that used it; the signing key equal to the TLS key, on the tier and on the peer.
- **Properties and fuzzing**: round trips with writes cut at random; random and mutated handshake messages, with fixed seeds.
- **Timeout**: a peer that stays silent in the handshake of the channel, or a client that never sends M3, is closed within the handshake timeout.
- **Test vectors**: HKDF against the published vectors of RFC 5869; ECDH P-256, ECDSA, AES-GCM, the key schedule, the composition of the transcript and the first records against values computed by **another implementation** (OpenSSL or the Python `cryptography` package, in a virtual environment), never by the code under test. The ECDSA signatures of .NET are randomized, so the vectors of signatures are vectors of verification (key, message, signature).
