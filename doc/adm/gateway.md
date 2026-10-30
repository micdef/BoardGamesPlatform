# Gateway

Settings of the Gateway, the single entry point of the fronts. The general rules (sources, secrets) are in [configuration.md](configuration.md).

## Settings

Section `Gateway` (environment variable form: `Gateway__HubPath`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `Gateway:HubPath` | `/hubs/platform` | Path of the SignalR hub, relative to the address of the Gateway. It must start with `/`; otherwise the Gateway refuses to start and names the setting. |

The hub offers one method for now, `Ping`, which checks that the chain of tiers answers: the Gateway asks the Back, which asks the Very Back. If the Back does not answer, `Ping` fails with a generic error.

### Link to the Back

Section `Back` (environment variable form: `Back__Host`, `Back__Port`, `Back__ConnectTimeout`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `Back:Host` | `127.0.0.1` | Host name or address of the Back. It must not be empty. |
| `Back:Port` | `7821` | Port the Back listens on, from 1 to 65535. |
| `Back:ConnectTimeout` | `00:00:05` | Longest time allowed to open the connection to the Back. It must be greater than zero and at most 5 minutes. |

- The settings are checked at startup: an invalid value stops the Gateway, and the message names the setting, never its value.
- The connection opens on the first `Ping`, is shared by all the fronts, and is opened again on the next call when it is lost. A failed request is not retried (retries come with 0.4.0).
- **Plain TCP until 0.5.0**: the link is not encrypted nor authenticated. For development only: keep the Gateway and the Back on the same machine or on a trusted network. TLS comes in 0.5.0.

## Security

- **The hub is not authenticated until 0.10.0**: anyone who can reach the Gateway can call it. Until then, do not expose the Gateway outside a trusted network.
- Detailed errors are off: an internal failure reaches a front as a generic error, never with its message. The details stay in the logs of the Gateway.
