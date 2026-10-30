# Very Back

The Very Back is the only process that reaches the database. The Back connects to it; nothing else does.

## Settings

| Key | Default | Meaning |
|-----|---------|---------|
| `VeryBack:ListenAddress` | `127.0.0.1` | IP address (IPv4 or IPv6) the Very Back listens on. The loopback address keeps it unreachable from other machines; set another address on purpose only |
| `VeryBack:Port` | `7822` | TCP port the Very Back listens on (Back to Very Back, see the network flows of the [roadmap](../analyse/roadmap.md)), from 1 to 65535 |
| `ConnectionStrings:Main` | empty | Connection string of the database, with the application SQL user. A secret: see [configuration.md](configuration.md) |

The Very Back **refuses to start** (and says which setting is wrong, without repeating a connection string) when the address is not an IP address, when the port is outside 1-65535, or when `ConnectionStrings:Main` is missing or empty.

As environment variables: `VeryBack__ListenAddress`, `VeryBack__Port`, `ConnectionStrings__Main`.

## Transport

In 0.3.0 and 0.4.0 the link is plain TCP, for development only. TLS 1.3 with an authenticated key exchange comes with 0.5.0, before any real use.

Each connection of the Back is served on its own: a connection that breaks the protocol, or whose request fails (for example when the database answers something unexpected), is closed and logged, and the other connections are not affected. The logs name the remote end point and the error, never a secret.
