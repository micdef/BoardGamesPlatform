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

## Connections to SQL Server

The Very Back uses the connection pool of the SQL client (`Microsoft.Data.SqlClient`, pooling on by default). Settings of the pool go in `ConnectionStrings:Main`, nowhere else: the Very Back sets no pool size itself.

- **A few sessions stay open between requests: this is normal.** Closing a connection returns its session to the pool instead of closing it, so that the next request skips the network connection and the login. They show in `sys.dm_exec_sessions`, under the name below.
- **Naming.** The sessions are named `BoardGamesPlatform.VeryBack` (column `program_name`) unless the connection string sets `Application Name` (or `App`) to a non-empty value: the value of the administrator is always kept. This is how to recognize the sessions of the Very Back, and tell them from other applications on the same server.
- **Bounding the pool.** Two keywords of the connection string:

  | Keyword | Default of the SQL client | Effect |
  |---------|---------------------------|--------|
  | `Max Pool Size` | `100` | Maximum number of sessions the pool holds. When all are in use, the next request waits up to `Connect Timeout` (15 seconds by default), then fails |
  | `Load Balance Timeout` (alias `Connection Lifetime`) | `0` (no limit) | A session older than this many seconds is closed when it returns to the pool, so the sessions turn over gradually |
  | `Connection Idle Timeout` | `300` (seconds) | Since Microsoft.Data.SqlClient 7.1.0: an idle session becomes eligible for closing after this delay; `0` disables it. Enforced only when the `UseLegacyIdleTimeoutBehavior` AppContext switch is `false` |

  With the default `Load Balance Timeout=0`, the pool also removes unused sessions above `Min Pool Size` (default `0`) after about four to eight minutes; Microsoft Learn presents this interval as implementation behavior, not as a guarantee per session. The Very Back gives no recommended value: size `Max Pool Size` from what your SQL Server accepts, remembering that the sessions can reach `Max Pool Size` for each running instance of the Very Back.
- **No pool.** `Pooling=False` opens and closes a real session for every request, at the cost of a full login each time. Nothing stays open between requests.
- **Stop.** When the Very Back stops, once the listener has ended and the requests in progress with it, the pool of its connection string is cleared (`SqlConnection.ClearPool`): no session of the Very Back stays on SQL Server after a stop or a redeployment. Only the pool of this connection string is cleared, not every pool of the process (`ClearAllPools`). If requests outlast the shutdown timeout of the host (30 seconds by default), the pool is cleared anyway: the sessions in use are closed when their connections are closed, and a request that starts after the clearing is refused instead of opening a new session. A process that is killed instead of stopped cannot clear anything (what SQL Server does with its sessions is not verified here).

Sources: [SQL Server connection pooling with Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/sql-server-connection-pooling), [SqlConnection.ClearPool](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.clearpool), [Connection strings for Microsoft.Data.SqlClient](https://learn.microsoft.com/sql/connect/ado-net/connection-strings).
