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

The Back **refuses to start**, before it listens, and names the wrong setting, when one of these values is invalid.

As environment variables: `Back__ListenAddress`, `Back__Port`, `VeryBack__Host`, `VeryBack__Port`, `VeryBack__ConnectTimeout`.

## Transport

In 0.3.0 and 0.4.0 both links of the Back are plain TCP, for development only. TLS 1.3 comes with 0.5.0, before any real use.

- **From the Gateway**: each connection is served on its own; a connection that breaks the protocol, or whose request fails, is closed and logged as a warning, and the other connections are not affected.
- **To the Very Back**: one connection, shared by every request, opened on the first one; when it is lost, the next request opens a new one. A failed request is not retried (retries come with 0.4.0).
- When the Very Back cannot be reached, the request of the Gateway fails, and until the error codes of 0.4.0 the connection of the Gateway is closed: the Gateway opens a new one on its next call.

## Stop and exit code

When the host stops, the listener closes and the requests in progress are canceled. The Back never keeps running without listening: if its listener is lost, or if 50 accepts fail in a row, it logs a **critical** message and stops with the **exit code 1** (0 on a normal stop), so that a service manager can restart it. The Back is installed as a service from 0.25.0.
