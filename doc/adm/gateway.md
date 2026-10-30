# Gateway

Settings of the Gateway, the single entry point of the fronts. The general rules (sources, secrets) are in [configuration.md](configuration.md).

## Settings

Section `Gateway` (environment variable form: `Gateway__HubPath`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `Gateway:HubPath` | `/hubs/platform` | Path of the SignalR hub, relative to the address of the Gateway. It must start with `/`; otherwise the Gateway refuses to start and names the setting. |

The hub offers one method for now, `Ping`, which checks that the chain of tiers answers. Until the link to the Back is wired (`feature/back-gateway-link`), `Ping` fails with a generic error.

## Security

- **The hub is not authenticated until 0.10.0**: anyone who can reach the Gateway can call it. Until then, do not expose the Gateway outside a trusted network.
- Detailed errors are off: an internal failure reaches a front as a generic error, never with its message. The details stay in the logs of the Gateway.
