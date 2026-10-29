# Roadmap to v1.0.0

This document plans every version from the repository bootstrap (`0.1.0`) to the first installable and executable release (`1.0.0`).
It records the scope, the order of the work, what each version must demonstrate, and the decisions taken so far.

## Scope

### In v1.0.0

- Player accounts: registration with nickname, e-mail address and password; e-mail verification; password reset by e-mail; profile.
- Registrations that the administrator can open or close at will.
- A lobby with a public chat, where players propose matches to each other by invitation.
- Friends list and private messaging.
- Battleships, playable by two players in real time, with spectators.
- Statistics and a per-game rating, used to pick a random online opponent of a similar level.
- AI players for Battleships, each level being a named player: easy, medium and hard.
- An administration section in the website and a separate administration tool.
- A user interface available in several languages (at least English and French), in the arcade neon style of the design mock-up.
- A native installation (IIS, Windows services, Linux systemd daemons) and a Docker variant.
- User, administrator and developer documentation.

### Out of v1.0.0

| Item | Planned for |
|------|-------------|
| Learning AI (expert level, trained on the match logs) | After 1.0.0 |
| Moderation (the data model is prepared in 0.24, no moderation code before 1.0.0) | After 1.0.0 |
| Kubernetes deployment | 1.1 |
| Other games (checkers, connect four, ludo…) | After 1.0.0 |

## Principles

- **One theme per version.** Each version ends with a demonstration, written below as its *done criteria*.
- **Riskiest work first.** The custom socket protocol, its security layers and the end-to-end path through every tier come before the features.
- **Tests from the start.** The test infrastructure exists from 0.2.0; every version ships the tests of what it adds, in the same branches as the code.
- **One version at a time.** Versions are developed in sequence: a single `release/X.Y.Z` branch is open at any time.
- **Multilingual from the start.** No text shown to a user is written in the code: the fronts read it from resources in the player's language. The other tiers never send display texts, only codes (errors, events, statuses) that the fronts translate.
- **Security-sensitive work is marked [SEC].** It is written by the main developer and reviewed by the security agents and by the project lead.

## Architecture summary

```
Blazor site ┐
WPF (later) ├─► Gateway ─► Back ─► Very Back ─► SQL Server
Other UIs   ┘
```

| Tier | Role | Talks to |
|------|------|----------|
| Fronts | User interfaces. Blazor Auto; forms forced to InteractiveServer. | Gateway only (SignalR) |
| Gateway | Single entry point: SignalR hub, authentication, shape validation, rate limiting. No game logic. | Back only (TLS socket) |
| Back | Modular monolith: a Platform module and one module per game. All the logic, maximum server authority. | Very Back only (TLS socket + authenticated ECDH) |
| Very Back | Autonomous service, the only access to the database. | SQL Server only |
| SQL Server | Stored procedures, table-valued parameters and views only, for a dedicated application user. | — |

## Versions

### Block 1 — Foundations and technical risks

#### 0.2.0 — Solution skeleton and test infrastructure

- **Content**: one project per tier, a shared contracts library, the empty SQL project, configuration and secrets conventions; observability: every executable writes its logs to the system log (Windows Event Log or systemd journal) and can send its logs, traces and metrics to an OpenTelemetry collector set in its configuration (see `doc/adm/observability.md`); the `test/` layout, a shared test-helper project (repository paths, fake clock; further helpers are added by the version that first needs them), architecture tests with the first dependency rules, coverage collection, a CI guard that fails when a test project is not executed.
- **Done**: the CI is green, every tier starts and logs its startup; the CI turns red when an architecture rule is broken.
- **Branches**: `chore/solution-layout`, `feature/observability`, `chore/shared-contracts`, `chore/test-infrastructure`, `feature/architecture-tests`, `chore/ci-test-guard`, `chore/coverage-reporting`, `chore/sql-project`.

#### 0.3.0 — Walking skeleton

