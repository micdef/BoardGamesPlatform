# Website

The website is a Blazor application in the **Interactive Server** render mode only (no WebAssembly, no static rendering of the pages). The browser talks to the website; the **server process of the website is the only SignalR client of the Gateway**, the browser never reaches the Gateway. The general rules (sources, secrets) are in [configuration.md](configuration.md).

## Settings

Section `Gateway` (environment variable form: `Gateway__HubUrl`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `Gateway:HubUrl` | `http://localhost:5273/hubs/platform` | Full address of the SignalR hub of the Gateway: an absolute `http` or `https` address, ending with the hub path of the Gateway (`Gateway:HubPath`, see [gateway.md](gateway.md)). The default is the HTTP address of the Gateway in development. |

The website **refuses to start** and names the setting (never its value) when `Gateway:HubUrl` is not an absolute `http` or `https` address.

The website connects to the Gateway on the first call of a player, not at startup: it starts even when the Gateway is down, and the page then shows a generic error.
One connection lives as long as the circuit of one player and is closed with it.

## Languages

Every text shown to a player comes from the resources `Resources/WebTexts.resx` (English, the default) and `Resources/WebTexts.fr.resx` (French). The language and the date format follow the `Accept-Language` header of the request, among the supported languages (`en`, `fr`).

## Pages

| Address | Content |
|---------|---------|
| `/ping` | A button that pings the platform through the Gateway and shows the time of SQL Server with its offset (Gateway, Back, Very Back and SQL Server must run) |

## Security

- **Plain HTTP until authentication (0.10.0)**: the link from the website to the Gateway is not encrypted and the hub is not authenticated. Use it for development only, on one machine or a trusted network, and never expose the Gateway outside it.
- A failing call shows a generic message to the player; the details stay in the logs of the website.
