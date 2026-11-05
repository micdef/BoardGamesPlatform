# Back

The Back holds all the logic of the platform. The Gateway connects to it; the Back connects to the Very Back. It never reaches the database itself.

## Settings

| Key | Default | Meaning |
|-----|---------|---------|
| `Back:ListenAddress` | `127.0.0.1` | IP address (IPv4 or IPv6) the Back listens on, for the Gateway. The loopback address keeps it unreachable from other machines. Another address is accepted, so that the tiers can run on separate machines or containers, but the Back logs a **warning** at startup: until 0.5.0 the link is neither encrypted nor authenticated, so restrict it with the firewall to the machine of the Gateway |
| `Back:Port` | `7821` | TCP port the Back listens on (Gateway to Back, see the network flows of the [roadmap](../analyse/roadmap.md)), from 1 to 65535 |
| `VeryBack:Host` | `127.0.0.1` | Host name or address of the Very Back |
| `VeryBack:Port` | `7822` | TCP port of the Very Back, from 1 to 65535 |
| `VeryBack:ConnectTimeout` | `00:00:05` | Longest time allowed to open a connection to the Very Back; greater than zero and at most 5 minutes |
| `VeryBack:RequestTimeout` | `00:00:05` | Longest time a request waits for the answer of the Very Back, the wait for the connection included; greater than zero and at most 5 minutes. When it elapses, the request is canceled in the Very Back and the Gateway is answered `link.timeout` |

The Back **refuses to start**, before it listens, and names the wrong setting, when one of these values is invalid.

As environment variables: `Back__ListenAddress`, `Back__Port`, `VeryBack__Host`, `VeryBack__Port`, `VeryBack__ConnectTimeout`, `VeryBack__RequestTimeout`.

The request timeouts must decrease along the chain, so that each tier still answers a code before its caller gives up: website `Gateway:RequestTimeout` (10 s) > Gateway `Back:RequestTimeout` (8 s) > Back `VeryBack:RequestTimeout` (5 s).

## Transport

In 0.3.0 and 0.4.0 both links of the Back are plain TCP, for development only. TLS 1.3 comes with 0.5.0, before any real use.

- **Handshake**: each connection opens with a handshake, before any request: the client offers the protocol versions it speaks and its largest frame, the server chooses the newest common version and answers its own limit of requests in progress. A client without a common version, or configured with another largest frame, is refused with the code `protocol.versionUnsupported` or `protocol.limitsMismatch`, logged, and disconnected. A client that does not complete the handshake within 5 seconds is disconnected too.
- **Heartbeat**: the client of each connection pings every 10 seconds, and the server answers. A connection on which the server receives nothing at all for 45 seconds is closed, so that a client that died, or a network path that broke, does not hold its requests in progress.
- **Connections**: at most 8 connections are served at the same time, and 4 more can be in their handshake (TLS, then protocol), 2 of them at most from one IP address; a new one beyond that is closed at once and logged as a warning, at most once every 10 seconds (the next log gives the number of refusals in between). A connection in its handshake never takes the place of a served one. The tier in front uses one shared connection, so this only bounds a peer that opens connections without end.
- **Requests in progress**: each connection handles at most 64 requests at the same time; the client never sends more (it learns the limit in the handshake), and a request beyond it is answered `server.busy` at once, without being started. A request canceled by the client (its caller gave up, for example a player who closed the tab) stops at once, and the cancellation goes on to the next tier.
- **From the Gateway**: each connection is served on its own. A request that fails is answered with an error code (never a text), logged with its cause, and the connection stays open. A connection that breaks the protocol (a malformed frame or envelope) is closed and logged as a warning, and the other connections are not affected.
- **To the Very Back**: one connection, shared by every request, opened on the first one; when it is lost, the next request opens a new one. It pings every 10 seconds, and is taken as lost when nothing comes back for 30 seconds. A failed request is never retried automatically: the peer may already have handled it. When the connection cannot be opened, the next attempt waits a back-off delay, 0.5 second doubled after each failure, spread by 20 % either way, and never more than 30 seconds; until then, calls fail at once with `link.unavailable` without touching the network, and the log tells when the next attempt is due. The first connection that opens resets the delay.
- When the Very Back cannot be reached, the request of the Gateway is answered with the code `link.unavailable` (`link.timeout` when it does not answer in time), and the connection of the Gateway stays open. A code answered by the Very Back is not passed on, except `server.busy`: any other one becomes `internal`, and is logged as a warning.

## Stop and exit code

When the host stops, the listener closes and the requests in progress are canceled. The Back never keeps running without listening: if its listener is lost, or if 50 accepts fail in a row, it logs a **critical** message and stops with the **exit code 1** (0 on a normal stop), so that a service manager can restart it. The Back is installed as a service from 0.25.0.