- **Content**: a minimal socket protocol on plain TCP: a transport interface that hands over a byte stream (TLS wraps it in 0.5.0), frames with a length prefix, a correlation identifier, a JSON payload and a maximum size, several requests in flight on one connection [SEC]; the Back's client of the Very Back; a `Ping` stored procedure and a minimal application SQL user allowed to execute it only [SEC]; the SignalR hub of the gateway; a Blazor page. The ports of each link become configurable (defaults in the network flows below); every listener binds the loopback address unless configured otherwise. Development-only version: never deployed without TLS.
- **Done**: a button on a Blazor page displays the SQL Server time, obtained through the five tiers; an end-to-end test proves it. In the CI, the end-to-end test runs against a fake procedure executor; its variant against the real SQL Server runs locally, under the SQL gate.
- **Tests**: framing unit tests, a first loopback integration test, a first end-to-end test, a first SQL test (`Ping` and permissions) with the DACPAC test-deployment script. Network tests never pick a "free" port in advance, because another process can take it between the check and the use: an in-process listener binds port 0 and exposes the port given by the system; a free-port helper in the TestKit is only added if the end-to-end tests start the tiers as separate processes. Check whether the CI can build the SQL project as it is (original format, with the Visual Studio build tools of the CI runner); otherwise the local SQL gate applies (see the test strategy).
- **Branches**, in order: `docs/ports-and-wire-format`, `chore/sql-ci-check`, `feature/minimal-frame-protocol`, `feature/transport-interface-tcp`, `chore/sql-test-deploy-script`, `feature/sql-ping-procedure`, `feature/protocol-request-response`, `feature/very-back-ping`, `feature/back-very-back-link`, `feature/back-gateway-link`, `feature/signalr-hub-ping`, `feature/blazor-ping-page`, `feature/e2e-test-host`. The deployment script of this version already refuses any database without the `DEV_` or `TEST_` prefix; 0.7.0 extends it.

#### 0.4.0 — Robust socket protocol [SEC]

- **Content**: protocol versioning and negotiation, serialization format, heartbeat, reconnection with back-off, request timeouts, message size limits, rejection of malformed frames; errors carried as codes, never as display texts.
- **Done**: the Very Back is stopped in the middle of a session; the Back reconnects, and pending requests either resume or fail cleanly.
- **Tests**: property-based and fuzz tests of the framing, reconnection tests against a test server that can be killed.
- **Branches**: `feature/protocol-versioning`, `feature/heartbeat`, `feature/reconnection`, `feature/protocol-limits`, `docs/protocol-specification`.

#### 0.5.0 — Mutual TLS 1.3 [SEC]

- **Content**: TLS 1.3 with mutual authentication (`SslStream`) on both socket links (Gateway ↔ Back, Back ↔ Very Back), certificate validation and pinning rules, PowerShell scripts that issue development certificates.
- **Done**: a connection without a valid client certificate is refused.
- **Tests**: valid, missing, wrong-authority and expired certificates. Test certificates are generated at run time; no private key is ever committed.
- **Branches**: `feature/mtls-transport`, `chore/dev-certificate-scripts`, `docs/certificates-and-rotation`.

#### 0.6.0 — Authenticated ECDH handshake, Back ↔ Very Back [SEC]

- **Content**: ephemeral ECDH keys signed with long-term ECDSA keys distinct from the TLS certificates, key derivation with HKDF, AES-GCM channel, protection against replay and downgrade. Standard .NET primitives only (`ECDiffieHellman`, `ECDsa`, `HKDF`, `AesGcm`).
- **Done**: a test intermediary that holds a valid TLS certificate but not the signing keys fails the handshake.
- **Tests**: test vectors, man-in-the-middle proxy, replay, downgrade, tampered AES-GCM frames.
- **Branches**: `feature/ecdh-handshake`, `feature/secure-channel`, `docs/handshake-threat-model`.

#### 0.7.0 — Database foundation [SEC]

