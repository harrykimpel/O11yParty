# Architecture

O11yParty is a single-page Blazor Server application. All game logic runs server-side; the browser connects via a persistent SignalR circuit.

## Component tree

```
App.razor
└── Routes.razor
    └── MainLayout.razor          layout shell
        └── Home.razor            @page "/"  ← game state machine lives here
            ├── GameHeader
            ├── TeamScoreboard
            └── (phase switch)
                ├── GameSetupPanel    phase: Setup
                ├── GameBoard         phase: Board
                ├── QuestionPanel     phase: Question | Answer
                └── WinnerLeaderboard phase: Winner
```

## Game phases

`Home.razor` owns a `GamePhase` field that drives which child component is visible:

```
Setup → Board → Question ⇄ Answer → Winner
                    ↑                   |
                    └─── (play again) ──┘
```

| Phase | What's shown |
|---|---|
| `Setup` | `GameSetupPanel` — configure teams, categories, options |
| `Board` | `GameBoard` — question tile grid |
| `Question` | `QuestionPanel` — prompt, buzzer, timer |
| `Answer` | `QuestionPanel` — answer reveal + scoring |
| `Winner` | `WinnerLeaderboard` — podium + full rankings |

## Components

### `Home.razor` — game controller
Owns all shared game state and drives phase transitions. Does not render game UI directly; delegates to child components via parameters and `EventCallback`.

Key state fields: `_phase`, `_teams`, `_categories`, `_currentQuestion`, `_buzzerLocked`, `_buzzedTeam`, `_timeRemaining`, `_maxWager`, `_activeChaos`.

### `GameHeader`
Renders the cosmic header bar. Shows a board-validity warning during Setup, and New game / Finish game buttons during active play.

Parameters: `Phase`, `IsBoardValid`, `NumberOfQuestions`
Callbacks: `OnResetGame`, `OnEndGame`

### `TeamScoreboard`
Read-only display of all team names and current scores. Re-renders automatically whenever the parent passes an updated `Teams` list.

Parameters: `Teams`

### `GameSetupPanel`
Setup form: team name editor, game option toggles (Double Down, Timer, Buzzer, seconds per question, questions per category), category picker, and Start Game button.

Boolean toggles use the Blazor `@bind-X` convention — each has a matching `XChanged` EventCallback so the parent can use `@bind-EnableDoubleDown`. Category and question-count changes invoke explicit callbacks because they trigger a board rebuild in the parent.

Parameters: `Teams`, `EnableDoubleDown`, `EnableTimer`, `EnableBuzzer`, `QuestionSeconds`, `NumberOfQuestions`, `SourceCategories`, `SelectedCategoryNames`, `IsBoardValid`
Callbacks: `OnTeamNameChanged`, `OnTeamRemoved`, `OnTeamAdded`, `OnNumberOfQuestionsChanged`, `OnCategoryToggled`, `OnStartGame`

### `GameBoard`
Renders the question tile grid. Tiles are disabled once answered. Fires `OnQuestionSelected` when a tile is clicked; the parent handles the phase transition and chaos-mode side effects.

Parameters: `Categories`, `NumberOfQuestions`
Callbacks: `OnQuestionSelected`

### `QuestionPanel`
Handles the Question and Answer phases in one component since both views share the same question context.

Owns `_doubleDownWager` as internal state (reset in `OnParametersSet` when `CurrentQuestion` changes). Contains `GetQuestionPoints()`, prompt/answer HTML formatters, and the wager clamp logic — none of which need to be visible to the parent.

When the user clicks **Mark Answered**, the component passes the computed points back via `OnCompleteQuestion(int points)` so the parent can auto-award the buzzed team if no manual score was applied.

Parameters: `Phase`, `CurrentQuestion`, `Teams`, `EnableTimer`, `TimeRemaining`, `EnableBuzzer`, `BuzzerConfigured`, `BuzzerLocked`, `BuzzedTeam`, `RemoteBuzzedName`, `MaxWager`
Callbacks: `OnBuzzIn`, `OnBuzzReset`, `OnScoreAdjusted(ScoreEvent)`, `OnRevealAnswer`, `OnReturnToBoard`, `OnCompleteQuestion(int)`

### `WinnerLeaderboard`
Sorts teams by score descending, renders a top-3 podium with the celebration video, and a table for 4th place and below. Medal and podium-class helpers are static methods local to this component.

Parameters: `Teams`
Callbacks: `OnPlayAgain`

## Services

| Service | Lifetime | Role |
|---|---|---|
| `O11yPartyDataService` | Singleton | Loads and caches `o11yparty-board.json` and `teams.json` from `wwwroot/data/` |
| `NewRelicBuzzService` | Transient + `HttpClient` | Polls New Relic's GraphQL API for remote buzz events. `IsConfigured` is false when credentials are absent — the game runs without it |

## Models

| Type | File | Notes |
|---|---|---|
| `O11yPartyBoard` | `Models/O11yPartyBoard.cs` | Root board POCO |
| `O11yPartyCategory` | `Models/O11yPartyBoard.cs` | Category with a list of questions |
| `O11yPartyQuestion` | `Models/O11yPartyBoard.cs` | Question with `Value`, `Prompt`, `Answer`, `IsDoubleDown`, `IsAnswered` |
| `Team` | `Models/Team.cs` | Runtime team state: `Name` + `Score` |
| `GamePhase` | `Models/GamePhase.cs` | Enum: `Setup`, `Board`, `Question`, `Answer`, `Winner` |
| `ScoreEvent` | `Models/ScoreEvent.cs` | Record passed from `QuestionPanel` to parent when a score button is clicked |

## Data flow

```
Home.razor (state)
    │  parameters (read-only view of state)
    ▼
Child components (render)
    │  EventCallbacks (user actions)
    ▼
Home.razor (mutates state → Blazor re-renders)
```

Child components never mutate shared state directly. They fire callbacks; `Home.razor` applies the mutation and Blazor propagates the updated parameters back down on the next render cycle.

## Chaos modes

Activated via `?chaos=mode1,mode2` query parameter. Intended for live observability demos — active modes are shown in a banner at the top of the page.

| Mode | Effect |
|---|---|
| `slowload` | 4-second startup delay |
| `memleak` | Allocates ~1 MB every 3 s, holds references to prevent GC |
| `errors` | 40% chance of synthetic question-load failure |
| `latency` | 1.5–4 s random delay when opening a question |
| `timerdrift` | Countdown timer runs at 2× speed |
