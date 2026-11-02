# Socket protocol

This document specifies the protocol of the two links between the tiers: Gateway → Back (port 7821) and Back → Very Back (port 7822).
It describes version 1 of the protocol, delivered in 0.4.0. The code is in `src/BoardGamesPlatform.Protocol`; its XML comments give the details of each class.

Until 0.5.0 the links are plain TCP, for development only: nothing is encrypted nor authenticated. TLS 1.3 with mutual authentication comes in 0.5.0, and an authenticated ECDH handshake on the Back → Very Back link in 0.6.0. Both wrap the byte stream described here; they change nothing above it.

## Layers

| Layer | Namespace | Role |
|-------|-----------|------|
| Transport | `Protocol.Transport` | A connection hands over a byte stream (TCP today, TLS from 0.5.0) |
| Framing | `Protocol.Framing` | Cuts the stream into frames and refuses an oversized frame before reading it |
| Control | `Protocol.Control` | Handshake, heartbeat and cancellation: the frames that manage the connection itself |
| Correlation | `Protocol.Correlation` | Several requests in flight on one connection, each response paired with its request |
| Messages | `Protocol.Messages` | JSON envelopes of the requests and the responses, routed by operation name |
| Errors | `Protocol.Errors` | Error codes and the exceptions that carry them |
| Links | `Protocol.Links` | What a tier uses: the link client (connection, reconnection, timeouts) and the link server (accept loop) |

## Frames

Every message is a frame: a 12-byte header, then the payload. All numbers are big-endian (network byte order).

| Bytes | Content |
|-------|---------|
| 0 to 3 | Length of the payload, unsigned 32-bit integer |
| 4 to 11 | Correlation identifier, unsigned 64-bit integer |
| 12 and after | Payload, exactly as many bytes as announced: UTF-8 JSON |

- The largest payload is 1 MiB by default (16 MiB at most). A frame that announces more is refused before its payload is read.
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
The server gives a client 5 seconds for the whole handshake. The client bounds it with its connect timeout (5 seconds by default).
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

A request is `{"operation":"<name>","body":{…}}`. The name is 1 to 64 characters among `A-Z a-z 0-9 . _ -`, matched exactly (case-sensitive). The writer checks both rules: a request the reader would refuse is a bug of its caller, refused before anything is written.

A response holds **exactly one** part:

- `{"body":{…}}`: the answer of the operation;
- `{"error":{"code":"…"}}`: why there is no answer.

The JSON is UTF-8 and camelCase. It is read strictly: an unknown or duplicated property, a property in another case, a number written as a string, a date without its offset, and nesting deeper than 16 levels are refused. The error never copies a text of the peer (a property name, a value), so that a peer cannot write lines or control characters into the logs.

Responses may arrive in any order: the correlation identifier pairs each one with its request.

## Error codes

A code is a short dotted name, never a display text and never an internal detail. The fronts translate the codes. The details of a failure stay in the logs of the tier where it happened.

| Code | Meaning | Connection |
|------|---------|------------|
| `request.unknownOperation` | The operation is unknown to the peer (two tiers of different versions, for example) | kept |
| `request.invalid` | The body does not match its operation | kept |
| `internal` | The peer failed while handling the request, could not write its response, or wrote one larger than the link can carry | kept |
| `link.unavailable` | A tier the peer needs cannot be reached, or its connection was lost before the response | kept |
| `link.timeout` | A tier the peer needs did not answer within the request timeout (the wait for the connection included); the request was canceled there | kept |
| `server.busy` | The connection already has as many requests in progress as the server allows; the request was not started | kept |
| `protocol.versionUnsupported` | No common protocol version (handshake) | closed |
| `protocol.limitsMismatch` | Different largest payloads (handshake) | closed |

A tier passes on the code of a failed link: when the Very Back cannot be reached, the Back answers the Gateway `link.unavailable`. A code answered by a tier behind is not passed on, except `server.busy`: any other one becomes `internal`, since it could tell how the tiers are built.

**What closes the connection instead of a code**: a malformed frame, a malformed request envelope, a malformed or unexpected control message, a request before the hello, a request whose identifier is still in progress, a response to an identifier that was never sent, and a failed write. In each of these cases, the stream can no longer be trusted.

## Cancellation

When the caller of a request gives up (its token is canceled, or the request timeout elapses), the client:

1. stops waiting at once, and forgets the request: a late response is ignored;
2. once the request is written, sends `{"control":"cancel","correlationId":N}`, once.

The server cancels the token of that request only and answers nothing, whatever the exception the handler ends with. A handler that finishes anyway is answered, and the client ignores the response. A cancellation for a request that already ended, or never existed, is ignored.

When a client closes its sending side, the requests still in progress are answered (it may still read), but those still running after the idle timeout of the server (45 seconds) are canceled, so that a connection never stays held.

Through the tiers, a front that disconnects stops the work of its call in the Back and in the Very Back, down to the call to the database.

## Heartbeat

| Timing | Value |
|--------|-------|
| The client sends a `ping` every | 10 seconds |
| The client closes when it has received no frame at all for | 30 seconds |
| The server closes when it has received no frame at all for | 45 seconds |

Any frame counts, a request or a response as well as a ping or a pong. Each end watches with its own timer, and no read is ever canceled without closing the connection. The write of a ping is bounded by the client timeout, so that a peer that no longer reads cannot block the heartbeat.

## Limits

| Limit | Value | When reached |
|-------|-------|--------------|
| Largest payload of a frame | 1 MiB (16 MiB at most) | the frame is refused, the connection closed |
| Requests in progress per connection | 64, announced in the handshake (the client never keeps more than 64, whatever the server announces) | the client waits for a place; a request beyond it is answered `server.busy` |
| Connections served by a link server | 8 | a new connection is closed at once, and a warning is logged |
| Handshake | 5 seconds | the connection is closed |
| Request timeout, the wait for the connection included | `Back:RequestTimeout` (Gateway, 8 s) and `VeryBack:RequestTimeout` (Back, 5 s), at most 5 minutes | `link.timeout`, the request is canceled on the peer |
| Response of an operation | the largest payload of the link | `internal`, logged; the connection stays open |

The server never waits for a place to read, so that a cancellation or a ping is received at once, even when every place is taken; only a client that stops reading the answers written from the read loop (a pong, a `server.busy`) can stall it, until the idle timeout closes the connection. It gives the place of a request back just before writing its response, so that the next request of the client always finds it free.

The request timeouts decrease along the chain (website 10 s > Gateway 8 s > Back 5 s), so that each tier still answers a code before its caller gives up.

## Reconnection

The link client shares one connection between all its callers.

- A lost connection is opened again by the next call, at once.
- When opening fails (the peer is down, refuses, or breaks the handshake), the next attempt waits a back-off delay: 0.5 second, doubled after each failure, spread at random by 20 % either way, and never more than 30 seconds. It is counted on a monotonic clock, so that a change of the system time does not change it.
- During the delay, calls fail at once with `link.unavailable`, without touching the network.
- The first connection that opens resets the delay.
- A request in flight on a lost connection fails with `link.unavailable`. No request is ever retried automatically, since the peer may already have handled it (idempotency comes with 0.14.0).

## Logs

The protocol logs through the logger of the tier that uses it, with event ids reserved in the [coding conventions](coding-conventions.md):

| Class | Event ids |
|-------|-----------|
| `LinkClient` | 9001-9006 |
| `LinkServer` | 9011-9019 |
| `OperationDispatcher` | 9021-9026 |

The loss of a connection is logged with its reason (heartbeat timeout, protocol violation, reset by the peer).
