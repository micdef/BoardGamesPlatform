# Source projects

One folder per project. In Visual Studio, solution folders group the projects by tier, in the order of the chain Fronts → Gateway → Back → Very Back → SQL Server.

| Solution folder | Project | Type | Purpose |
|-----------------|---------|------|---------|
| 0 - Shared | `BoardGamesPlatform.Codes` | Library | Language-neutral codes (errors, statuses, events) that the fronts translate |
| 0 - Shared | `BoardGamesPlatform.Protocol` | Library | Socket protocol of the Gateway ↔ Back and Back ↔ Very Back links |
| 0 - Shared | `BoardGamesPlatform.Observability` | Library | Sends the logs, traces and metrics of every executable to an OpenTelemetry collector, when one is configured |
| 1 - Front-End | `BoardGamesPlatform.Web` | Blazor (server) | Website, server part of the Blazor Auto application, hosted on IIS |
| 1 - Front-End | `BoardGamesPlatform.Web.Client` | Blazor (WebAssembly) | Website, part run in the player's browser |
| 2 - Gateway | `BoardGamesPlatform.Gateway` | ASP.NET Core | Single entry point of the fronts: SignalR hub, authentication, validation, rate limiting |
| 2 - Gateway | `BoardGamesPlatform.Gateway.Contracts` | Library | Contract exposed by the Gateway to the fronts |
| 3 - Back-End / Platform | `BoardGamesPlatform.Platform.Domain` | Library | Platform entities and rules |
| 3 - Back-End / Platform | `BoardGamesPlatform.Platform.Application` | Library | Platform use cases and ports |
| 3 - Back-End / Platform | `BoardGamesPlatform.Platform.Infrastructure` | Library | Platform adapters: the Very Back client |
| 3 - Back-End / Games | `BoardGamesPlatform.Games.Abstractions` | Library | Game contract that every game implements |
| 3 - Back-End / Host | `BoardGamesPlatform.Back` | Worker service | Back host: composition root and socket server for the Gateway |
| 3 - Back-End / Host | `BoardGamesPlatform.Back.Contracts` | Library | Contract exposed by the Back to the Gateway |
| 4 - Very-Back-End | `BoardGamesPlatform.VeryBack` | Worker service | Very Back host: the only access to the database |
| 4 - Very-Back-End | `BoardGamesPlatform.VeryBack.Contracts` | Library | Contract exposed by the Very Back to the Back |
| 5 - Database | `BoardGamesPlatform.Database` | SQL project | Database schema (SSDT, built in Visual Studio only) |

## Rules

- A `*.Contracts` project belongs to the tier that exposes it; only that tier and its direct caller reference it.
- No project references the SQL project: the Very Back calls the stored procedures by name.
- No project references a game other than itself; the Gateway references no game at all.
- The CI builds the solution filter `BoardGamesPlatform.CI.slnf`, which lists every .NET project and leaves out the SQL project.

## Building

| Tool | Command | Builds the SQL project |
|------|---------|------------------------|
| Visual Studio | Build Solution (Ctrl+Shift+B) | Yes |
| .NET CLI | `dotnet build BoardGamesPlatform.CI.slnf` | No |
| .NET CLI | `dotnet build BoardGamesPlatform.slnx` | Fails (error MSB4278: the SSDT targets only exist in Visual Studio); use the solution filter instead |