- **Content**: schemas, filegroups, temporal tables, immutable audit, table-valued parameter and JSON-result patterns; `DEV_`, `ACC_` and `TEST_` databases; one application SQL user per database with a random password stored in a vault; the deployment script.
- **Deployment script**: it drops and recreates a `DEV_` or `TEST_` database, and refuses to drop any database without one of these prefixes.
- **Done**: `DEV_` is deployed from scratch by one script; the application user can execute procedures but cannot read tables; an update of the audit is rejected.
- **Tests**: the SQL test suite becomes the standard: one test class per procedure group, audit immutability, permission matrix, temporal history. A new procedure without a test fails the review.
- **Branches**: `feature/db-schemas-and-audit`, `feature/tvp-json-pattern`, `chore/db-deploy-script`, `feature/vault-secrets`.

### Block 2 — Platform

#### 0.8.0 — Accounts and sessions [SEC]

- **Content**: registration (nickname, e-mail address, password, preferred language), hashed passwords, login and logout, opaque 256-bit session token (only its SHA-256 hash is stored), expiry, immediate revocation, session audit, account states (active, warned, suspended, banned), registration switch.
- **Done**: register, log in, log out; a revoked token is refused immediately; the audit rows exist.
- **Tests**: expiry, revocation, hash-only storage, registration switch.
- **Branches**: `feature/account-registration`, `feature/session-tokens`, `feature/session-audit`, `feature/registration-switch`.

#### 0.9.0 — E-mail: verification and password reset [SEC]

