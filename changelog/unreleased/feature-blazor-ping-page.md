### Added

- Website, ping page (`/ping`): a button calls the Ping method of the Gateway and shows the time of SQL Server with its offset, formatted with the culture of the request, with a localized "in progress" state and a localized generic error (never the text of the exception).
- Website, client of the Gateway: `IGatewayClient` and its `SignalRGatewayClient`, a scoped service (one SignalR connection per circuit, started on first use, disposed with the circuit). Setting `Gateway:HubUrl` (also `Gateway__HubUrl`, default `http://localhost:5273/hubs/platform`), validated at startup: an invalid value stops the website and the message names the setting, never its value. Setting `Gateway:RequestTimeout` (default 10 seconds, from zero excluded to one minute) bounds every call: a Gateway that never answers gives the generic error. Disposing the client cancels the calls in progress and the calls waiting to connect. Plain HTTP until authentication (0.10.0).
- Website, languages: every text comes from `.resx` resources (`WebTexts`, English by default and French); the supported cultures are `en` and `fr` (that the culture chosen by the request localization middleware reaches the circuit is not verified end to end).
- Website tests (`BoardGamesPlatform.Web.Tests`): the page rendered by bUnit with a fake client (time, progress, error without the exception message, English and French), the SignalR client against the real Gateway started in process with a fake Back client (restart after a loss, single start for parallel calls, cancellation, timeout, disposal), and the `Gateway:HubUrl` setting.
- `bunit` 2.13.3, for the website tests only.
- Administrator documentation: `doc/adm/website.md`.

### Changed

- Website: the Interactive Server render mode is set for the whole application (`Routes` and `HeadOutlet`), as decided for 0.3.0.

### Removed

- `BoardGamesPlatform.Web.Client` (WebAssembly project of the template) and the WebAssembly packages: the website runs in the Interactive Server render mode only.
