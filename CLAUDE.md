# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

O11yParty is a Jeopardy-style trivia game built with C# / .NET 10 and ASP.NET Core Blazor (Interactive Server-side rendering). It's designed for team-building events with a focus on observability, cloud, and DevOps topics. Features include multi-team scoring, timers, double downs, optional New Relic buzzer integration, and chaos modes for observability demos.

## Commands

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run (available at https://localhost:7291 or http://localhost:5122)
dotnet run

# Trust the dev certificate if needed
dotnet dev-certs https --trust

# Docker build and run
docker build -t o11yparty .
docker run -p 8080:8080 o11yparty
```

There are no automated tests in this project.

## Architecture

See [ARCHITECTURE.md](ARCHITECTURE.md) for the full component tree, buzz-transport design
(SignalR push vs legacy polling), and the auto-mark-answered-on-award flow. Summary below.

### Component Structure

`Components/Pages/Home.razor` owns all shared game state (`_phase`, `_teams`, `_categories`,
`_currentQuestion`, etc.) and drives the phase state machine: `Setup → Board → Question → Answer →
Winner`. It doesn't render game UI itself — it delegates to child components, which never mutate
state directly (they fire `EventCallback`s back up):

- `GameHeader` — header bar, board-validity warning, New Game / Finish Game.
- `TeamScoreboard` — rendered in **every** phase (not just Setup); editable scores, plus an
  inline add-team/attendee form so a late arrival can be added mid-game.
- `GameSetupPanel` (Setup phase) — team editor, option toggles (Double Down, Timer, Buzzer, Auto-
  mark answered on award), category picker, TSV question import.
- `GameBoard` (Board phase) — question tile grid.
- `QuestionPanel` (Question/Answer phases) — prompt, buzzer UI, scoring, wager.
- `WinnerLeaderboard` (Winner phase) — podium + rankings.
- `Components/Layout/` — shell layout, reconnect modal. `Components/App.razor` / `Routes.razor` —
  root wiring.

`Hubs/BuzzHub.cs` is a separate SignalR endpoint (`/hubs/buzz`, independent of the Blazor render
circuit) that the companion buzzer app connects to — see "Buzz transport" below.

### Services (registered in `Program.cs`)

- **`O11yPartyDataService`** (singleton) — loads and caches `wwwroot/data/o11yparty-board.json` and `wwwroot/data/teams.json`. Falls back gracefully if files are missing.
- **`IBuzzNotifier` / `BuzzNotifier`** (singleton) — in-process pub/sub: `BuzzHub` publishes a `BuzzNotification`, `Home.razor` subscribes. The default (SignalR push) buzz transport.
- **`NewRelicBuzzService`** (transient + `HttpClient`) — legacy transport: polls New Relic's GraphQL API for remote buzz events. Only active when `NewRelic:UseLegacyBuzzPolling` is `true`.

### Buzz transport

Two ways the companion buzzer app can report a buzz-in, selected at startup via
`NewRelic:UseLegacyBuzzPolling` (default `false`, i.e. SignalR push):

- **SignalR push (default)** — the buzzer connects to `/hubs/buzz` and calls `Buzz(teamName,
  buzzedAtUtcMs)`. `BuzzHub` authenticates the connection via `BuzzHub:SharedSecret` (query
  string `access_token` or `Authorization: Bearer`) — blank fails open in Development only,
  fails closed everywhere else; a mismatched token always fails closed. It stamps its **own**
  server-side timestamp (not the client's — a misbehaving buzzer could otherwise always claim to
  have won) before publishing. `Home.CollectBuzzAsync` then opens a 400 ms arbitration window to
  correct for network-reordering: the lowest server-stamped timestamp among everyone who buzzed
  in that window wins.
- **Legacy polling** — `NewRelicBuzzService` polls NerdGraph; NRQL's `earliest()` resolves the
  winner server-side, so no local arbitration window is needed.

### Auto-mark answered on award

Setup toggle `AutoMarkAnsweredOnAward` (on by default). `AdjustScore` and `HandleCompleteQuestion`
share a `CompleteCurrentQuestion()` helper (idempotent — no-ops if already completed). Awarding a
**positive** score to any team while a question is open completes it automatically; penalties
never do. Clicking **Mark Answered** always completes it regardless of the toggle.

### Models (`Models/O11yPartyBoard.cs`)

Simple POCOs: `O11yPartyBoard → O11yPartyCategory → O11yPartyQuestion`. `O11yPartyQuestion` includes `IsDoubleDown` (marks a question as a double-down wager) and `IsAnswered` (runtime state).

### Data Files (`wwwroot/data/`)

- `o11yparty-board.json` — active board: all categories and questions/answers. Edit this to change game content.
- `o11yparty-board-wth.json` — alternate "WTH" themed board variant.
- `o11yparty-board-wth-original.json` — original WTH board (backup/reference).
- `o11yparty-board-initial.json` — initial/default board snapshot.
- `teams.json` — team names shown on the setup screen.

### Configuration (`appsettings.json`)

```json
{
  "Board": {
    "Name": "O11y Party",
    "NumberOfCategories": 3,
    "NumberOfQuestions": 3
  },
  "NewRelic": {
    "AccountId": "...",
    "UserApiKey": "...",
    "BuzzEventType": "O11yPartyBuzz",
    "BuzzNameAttribute": "teamName",
    "UseLegacyBuzzPolling": false
  },
  "BuzzHub": {
    "SharedSecret": "..."
  }
}
```

New Relic credentials can be overridden via environment variables (`NewRelic__AccountId`, `NewRelic__UserApiKey`). The New Relic integration is optional; the game runs without it. `BuzzHub__SharedSecret` authenticates the buzzer's SignalR connection — required outside Development (see "Buzz transport" above).

### New Relic APM Agent

`newrelic.config` enables the .NET APM agent with distributed tracing, all request headers, and SQL obfuscation. The Dockerfile wires up the CoreCLR profiler at runtime via environment variables (`CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_NEWRELIC_HOME`, `CORECLR_PROFILER_PATH`, `NEW_RELIC_LICENSE_KEY`). Set `NEW_RELIC_LICENSE_KEY` at container runtime to activate APM instrumentation.

### Chaos Modes

Chaos modes are activated via the `?chaos=` query parameter (comma-separated) and are intended for observability demos. Active modes appear in a banner at the top of the page.

| Mode | Effect |
| --- | --- |
| `slowload` | Injects a 4-second startup delay |
| `memleak` | Continuously leaks memory (~1 MB/s) until disabled |
| `errors` | 40% chance of a synthetic question-load failure |
| `latency` | Injects a random delay (100–2000 ms) when opening a question |
| `timerdrift` | Runs the countdown timer at 2× speed |

Example: `https://localhost:7291/?chaos=latency,errors`

### Deployment

- `Dockerfile` — multi-stage build (SDK image → runtime image), bundles New Relic APM agent, exposes port 8080. Build stage is pinned `--platform=$BUILDPLATFORM` (avoids crashing MSBuild under QEMU emulation on a cross-arch build host) — **the runtime stage is currently unpinned**, so building on an Apple Silicon host without an explicit `--platform linux/amd64` produces an arm64 image that fails with `exec format error` on standard x86_64 ECS/Fargate/App Runner. (Already fixed the same way in the companion buzzer repo's Dockerfile — worth porting here too.)
- `apprunner.yaml` — AWS AppRunner configuration.
- `docker-compose.yml` — Elastic Beanstalk Docker-compose-mode deployment; EB doesn't auto-inject environment properties in this mode, so env vars must be passed through explicitly here.
- `.devcontainer/devcontainer.json` — Dev Container configuration for VS Code / Codespaces.