- **Content**: e-mail sending through an SMTP server, e-mail templates in each supported language (the player's preferred language), e-mail address verification, password reset by a single-use, time-limited link (only its hash is stored).
- **Done**: a new account is activated from the verification e-mail; a forgotten password is reset from the e-mail link, and the link no longer works afterwards.
- **Tests**: single use, expiry, unverified account restrictions; e-mails captured by a test SMTP server.
- **Branches**: `feature/email-sending`, `feature/email-verification`, `feature/password-reset`.

#### 0.10.0 — Gateway and Platform module [SEC]

- **Content**: the Platform module and the registration of game modules; token verification by the Back through the Very Back; rate limiting, shape validation and error model in the gateway.
- **Done**: anonymous calls are rejected; flooding the gateway is throttled.
- **Tests**: architecture rule "the gateway contains no game logic", rate-limit tests.
- **Branches**: `feature/platform-module`, `feature/gateway-authentication`, `feature/gateway-rate-limit`.

#### 0.11.0 — Website, design system and languages

- **Content**:
  - the **design system** of the arcade neon mock-up: colors, typography (Orbitron for titles and buttons, Rajdhani for text), buttons, badges, form fields, glow rules, as one shared stylesheet of variables and Blazor components;
  - the **visual identity of a game** (icon and background watermark), provided by each game module, so that a player knows at a glance which game is on screen;
  - **internationalization**: English and French, in .NET `.resx` resource files (adding a language means adding one resource file per component); a language picker with flags from [flagcdn.com](https://flagcdn.com) (Flagpedia), copied into the site so that no player's browser calls a third party; the language changes **on the fly**, without reloading the page nor interrupting a match in progress; the player's preferred language; culture-aware dates and numbers; translation of the codes received from the other tiers;
  - the Blazor Auto layout, the registration and login forms forced to InteractiveServer with full validation, a minimal administration section (open or close registrations, user list).
- **Done**: a user registers and logs in from a browser, in English or in French, and the whole site switches language; the administrator closes the registrations and the registration form refuses new accounts.
- **Tests**: every resource key exists in every supported language (a missing translation fails the build); component tests of the design system.
- **Branches**: `feature/design-system`, `feature/game-visual-identity`, `feature/localization`, `feature/site-layout`, `feature/login-registration-forms`, `feature/admin-registration-switch`.

#### 0.12.0 — Lobby and chat

- **Content**: online players list, public chat. Messages are identifiable, timestamped and can be hidden without being deleted, for the future moderation.
- **Done**: two browsers chat in real time.
- **Branches**: `feature/presence`, `feature/lobby-chat`, `feature/message-model`.

### Block 3 — The game

#### 0.13.0 — Game contract, Battleships rules and public view

- **Content**: the game contract (description including the game's visual identity — icon and background watermark, required for every game —, match creation, action handling, events, per-player view, public view for spectators, end of match); the Battleships rules as a pure domain.
- **Done**: a complete scripted match passes the tests; the opponent's view and the public view never contain ship positions.
- **Tests**: domain tests are the core deliverable, with a seeded random source added to the TestKit next to the injectable random source of the domain; property-based tests (any legal sequence of moves ends; no view contains hidden data); architecture rule: each game module is isolated.
- **Branches**: `feature/game-contract`, `feature/battleships-rules`, `feature/battleships-views`.

#### 0.14.0 — Move log and match recovery [SEC]

- **Content**: match tables; every move recorded with the match id and a monotonic sequence number (a move received twice is recorded once); server-side randomness recorded as moves; replay; recovery after a restart; pseudonymized training log.
- **Done**: the Back is restarted in the middle of a match and the match resumes; a move sent twice changes nothing.
- **Tests**: replay equals the live result, idempotency, recovery, no account identifier in the training log.
- **Branches**: `feature/match-persistence`, `feature/move-log`, `feature/match-recovery`, `feature/training-log`.

#### 0.15.0 — Playable Battleships

- **Content**: the Battleships UI (fleet placement, shots, own and opponent views, end screen), real-time push, disconnection and reconnection.
- **Done**: two browsers play a complete match.
- **Tests**: end-to-end match between two headless clients, Blazor component tests of the board.
- **Branches**: `feature/battleships-ui`, `feature/match-realtime-push`, `feature/reconnection-handling`.

#### 0.16.0 — Invitations

- **Content**: invite a player from the lobby, accept, decline, expiry, one active match per player.
- **Done**: a player invites another from the lobby, the invitation is accepted and the match starts.
- **Branches**: `feature/match-invitations`.

#### 0.17.0 — Spectators

- **Content**: list of matches in progress, joining a match as a spectator through the public view.
- **Done**: a third browser watches a match: shots and results are visible, fleets never are.
- **Branches**: `feature/spectators`.

### Block 4 — Social features and ranking

#### 0.18.0 — Profile, friends and private messaging

- **Content**: player profile, friend requests and friends list, private messages (same message model as the chat), friends' presence.
- **Done**: two friends exchange private messages.
- **Branches**: `feature/player-profile`, `feature/friends`, `feature/private-messages`.

#### 0.19.0 — Statistics and rating

- **Content**: match statistics; per-game rating (Elo or Glicko, chosen at the start of the version); ranking page.
- **Done**: the ratings and statistics are updated at the end of a match.
- **Tests**: reference values of the rating system, convergence simulations.
- **Branches**: `feature/match-statistics`, `feature/rating-engine`, `feature/ranking-page`.

#### 0.20.0 — Opponent of a similar level

- **Content**: matchmaking queue, rating window that widens with the waiting time, cancellation.
- **Done**: two players of a similar level are paired automatically.
- **Branches**: `feature/matchmaking`.

### Block 5 — AI players

An AI player goes through the same contract as a human: it receives the same per-player view and its moves go through the same validation. Each level is a named player account.

#### 0.21.0 — AI framework and easy level

- **Content**: the AI player framework (an AI account driven by the Back), easy level (random shots).
- **Done**: a player wins or loses a match against the easy AI.
- **Branches**: `feature/ai-framework`, `feature/ai-easy`.

#### 0.22.0 — Medium level

- **Content**: medium level (targets around a hit), simulation bench.
- **Done**: over simulated matches, the medium AI beats the easy AI.
- **Branches**: `feature/ai-medium`, `chore/ai-simulation-bench`.

#### 0.23.0 — Hard level

- **Content**: hard level (deduces the orientation of a ship).
- **Done**: over simulated matches, the hard AI beats the medium AI.
- **Tests (0.21 to 0.23)**: strategies tested deterministically with a seeded random source; statistical simulations tagged as slow and run in a separate CI job.
- **Branches**: `feature/ai-hard`.

### Block 6 — Towards v1.0.0

#### 0.24.0 — Administration tool and moderation ground

- **Content**: the separate administration tool (a front, through the gateway), account state management, session audit view; reports table and message hiding (data only, no moderation code).
- **Done**: the administrator changes an account state; a hidden message disappears from the clients but remains in the database.
- **Branches**: `feature/admin-tool`, `feature/account-states`, `feature/reports-model`, `feature/message-hiding`.

#### 0.25.0 — Native installation

- **Content**: installation package or PowerShell scripts, **run as administrator** (IIS site, Windows services, systemd units declared with `Type=notify`, one Windows Event Log source per executable), certificate and key provisioning, final network flow matrix, acceptance environment.
- **Done**: a clean machine is installed from the package alone.
- **Tests**: installation smoke test on a clean machine; the Back and the Very Back run on Linux at least once in acceptance.
- **Branches**: `feature/installation-package`, `chore/systemd-units`, `chore/iis-deployment`, `docs/administrator-installation-guide`.

#### 0.26.0 — Docker variant

- **Content**: Dockerfiles and a `docker-compose` file. Without IIS, the website and the gateway run on Kestrel.
- **Done**: the whole platform starts with `docker compose up` on a clean machine.
- **Branches**: `feature/docker-images`, `docs/docker-deployment`.

#### 1.0.0-beta.N and 1.0.0-rc.N — Hardening

- **Content**: security review (Ackbar and Cassian), load test with about twenty players, backup and restore test, complete user, administrator and developer documentation, upgrade notes.
- **Done**: a family-and-friends beta runs for one to two weeks without a blocking bug.

#### 1.0.0 — Release

- **Done**: clean installation in production and complete verification run; final changelog and tag.

### Slack

If time runs short, these versions can move to 1.1 without affecting the core: **0.17** (spectators), **0.18** (profile, friends and messaging), **0.22** and **0.23** (medium and hard AI).

## Test strategy

| Level | Covers | From |
|-------|--------|------|
| Architecture | Dependency direction inside each module; a game never references another game; the Back has no database access; the gateway has no game logic; no cycles | 0.2.0 |
| Domain | Game rules, rating, matchmaking, AI strategies; pure code with an injectable random source | 0.13.0 |
| Unit | Framing, serialization, cryptography helpers, token hashing, validation | 0.3.0 |
| Integration — network and security | Real loopback sockets, mutual TLS cases, handshake (man in the middle, replay, downgrade, test vectors), reconnection | 0.3.0 to 0.6.0 |
| SQL | Stored procedures called through the real application user: JSON results, table-valued parameters, permissions, temporal history, immutable audit | 0.3.0 |
| End-to-end | A headless client through the five tiers | 0.3.0 |
| UI | Blazor component tests | 0.15.0 |

- Test projects live in `test/` and all run in the CI; a CI guard fails when one is missing.
- **SQL project outside the CI**: a PowerShell script builds the `.sqlproj` into a DACPAC and deploys it to a throwaway `TEST_` database. The SQL tests are a normal .NET test project. Without a SQL Server they are reported as *skipped*, never as passed. Running them locally is **mandatory before every `release/X.Y.Z` → `dev` merge**. A SQL Server container may be used **in the CI only**, if the CI can build the DACPAC (checked in 0.3.0).
- Slow simulations are tagged and run in a separate CI job; end-to-end tests wait for events, never for fixed delays.

## Network flows

Every tier communicates through the network. All ports are configurable; the defaults below were chosen in 0.3.0. This matrix is completed at each version that adds a flow and is copied into the administrator documentation.

| Flow | Port | Protocol | Firewall zones |
|------|------|----------|----------------|
| Internet → website and gateway (IIS) | 443 | HTTPS, WebSocket | WAN → DMZ (the only port open to the outside) |
| Gateway → Back | 7821 | TCP + TLS 1.3 (plain TCP in 0.3.0 and 0.4.0, development only) | DMZ → servers |
| Back → Very Back | 7822 | TCP + TLS 1.3 + authenticated ECDH (plain TCP in 0.3.0 and 0.4.0, development only) | servers → servers |
| Very Back → SQL Server | 1433 | TDS | servers → servers |
| Back → SMTP server | 587 | SMTP + STARTTLS | servers → mail server |
| Every executable → OpenTelemetry collector (optional) | 4317 or 4318 | OTLP over gRPC or HTTP | DMZ and servers → servers |

## Risks

| Risk | Addressed in |
|------|--------------|
| A fragile custom protocol (framing, correlation, partial reads) | 0.3.0 (minimal), 0.4.0 (fuzzing, limits, reconnection) |
| A design error in the handshake | 0.6.0: written threat model, test vectors, standard primitives only |
| Security added late breaks the architecture | Transport interface in 0.3.0, security layers plugged in 0.5.0 and 0.6.0 |
| SQL tests not run because the SQL project is outside the CI | Test-deployment script, explicit *skipped* status, mandatory local gate before each release merge |
| Drift from cross-platform code | Back and Very Back run on Linux in acceptance (0.25.0) |
| Hidden information leaks to a client or a spectator | View tests in 0.13.0; server-only models never sent to the browser |
| Scope creep | Done criteria per version; slack versions movable to 1.1 |
| Real-time bugs (reconnection, duplicate moves) | Idempotency in 0.14.0, reconnection in 0.15.0 |
| Secrets and certificate rotation | 0.5.0 and 0.7.0, documented in 0.25.0 |

## Decisions taken

| Topic | Decision |
|-------|----------|
| Solution architecture | Modular monolith; plugins or services considered when games multiply (no Docker in the author's own hosting) |
| Hosting | Website on IIS (Windows Server); services as Windows services or Linux systemd daemons; Docker variant offered for other deployments |
| Back ↔ Very Back | Raw TCP sockets, TLS 1.3 with mutual authentication, plus an authenticated ECDH handshake |
| Gateway ↔ Back | Raw TCP sockets, TLS 1.3 with mutual authentication |
| Socket protocol | The transport hands over a byte stream, so that TLS wraps it; frames carry a length prefix, a correlation identifier and a UTF-8 JSON payload (System.Text.Json), with a maximum frame size; several requests in flight on one connection. No version field before 0.4.0 |
| Default ports | Gateway → Back 7821, Back → Very Back 7822 (unassigned in the IANA registry, range 7811-7844); configurable; listeners on the loopback address unless configured otherwise |
| Real time | SignalR, through the gateway, for every front including a future WPF client |
| Identity | Opaque session token verified by the Back; no JWT |
| Forms | InteractiveServer render mode, full validation, revalidated by the gateway and the Back |
| Database | SQL Server only until 1.0.0; the SQL project keeps its original format (DACPAC, opened by Visual Studio 2026) and is never converted to the SDK style; outside the CI unless the CI can build it as it is |
| Development databases | `DEV_` and `ACC_` rebuilt freely until the first beta; no migration scripts before then |
| Match history | Every move recorded; training logs pseudonymized |
| Spectators | Public view: the match is visible, the fleets never are |
| Rating | A rating system (Elo or Glicko), per game |
| AI | Named player accounts going through the same contract as humans |
| Languages | English and French in v1.0.0, in `.resx` files; language switched on the fly; flags from flagcdn.com served by the site; only codes travel between tiers, the fronts translate them |
| User interface | Arcade neon design system (direction approved, mock-up being refined): cyan for the player and primary actions, magenta for the opponent and hits, glow only on interactive or live elements; each game has its own icon and background watermark |

## Choices left for later

| Choice | Decided in |
|--------|------------|
| Certificate authority (private CA or self-signed with pinning), key storage and rotation | 0.5.0 |
| Vault product | 0.7.0 |
| How clients present the session token to the SignalR hub | 0.10.0 |
| Chat retention and GDPR (hiding versus deleting) | 0.12.0 |
| Elo or Glicko | 0.19.0 |
| Whether AI players appear in the ranking and in random matchmaking | 0.20.0 / 0.21.0 |
| Names of the AI players | 0.21.0 |
| Further languages (one `.resx` file per component and language) | When needed, after 1.0.0 |
