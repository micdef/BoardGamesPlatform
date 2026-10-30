# Website

The website is a Blazor application in the **Interactive Server** render mode only (no WebAssembly, no static rendering of the pages). The browser talks to the website; the **server process of the website is the only SignalR client of the Gateway**, the browser never reaches the Gateway. The general rules (sources, secrets) are in [configuration.md](configuration.md).

## Settings

Section `Gateway` (environment variable form: `Gateway__HubUrl`).

| Setting | Default | Meaning |
|---------|---------|---------|
| `Gateway:HubUrl` | `http://localhost:5273/hubs/platform` | Full address of the SignalR hub of the Gateway: an absolute `http` or `https` address, ending with the hub path of the Gateway (`Gateway:HubPath`, see [gateway.md](gateway.md)). The default is the HTTP address of the Gateway in development. |

| `Gateway:RequestTimeout` | `00:00:10` | Longest duration of a call to the Gateway, connection included. Greater than zero and at most `00:01:00`. When it elapses, the player sees the generic error instead of waiting forever. |

The website **refuses to start** and names the setting (never its value) when `Gateway:HubUrl` is not an absolute `http` or `https` address, or when `Gateway:RequestTimeout` is outside its range.

The website connects to the Gateway on the first call of a player, not at startup: it starts even when the Gateway is down, and the page then shows a generic error.
One connection lives as long as the circuit of one player and is closed with it.

## Languages

Every text shown to a player comes from the resources `Resources/WebTexts.resx` (English, the default) and `Resources/WebTexts.fr.resx` (French). The supported languages are `en` (default) and `fr`. The request localization middleware chooses the culture of a request (by default from the `Accept-Language` header). **Not verified end to end:** the tests set the culture directly, so they do not prove that the culture chosen by the middleware reaches the circuit of the player. Microsoft Learn recommends a localization culture cookie for server-side Blazor; this is left to the browser tests of 0.11.0.

## Tests

The tests start the real Gateway in process, reached through the in-memory test server by **long polling**. A real website connects by WebSockets first, so **WebSockets are not tested** by the automated tests.

## Pages

| Address | Content |
|---------|---------|
| `/ping` | A button that pings the platform through the Gateway and shows the time of SQL Server with its offset (Gateway, Back, Very Back and SQL Server must run) |

## Security

- **Plain HTTP until authentication (0.10.0)**: the link from the website to the Gateway is not encrypted and the hub is not authenticated. Use it for development only, on one machine or a trusted network, and never expose the Gateway outside it.
- A failing call shows a generic message to the player; the details stay in the logs of the website.
