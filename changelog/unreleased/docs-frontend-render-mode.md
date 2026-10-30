### Changed

- Roadmap and administrator documentation: the website uses the Blazor Interactive Server render mode only (no WebAssembly, no static rendering), and its server process is the only SignalR client of the gateway; the gateway is never reachable from the Internet. The decision and its reasons are in the decisions table.
- Roadmap: every executable runs on Kestrel as a service, without IIS; the website is exposed behind a reverse proxy (Nginx, IIS possible); the network flows separate the Internet, the reverse proxy, the website and the gateway.
- Roadmap 0.10.0: the gateway authenticates the website, counts the rate limit per player, receives the IP address of the player from the website only, and the storage of the session token in the website is decided.