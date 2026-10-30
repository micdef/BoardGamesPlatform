# Very Back

The Very Back is the only process that reaches the database. The Back connects to it; nothing else does.

## Settings

| Key | Default | Meaning |
|-----|---------|---------|
| `VeryBack:ListenAddress` | `127.0.0.1` | IP address (IPv4 or IPv6) the Very Back listens on. The loopback address keeps it unreachable from other machines. Another address is accepted, so that the tiers can run on separate machines or containers, but the Very Back logs a **warning** at startup: until 0.5.0 the link is neither encrypted nor authenticated, so restrict it with the firewall to the machine of the Back |
| `VeryBack:Port` | `7822` | TCP port the Very Back listens on (Back to Very Back, see the network flows of the [roadmap](../analyse/roadmap.md)), from 1 to 65535 |
| `ConnectionStrings:Main` | empty | Connection string of the database, with the application SQL user. A secret: see [configuration.md](configuration.md) |

The Very Back **refuses to start**, before it listens, and says which setting is wrong without repeating a connection string, when:

- the address is not an IP address, or the port is outside 1-65535;
- `ConnectionStrings:Main` is missing or empty;
- `ConnectionStrings:Main` cannot be read by the SQL client: unknown keyword, invalid value, unbalanced quote. A value holding a semicolon (a password, for example) must be quoted: `Password="ab;cd"`. The error of the SQL client is not repeated, since it quotes the unknown keyword, which can be a piece of the password.

Whether the server, the database and the login are right is only known when the first request reaches the database.

As environment variables: `VeryBack__ListenAddress`, `VeryBack__Port`, `ConnectionStrings__Main`.

## Transport

In 0.3.0 and 0.4.0 the link is plain TCP, for development only. TLS 1.3 with an authenticated key exchange comes with 0.5.0, before any real use.

Each connection of the Back is served on its own: a connection that breaks the protocol, or whose request fails (for example when the database is unreachable or answers something unexpected), is closed and logged as a warning, and the other connections are not affected. Until the error codes of 0.4.0, a failing request also ends the other requests in progress **on the same connection**: the Back sees the connection close and opens a new one on its next call.

The logs name the remote end point and the error, never the connection string. An error of SQL Server itself may name the server, the database or the login, as SQL Server writes them in its messages.

## Stop and exit code

When the host stops, the listener closes and the requests in progress are canceled, including a call to the database.

The Very Back never keeps running without listening. If its listener is lost, or if 50 accepts fail in a row, it logs a **critical** message and stops, with the **exit code 1**. A normal stop gives the exit code 0. A service manager (Windows service recovery, systemd `Restart=on-failure`) can therefore restart it; the Very Back is installed as a service from 0.25.0.
