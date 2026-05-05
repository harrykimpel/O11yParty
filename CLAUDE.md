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

### Component Structure
- **`Components/Pages/Home.razor`** (~1185 lines) — the entire game lives here. It owns all game state as component fields and drives a state machine through these phases: `Setup → Board → Question → Answer → Winner`.
- **`Components/Layout/`** — shell layout, reconnect modal.
- **`Components/App.razor` / `Routes.razor`** — root wiring.

### Services (registered in `Program.cs`)
- **`O11yPartyDataService`** (singleton) — loads and caches `wwwroot/data/o11yparty-board.json` and `wwwroot/data/teams.json`. Falls back gracefully if files are missing.
- **`NewRelicBuzzService`** (transient + `HttpClient`) — polls New Relic's GraphQL API for remote buzz events. Used to detect which team buzzed in during a live game.

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
    "BuzzNameAttribute": "teamName"
  }
}
```
New Relic credentials can be overridden via environment variables (`NewRelic__AccountId`, `NewRelic__UserApiKey`). The New Relic integration is optional; the game runs without it.

### New Relic APM Agent
`newrelic.config` enables the .NET APM agent with distributed tracing, all request headers, and SQL obfuscation. The Dockerfile wires up the CoreCLR profiler at runtime via environment variables (`CORECLR_ENABLE_PROFILING`, `CORECLR_PROFILER`, `CORECLR_NEWRELIC_HOME`, `CORECLR_PROFILER_PATH`, `NEW_RELIC_LICENSE_KEY`). Set `NEW_RELIC_LICENSE_KEY` at container runtime to activate APM instrumentation.

### Chaos Modes
Chaos modes are activated via the `?chaos=` query parameter (comma-separated) and are intended for observability demos. Active modes appear in a banner at the top of the page.

| Mode | Effect |
|---|---|
| `slowload` | Injects a 4-second startup delay |
| `memleak` | Continuously leaks memory (~1 MB/s) until disabled |
| `errors` | 40% chance of a synthetic question-load failure |
| `latency` | Injects a random delay (100–2000 ms) when opening a question |
| `timerdrift` | Runs the countdown timer at 2× speed |

Example: `https://localhost:7291/?chaos=latency,errors`

### Deployment
- `Dockerfile` — multi-stage build (SDK image → runtime image), bundles New Relic APM agent, exposes port 8080.
- `apprunner.yaml` — AWS AppRunner configuration.
- `.devcontainer/devcontainer.json` — Dev Container configuration for VS Code / Codespaces.
