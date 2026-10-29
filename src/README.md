# Source projects

One folder per project. In Visual Studio, solution folders group the projects by tier, in the order of the chain Fronts → Gateway → Back → Very Back → SQL Server.

| Solution folder | Project | Type | Purpose |
|-----------------|---------|------|---------|
| 0 - Shared | `SocialGamesPlatform.Codes` | Library | Language-neutral codes (errors, statuses, events) that the fronts translate |
| 0 - Shared | `SocialGamesPlatform.Protocol` | Library | Socket protocol of the Gateway ↔ Back and Back ↔ Very Back links |
| 1 - Front-End | `SocialGamesPlatform.Web` | Blazor (server) | Website, server part of the Blazor Auto application, hosted on IIS |
| 1 - Front-End | `SocialGamesPlatform.Web.Client` | Blazor (WebAssembly) | Website, part run in the player's browser |
| 2 - Gateway | `SocialGamesPlatform.Gateway` | ASP.NET Core | Single entry point of the fronts: SignalR hub, authentication, validation, rate limiting |
| 2 - Gateway | `SocialGamesPlatform.Gateway.Contracts` | Library | Contract exposed by the Gateway to the fronts |
| 3 - Back-End / Platform | `SocialGamesPlatform.Platform.Domain` | Library | Platform entities and rules |
| 3 - Back-End / Platform | `SocialGamesPlatform.Platform.Application` | Library | Platform use cases and ports |
| 3 - Back-End / Platform | `SocialGamesPlatform.Platform.Infrastructure` | Library | Platform adapters: the Very Back client |
| 3 - Back-End / Games | `SocialGamesPlatform.Games.Abstractions` | Library | Game contract that every game implements |
| 3 - Back-End / Host | `SocialGamesPlatform.Back` | Worker service | Back host: composition root and socket server for the Gateway |
| 3 - Back-End / Host | `SocialGamesPlatform.Back.Contracts` | Library | Contract exposed by the Back to the Gateway |
| 4 - Very-Back-End | `SocialGamesPlatform.VeryBack` | Worker service | Very Back host: the only access to the database |
| 4 - Very-Back-End | `SocialGamesPlatform.VeryBack.Contracts` | Library | Contract exposed by the Very Back to the Back |
| 5 - Database | `SocialGamesPlatform.Database` | SQL project | Database schema (SSDT, built in Visual Studio only) |

## Rules

- A `*.Contracts` project belongs to the tier that exposes it; only that tier and its direct caller reference it.
- No project references the SQL project: the Very Back calls the stored procedures by name.
- No project references a game other than itself; the Gateway references no game at all.
- The CI builds the solution filter `SocialGamesPlatform.CI.slnf`, which lists every .NET project and leaves out the SQL project.
