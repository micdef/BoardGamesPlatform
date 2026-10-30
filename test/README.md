# Test projects

One folder per test project. In Visual Studio, they are grouped in the `9 - Tests` solution folder.

| Project | Type | Purpose |
|---------|------|---------|
| `BoardGamesPlatform.Architecture.Tests` | Tests | Architecture rules: allowed project references, game isolation, dependencies between layers |
| `BoardGamesPlatform.Back.Tests` | Tests | Back: validation of its settings (listen address and port, address of the Very Back), the host setup (invalid settings stop it before it listens), and its listener of the Gateway on loopback sockets with a fake Very Back (Ping round trip with the offset, a Very Back that is down fails the Ping without stopping the Back, lost listener, warning off loopback) |
| `BoardGamesPlatform.Database.Tests` | Tests | SQL project: conventions of its files (run everywhere); the Ping procedure and the permissions of the application role on a `TEST_` or `DEV_` database (skipped without one, see `tools/database/README.md`) |
| `BoardGamesPlatform.Gateway.Tests` | Tests | Gateway started in process and called by a SignalR client, with a fake Back client or with the real `BackClient` against a fake Back on a loopback socket: the `Ping` method of the hub (time and offset), errors without internal message, hub path setting, `/health`, the `Back` settings (defaults, startup refusal) and the client (Ping, unreachable Back, disposal) |
| `BoardGamesPlatform.Platform.Infrastructure.Tests` | Tests | Platform adapters: the Very Back client of the Back against a fake Very Back on a loopback socket (shared connection, reconnection on the next call, no automatic retry, connect timeout, disposal) and the validation of its settings |
| `BoardGamesPlatform.Protocol.Tests` | Tests | Socket protocol: framing (partial and grouped reads, end of stream, maximum length, wire format); plain TCP transport on loopback sockets (fixed test ports, round trip, clean end, refused connection, cancellation); request/response correlation (pairing in any order, closes, cancellation, protocol violations, limit per connection); JSON message envelopes (wire format, strict reading, operation routing, dates with offset) and the Ping contracts; the link server and the link client shared by the tiers, under another tier name (round trip, lost listener, repeated accept failures, single run, disposal, parameter checks) |
| `BoardGamesPlatform.TestKit` | Library | Helpers shared by the test projects: repository paths; a logger that keeps the messages; a listener whose accepts fail on demand; references the `FakeTimeProvider` package for fake time |
| `BoardGamesPlatform.VeryBack.Tests` | Tests | Very Back: validation of its settings (listen address, port, connection string the SQL client can read, never repeated), strict reading of the Ping procedure result, the host setup and its exit code, and its listener on loopback sockets with fake procedures and a fake listener (Ping round trip, a failing connection never stops the others, stop during a request, lost listener and repeated accept failures stop the host, warning off loopback); the real procedure is called only when `BOARDGAMESPLATFORM_TEST_DATABASE` is set |

## Running the tests

The tests run on **Microsoft.Testing.Platform** (MTP), selected in `global.json`: the .NET 10 SDK no longer runs MTP-based test projects such as xUnit v3 in the older VSTest mode.

| Tool | Command |
|------|---------|
| .NET CLI, every test project | `dotnet test --solution BoardGamesPlatform.CI.slnf` |
| .NET CLI, one test project | `dotnet test --project test/BoardGamesPlatform.Architecture.Tests` |
| Visual Studio | Test Explorer |

With MTP, `--solution` and `--project` are the documented syntax of `dotnet test`; use them rather than a bare path.

## Network test ports

The network tests never use a port handed out by the system (no port 0, no port freed then reused): they use fixed ports of the block **17000-17999**, below 32768, so outside the dynamic ranges of Windows (49152-65535) and of Linux (32768-60999). Windows can still exclude ranges below 32768 (list them with `netsh interface ipv4 show excludedportrange protocol=tcp`): a port in such a range fails with an explicit message.

| Block | Test project |
|-------|--------------|
| 17000-17099 | `BoardGamesPlatform.Protocol.Tests` |
| 17100-17199 | `BoardGamesPlatform.VeryBack.Tests` |
| 17200-17299 | `BoardGamesPlatform.Platform.Infrastructure.Tests` |
| 17300-17399 | `BoardGamesPlatform.Back.Tests` |
| 17400-17499 | `BoardGamesPlatform.Gateway.Tests` |

- Each project takes its ports from its own block (`TestKit/TestPortBlocks`), so that the projects run in parallel without sharing a port; each listener takes the next port of the block, never the same one twice in a run.
- A port that cannot be bound fails the test at once, with a message naming the port. On a machine that already uses the range, set `BOARDGAMESPLATFORM_TEST_PORT_BASE` (a multiple of 100, from 1100 to 31700) to move it; the multiple of 100 keeps the blocks of two runs either the same or disjoint, never partly overlapping.
- Each block is locked for the whole run of its test project (a lock file in the temporary folder, released when the run ends, even after a crash): a second run of the same test project at the same time, from another worktree or a coding agent, fails at once with a clear message. Give that run other ports with `BOARDGAMESPLATFORM_TEST_PORT_BASE` (for example 18000, 19000...).
- A new test project that opens sockets takes the next free block in `TestKit/TestPortBlocks`.
- The ports actually delivered (`Back:Port` 7821, `VeryBack:Port` 7822, read from the `appsettings.json` of the tiers) are proven by tests that start the tier with its delivered configuration: they need those ports free, so stop a local Back or Very Back before running the tests.
## Conventions

- A test project is named after the project it tests, followed by `.Tests`; `test/Directory.Build.props` gives every `*.Tests` project the test framework (xUnit v3), the assertions (Shouldly), the coverage and TRX report extensions.
- Any other project of this folder (such as the TestKit) is a plain library.
- Test classes and methods follow the [coding conventions](../doc/dev/coding-conventions.md): the summary of a test states the rule it proves.

## Architecture rules

The rules of `BoardGamesPlatform.Architecture.Tests` work at two levels:

| Level | Checks | Works while the projects are empty |
|-------|--------|:---------------------------------:|
| Project files | Every project of `src/` only references the projects listed in `ProjectReferenceTests._allowedReferences`; every entry names a real project; every project is analyzed by the layer rules; a game never references another game; nothing references the SQL project | Yes |
| Types (ArchUnitNET) | The platform domain and use cases depend on no outer layer; only the Very Back uses a SQL client | From the first type written in the layer |

A new project of `src/`, or a new reference, must be added on purpose to `_allowedReferences`, and the new project referenced by `BoardGamesPlatform.Architecture.Tests.csproj`; otherwise the build of the pull request fails.
