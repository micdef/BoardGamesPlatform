# Socket protocol

This document specifies the protocol of the two links between the tiers: Gateway → Back (port 7821) and Back → Very Back (port 7822).
It describes version 1 of the protocol, delivered in 0.4.0. The code is in `src/BoardGamesPlatform.Protocol`; its XML comments give the details of each class.

**Security limits of this version**

- From 0.5.0 both links run over mutual TLS 1.3 (see [Transport security](#transport-security-mutual-tls-13) and `doc/adm/certificates.md`): each end presents the certificate of its role, issued by the authority of the platform, and only the expected role is accepted. TLS wraps the byte stream described here and changes nothing above it.
- TLS identifies the **tiers, not the players**: the requests carry no session before 0.10.0, so a tier that holds a valid certificate of its role can call every operation of its peer.
- A process that can reach the port but holds no certificate can still open connections: it gets one of the 4 places of the handshakes (2 per IP address) for 5 seconds at most, never the place of a served connection, and a newer connection makes room by closing the oldest one. A peer that opens connections without pause, from 2 addresses or more, can still keep the client tier from connecting: keep the listeners on the loopback address or behind a firewall that admits the peer tier only. Behind a NAT or a published Docker port, all the peers may show the same address, which makes the part per address the whole of the places.
- The largest payload (64 KiB) is part of the contract of each link, checked by the handshake: a change of it is deployed on both ends at the same time, since a client and a server that differ refuse each other (`protocol.limitsMismatch`).
- A withdrawn certificate is refused through `Tls:RefusedThumbprints` only: there is no revocation list nor OCSP. The vault for the passwords of the certificates and of the signing keys comes in 0.7.0.
- From 0.6.0 the Back → Very Back link adds the secure channel inside TLS ([secure-channel.md](secure-channel.md)): an authenticated ECDH handshake with signing keys pinned by each tier, then AES-256-GCM records, so that a valid certificate alone never reaches the Very Back. The Gateway → Back link keeps mutual TLS only.

## Layers

| Layer | Namespace | Role |
|-------|-----------|------|
| Transport | `Protocol.Transport` | A connection hands over a byte stream: plain TCP, mutual TLS 1.3 over TCP (`Protocol.Transport.Tls`), and on the Back → Very Back link the secure channel inside TLS (`Protocol.Transport.Secure`, [secure-channel.md](secure-channel.md)) |
| Framing | `Protocol.Framing` | Cuts the stream into frames and refuses an oversized frame before reading it |
| Control | `Protocol.Control` | Handshake, heartbeat and cancellation: the frames that manage the connection itself |
| Correlation | `Protocol.Correlation` | Several requests in flight on one connection, each response paired with its request |
| Messages | `Protocol.Messages` | JSON envelopes of the requests and the responses, routed by operation name |
| Errors | `Protocol.Errors` | Error codes and the exceptions that carry them |
| Links | `Protocol.Links` | What a tier uses: the link client (connection, reconnection, timeouts) and the link server (accept loop) |

## Transport security: mutual TLS 1.3

Every byte of a link travels in a TLS 1.3 session, and both ends prove who they are with a certificate of the platform authority (`src/BoardGamesPlatform.CertificateAuthority`).

- **Trust**: a tier trusts the roots of the platform only, never the trust store of the system. Several roots may be trusted at the same time, during a change of root.
- **Identity**: the certificate of a tier holds one DNS name, `<role>.<suffix>` (`gateway`, `back` or `veryback`, then the suffix of the deployment, for example `back.bgp.internal`), and the usages of its sides: TLS server for the Back and the Very Back, TLS client for the Gateway and the Back. The name never depends on the address connected to.
- **Who accepts whom**: the Back accepts the Gateway only, the Very Back the Back only; the Gateway talks to the Back only, the Back to the Very Back only. A valid certificate of another role is refused: the key of one tier never opens a link meant for another one.
- **Check of a certificate**: it chains to a trusted root, every certificate of the chain is valid now and allowed for the usage of its side, its only DNS name is the one of the role expected, and its SHA-256 thumbprint is not in the refused list. No revocation is checked and nothing is downloaded: the links run in a closed network, and a compromised certificate is refused by its thumbprint.
- **Handshake**: TLS 1.3 only (an older version is refused), no renegotiation, and no session resumption, so that every connection checks the certificates again. On the server, the TLS handshake runs in the task of the connection, never in the accept loop, within the same handshake timeout as the protocol handshake below (5 seconds for both): a silent peer never holds the accept loop.
- **Refusal**: a refused peer is logged with the reason and the thumbprint of its certificate, never with a text the peer chose; the failed handshakes are logged once every 10 seconds at most, the next entry giving the number held back, and the link server only traces them (Debug), so that a peer that fails handshakes in a loop cannot flood the logs. A connection is logged as accepted once it is established. For the caller of a link, a refused peer is `link.unavailable`, like a peer that cannot be reached. In TLS 1.3, the server may refuse the certificate of the client after the client ended its handshake: the client then sees the refusal at its first read, as a failure of the connection.
- **Renewal**: before each new connection, and every hour, a tier checks whether its PFX or the file of its roots changed. If so, it loads and checks them again, and uses them for the new connections; files that fail the check are logged, and the current certificate is kept until the files change again (files that cannot be read yet, a copy in progress, are tried again at the next check). An established connection keeps the certificate it started with. The hourly check also warns, once a day at most, when the certificate expires within 30 days, and logs an error once it expired, so that a tier whose links stay open still warns.
- **Prerequisite**: TLS 1.3 needs Windows Server 2022 or Windows 11 or later (Schannel; a tier refuses to start on an older Windows), or OpenSSL 3 on Linux (OpenSSL 1.1.1, the first with TLS 1.3, is no longer supported by its project).
- **Usages**: a certificate must carry its extended usages; one without them is refused, though a chain alone would accept it for any usage.
- **Closing the sending side** announces the end of the session (close_notify), then sends the end of the TCP stream; the other end reads a clean end and may still answer.

## Frames

Every message is a frame: a 12-byte header, then the payload. All numbers are big-endian (network byte order).

| Bytes | Content |
|-------|---------|
| 0 to 3 | Length of the payload, unsigned 32-bit integer |
| 4 to 11 | Correlation identifier, unsigned 64-bit integer |
| 12 and after | Payload, exactly as many bytes as announced: UTF-8 JSON |

- The largest payload is 1 MiB by default (16 MiB at most); each link sets its own, used by both ends: 64 KiB for Gateway → Back (`BackOperations.LinkMaxPayloadLength`) and for Back → Very Back (`VeryBackOperations.LinkMaxPayloadLength`), sized to their operations of a few hundred bytes, so that the memory a peer can make a server hold stays small (connections × requests in progress × largest payload). A frame that announces more is refused before its payload is read.
- Correlation identifier **0** is reserved for the control messages. A request uses 1, 2, 3… in the order the client gives them.
- **Connection rule**: after any failure of a read or a write, cancellation included, the position in the stream is unknown, so the connection is closed, never reused.

## Opening a connection: the handshake

The client speaks first. Both messages are control messages, on identifier 0. No request is accepted before the handshake ends.

1. The client sends `{"control":"hello","minVersion":1,"maxVersion":1,"maxPayloadLength":1048576}`.
2. The server chooses the newest version that both ends speak and answers `{"control":"helloAck","version":1,"maxPayloadLength":1048576,"maxConcurrentRequests":64}`.
3. Or it refuses with `{"control":"helloRefused","code":"…"}`, then closes:
   - `protocol.versionUnsupported`: no common version;
   - `protocol.limitsMismatch`: both ends are not configured with the same largest payload.

The client checks the answer again: a version outside its range, another largest payload or a limit below one is a protocol violation.
The server gives a client 5 seconds for the whole handshake. The client bounds it with its connect timeout (`Back:ConnectTimeout` in the Gateway and `VeryBack:ConnectTimeout` in the Back, 5 seconds by default).
The version is agreed once per connection: no message carries a version field.

## Control messages

A control message is the payload of a frame with identifier 0: a JSON object whose **first** property, `control`, names its kind. It is read strictly. An unknown kind, a missing property or an unknown property is a protocol violation, and the connection is closed.

| Kind | Sent by | When | Content |
|------|---------|------|---------|
| `hello` | client | first frame of the connection | `minVersion`, `maxVersion`, `maxPayloadLength` |
| `helloAck` | server | answer to the hello | `version`, `maxPayloadLength`, `maxConcurrentRequests` |
| `helloRefused` | server | answer to the hello, then close | `code` |
| `cancel` | client | after the handshake | `correlationId` of the request to cancel |
| `ping` | client | after the handshake | none |
| `pong` | server | answer to each ping | none |

Any other combination (a hello after the handshake, a ping from the server, a cancel from the server) is a protocol violation.

## Requests and responses

A request is `{"operation":"<name>","body":{…}}`. The name is 1 to 64 characters among `A-Z a-z 0-9 . _ -`, matched exactly (case-sensitive). The writer checks the name, and that the body is written as a JSON object: a request the reader would refuse is a bug of its caller, refused with an `ArgumentException` before anything is written.

A response holds **exactly one** part:

- `{"body":{…}}`: the answer of the operation;
- `{"error":{"code":"…"}}`: why there is no answer.

The JSON is UTF-8 and camelCase. It is read strictly: an unknown or duplicated property, a property in another case, a number written as a string, a date without its offset, and nesting deeper than 16 levels are refused. An error message never copies text from the peer (a property name or a value), so that a peer cannot write lines or control characters into the logs.

Responses may arrive in any order: the correlation identifier pairs each one with its request.

## Error codes

A code is a short dotted name, never a display text and never an internal detail. The fronts translate the codes. The details of a failure stay in the logs of the tier where it happened.

| Code | Meaning | Connection |
|------|---------|------------|
| `request.unknownOperation` | The operation is unknown to the peer (two tiers of different versions, for example) | kept |
| `request.invalid` | The body does not match its operation | kept |
| `internal` | The peer failed while handling the request, could not serialize its response, or produced one larger than the link can carry | kept |
| `link.unavailable` | A tier the peer needs cannot be reached (or its back-off delay is running), or its connection was lost before the response | kept |
| `link.timeout` | A tier the peer needs did not answer within the request timeout (the wait for the connection included); the request was canceled there | kept |
| `server.busy` | The connection already has as many requests in progress as the server allows; the request was not started | kept |
| `protocol.versionUnsupported` | No common protocol version (handshake) | closed |
| `protocol.limitsMismatch` | Different largest payloads (handshake) | closed |

A tier passes on the code of a failed link: when the Very Back cannot be reached, the Back answers the Gateway `link.unavailable`. A code answered by a tier behind is not passed on, except `server.busy`: any other one becomes `internal`, since it could tell how the tiers are built.

**What closes the connection instead of a code**: a malformed frame, a malformed request envelope, a malformed or unexpected control message, a request before the hello, a request whose identifier is still in progress, a response to an identifier that was never sent, and a failed write on the stream. In each of these cases, the stream can no longer be trusted.

## Cancellation

When the caller of a request gives up (its token is canceled, or the request timeout elapses), the client:

1. stops waiting at once, and forgets the request: a late response is ignored;
2. once the request is written, sends `{"control":"cancel","correlationId":N}`, once.

The server cancels the token of that request only. A handler that ends with an exception is not answered: the operation dispatcher turns any exception into a cancellation once the token is canceled. A handler that still returns a response is answered, and the client ignores that response. A cancellation for a request that already ended, or never existed, is ignored.

When a client closes its sending side, the requests still in progress are answered, since it may still read. Those still running after the idle timeout of the server (45 seconds) are canceled, so that a connection never stays held. In that case only, the service ends with a `TimeoutException`, which the link server logs; when no request is left, the service simply ends. A handler must observe the token of its request: one that ignores it holds its place until it ends by itself.

Through the tiers, a front that disconnects stops the work of its call in the Back and in the Very Back, down to the procedure that calls the database, which passes the token to the SQL client. This is proven end to end by `AFrontThatLeavesStopsTheCallInTheVeryBackAsync`, with a fake procedure in place of SQL Server.

## Heartbeat

| Timing | Value |
|--------|-------|
| The client sends a `ping` every | 10 seconds |
| The client closes when the server has shown no sign of life for more than | 30 seconds, checked at each ping (so within 40 seconds) |
| The server closes when it has received nothing at all for more than | 45 seconds, checked at every ping interval (so within 55 seconds) |

Any byte received counts, not only a whole frame: a large frame on a slow link is not taken for a silent peer. On the client, a piece of a large frame written counts too (a frame larger than 64 KiB is written in pieces of 64 KiB, which leave only when the server reads, once the buffers of the system are full): a large request on a slow link is not taken for a dead server. A short frame written is never a sign of life, since the system takes its bytes without the server reading them: short requests sent often never keep a silent server alive. While a frame is being written, the client skips its ping instead of waiting behind it; if that write stalls, no sign of life comes and the client closes after its timeout. Each end watches with its own timer, and no read is ever canceled without closing the connection. The write of a ping is bounded by the client heartbeat timeout (30 seconds), so that a peer that no longer reads cannot block the heartbeat. On the Back → Very Back link, the bytes reach the protocol only once their record of the secure channel is authenticated: there, any authenticated record counts, and a party in the middle cannot fake a sign of life.

## Limits

| Limit | Value | When reached |
|-------|-------|--------------|
| Largest payload of a frame | 64 KiB on the links Gateway → Back and Back → Very Back (1 MiB by default, 16 MiB at most) | the frame is refused before its payload is read, the connection closed; a hello with another largest payload is refused (`protocol.limitsMismatch`) |
| Requests in progress per connection | 64, announced in the handshake (the client never keeps more than 64, whatever the server announces) | the client waits for a free place; the server answers `server.busy` to a request that arrives anyway (for example just after a cancellation, before the server has seen it) |
| Connections served by a link server | 8 | a new connection is closed at once, before any byte; a connection whose handshake ends then is closed too |
| Connections in their handshake (TLS, then protocol), not yet served | 4, of which 2 at most from one source (an IP address) | the oldest connection in its handshake of the source (when it holds its 2), else of all, is closed to make room for the new one: a silent or unknown peer never takes the place of a served connection, and must open connections without pause to keep the client tier out (a port it reaches can still be saturated before authentication: the firewall stays the barrier) |
| Logs of the refused connections | one every 10 seconds at most; the next log gives the number of refusals in between | the refusals are counted, not logged one by one |
| Handshake | 5 seconds | the connection is closed |
| Write of a frame by the server | no progress for the idle timeout (45 seconds) | the connection is closed (`TimeoutException`, logged by the link server) |
| Request timeout, the wait for the connection included | `Back:RequestTimeout` (Gateway, 8 s) and `VeryBack:RequestTimeout` (Back, 5 s), at most 5 minutes | `link.timeout`, the request is canceled on the peer |
| Response of an operation | the largest payload of the link | `internal`, logged; the connection stays open |

The server never waits for a place to read, so that a cancellation or a ping is received at once, even when every place is taken; only a client that stops reading the answers written from the read loop (a pong, a `server.busy`) can stall it, until the idle timeout closes the connection. A write of the server that makes no progress (no piece of 64 KiB written) for the idle timeout closes the connection too, so that a client that stops reading while it sends frames that need no answer (cancellations) cannot hold the writes, the places and the responses of its connection; the deadline moves with each piece, so a large frame read slowly is not cut. It gives the place of a request back just before writing its response, so that the next request of the client always finds it free.

The request timeouts decrease along the chain (website 10 s > Gateway 8 s > Back 5 s), so that each tier still answers a code before its caller gives up.

## Reconnection

The link client shares one connection between all its callers.

- A lost connection is opened again by the next call, at once.
- When opening fails (the peer is down, refuses, breaks the handshake, or the request timeout cuts the opening), the next attempt waits a back-off delay: 0.5 second, doubled after each failure, spread at random by 20 % either way, and never more than 30 seconds. It is counted on a monotonic clock, so that a change of the system time does not change it.
- During the delay, calls fail at once with `link.unavailable`, without touching the network.
- A connection that opens lets the next attempt go at once, but the failures in a row are only forgotten once it answers a request (an error code of the peer included). A connection lost before any response counts as a failure to open: a peer in a crash loop (it accepts the handshake, then dies at the first request) is reconnected with the back-off, not on every call.
- A request in flight on a lost connection fails with `link.unavailable`. No request is ever retried automatically, since the peer may already have handled it (idempotency comes with 0.14.0).
- Renewal (from 0.6.0, the secure channel): a connection that asks for it (2²³ records on a direction, or 24 hours) is replaced in the background, the callers never waiting for it: the new connection takes the next requests, and the old one takes no new request (one refused that way, never sent, is sent again on the new one) and is closed once its requests ended, within the request timeout, so that no request fails. When the new connection cannot be opened, the old one stays in use, and the renewal is tried again after one minute.

## Logs

The protocol logs through the logger of the tier that uses it, with event ids reserved in the [coding conventions](coding-conventions.md):

| Class | Event ids |
|-------|-----------|
| `LinkClient` | 9001-9010 |
| `LinkServer` | 9011-9019, 9040-9043 |
| `OperationDispatcher` | 9021-9026 |
| `TlsTransportListener` | 9031-9033 |
| `TlsTransportConnector` | 9034-9035 |
| `TlsCredentials` | 9036-9039 |
| Secure channel (`Protocol.Transport.Secure`) | 9051-9062, see [secure-channel.md](secure-channel.md#failures) |

On the client, the loss of a connection is logged with its reason (heartbeat timeout, protocol violation, reset by the peer). On the server, the end of a connection that failed is logged with its exception, for example the `TimeoutException` of a drain cut after a client closed.
