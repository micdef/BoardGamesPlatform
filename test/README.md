# Test projects

One folder per test project. In Visual Studio, they are grouped in the `9 - Tests` solution folder.

| Project | Type | Purpose |
|---------|------|---------|
| `BoardGamesPlatform.Architecture.Tests` | Tests | Architecture rules: allowed project references, game isolation, dependencies between layers |
| `BoardGamesPlatform.Database.Tests` | Tests | SQL project: conventions of its files (run everywhere); the Ping procedure and the permissions of the application role on a `TEST_` or `DEV_` database (skipped without one, see `tools/database/README.md`) |
| `BoardGamesPlatform.Gateway.Tests` | Tests | Gateway started in process with a fake Back client and called by a SignalR client: the `Ping` method of the hub (time and offset), errors without internal message, hub path setting, `/health` |
| `BoardGamesPlatform.Platform.Infrastructure.Tests` | Tests | Platform adapters: the Very Back client of the Back against a fake Very Back on a loopback socket (shared connection, reconnection on the next call, no automatic retry, connect timeout, disposal) and the validation of its settings |
| `BoardGamesPlatform.Protocol.Tests` | Tests | Socket protocol: framing (partial and grouped reads, end of stream, maximum length, wire format); plain TCP transport on loopback sockets (port 0, round trip, clean end, cancellation); request/response correlation (pairing in any order, closes, cancellation, protocol violations, limit per connection); JSON message envelopes (wire format, strict reading, operation routing, dates with offset) and the Ping contracts |
| `BoardGamesPlatform.TestKit` | Library | Helpers shared by the test projects: repository paths; references the `FakeTimeProvider` package for fake time |

## Running the tests

The tests run on **Microsoft.Testing.Platform** (MTP), selected in `global.json`: the .NET 10 SDK no longer runs MTP-based test projects such as xUnit v3 in the older VSTest mode.

| Tool | Command |
|------|---------|
| .NET CLI, every test project | `dotnet test --solution BoardGamesPlatform.CI.slnf` |
| .NET CLI, one test project | `dotnet test --project test/BoardGamesPlatform.Architecture.Tests` |
| Visual Studio | Test Explorer |

With MTP, `--solution` and `--project` are the documented syntax of `dotnet test`; use them rather than a bare path.

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
