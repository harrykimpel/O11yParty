# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

O11yParty is a Jeopardy-style trivia game built with C# / .NET 10 and ASP.NET Core Blazor (Interactive Server-side rendering). It's designed for team-building events with a focus on observability, cloud, and DevOps topics. Features include multi-team scoring, timers, daily doubles, and optional New Relic buzzer integration.

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
- **`Components/Pages/Home.razor`** (~930 lines) — the entire game lives here. It owns all game state as component fields and drives a state machine through these phases: `Setup → Board → Question → Answer → Winner`.
- **`Components/Layout/`** — shell layout, reconnect modal.
- **`Components/App.razor` / `Routes.razor`** — root wiring.

### Services (registered in `Program.cs`)
- **`O11yPartyDataService`** (singleton) — loads and caches `wwwroot/data/o11yparty-board.json` and `wwwroot/data/teams.json`. Falls back gracefully if files are missing.
- **`NewRelicBuzzService`** (transient + `HttpClient`) — polls New Relic's GraphQL API for remote buzz events. Used to detect which team buzzed in during a live game.

### Models (`Models/O11yPartyBoard.cs`)
Simple POCOs: `O11yPartyBoard → O11yPartyCategory → O11yPartyQuestion`.

### Data Files (`wwwroot/data/`)
- `o11yparty-board.json` — all categories and questions/answers. Edit this to change game content.
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

### Deployment
- `Dockerfile` — multi-stage build (SDK image → runtime image), exposes port 8080.
- `apprunner.yaml` — AWS AppRunner configuration.
