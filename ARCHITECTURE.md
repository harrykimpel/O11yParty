# Architecture

O11yParty is a single-page Blazor Server application. All game logic runs server-side; the browser connects via a persistent SignalR circuit for rendering (Blazor's own `/_blazor` circuit). A second, independent SignalR hub (`/hubs/buzz`) exists purely to receive buzz-in events pushed from the companion [O11yParty-Buzzer](https://github.com/harrykimpel/O11yParty-Buzzer) app — see [Buzz transport](#buzz-transport).

## Component tree

```plain
App.razor
└── Routes.razor
    └── MainLayout.razor          layout shell
        └── Home.razor            @page "/"  ← game state machine lives here
            ├── GameHeader
            ├── TeamScoreboard         visible in every phase; also where teams are added mid-game
            └── (phase switch)
                ├── GameSetupPanel    phase: Setup
                ├── GameBoard         phase: Board
                ├── QuestionPanel     phase: Question | Answer
                └── WinnerLeaderboard phase: Winner

Hubs/BuzzHub.cs                   SignalR endpoint, independent of the component tree above —
                                   pushes into Home.razor via IBuzzNotifier (see Buzz transport)
```

## Game phases

`Home.razor` owns a `GamePhase` field that drives which child component is visible:

```plain
Setup → Board → Question ⇄ Answer → Winner
                    ↑                   |
                    └─── (play again) ──┘
```

| Phase | What's shown |
| --- | --- |
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

Displays all team names and current scores, editable inline (click a score to type a new total). Also owns a small "add team/attendee" form so a late arrival can be added without leaving the current phase — it's rendered in every phase, not just Setup. Re-renders automatically whenever the parent passes an updated `Teams` list.

The parent (`Home.razor`) rejects a duplicate name (case-insensitive) rather than adding it, since buzz-in matching is by name — see `AddTeam` below.

Parameters: `Teams`, `StartCollapsed`
Callbacks: `OnScoreSet((Team, int))`, `OnTeamAdded(string)`

### `GameSetupPanel`

Setup form: team name editor, game option toggles (Double Down, Timer, Buzzer, Auto-mark answered on award, seconds per question, questions per category), category picker, and Start Game button.

Boolean toggles use the Blazor `@bind-X` convention — each has a matching `XChanged` EventCallback so the parent can use `@bind-EnableDoubleDown`. Category and question-count changes invoke explicit callbacks because they trigger a board rebuild in the parent. `OnTeamAdded` here is the same `Home.razor.AddTeam` method `TeamScoreboard` calls — this call site passes no name (auto-generates "Team N"), `TeamScoreboard`'s passes the typed name.

Parameters: `Teams`, `EnableDoubleDown`, `EnableTimer`, `EnableBuzzer`, `AutoMarkAnsweredOnAward`, `QuestionSeconds`, `NumberOfQuestions`, `SourceCategories`, `SelectedCategoryNames`, `IsBoardValid`
Callbacks: `OnTeamNameChanged`, `OnTeamRemoved`, `OnTeamAdded`, `OnNumberOfQuestionsChanged`, `OnCategoryToggled`, `OnStartGame`

### `GameBoard`

Renders the question tile grid. Tiles are disabled once answered. Fires `OnQuestionSelected` when a tile is clicked; the parent handles the phase transition and chaos-mode side effects.

Parameters: `Categories`, `NumberOfQuestions`
Callbacks: `OnQuestionSelected`

### `QuestionPanel`

Handles the Question and Answer phases in one component since both views share the same question context.

Owns `_doubleDownWager` as internal state (reset in `OnParametersSet` when `CurrentQuestion` changes). Contains `GetQuestionPoints()`, prompt/answer HTML formatters, and the wager clamp logic — none of which need to be visible to the parent.

When the user clicks **Mark Answered**, the component passes the computed points back via `OnCompleteQuestion(int points)` so the parent can auto-award the buzzed team if no manual score was applied. The parent's award path (`AdjustScore`) and its "Mark Answered" path (`HandleCompleteQuestion`) both funnel into a shared `CompleteCurrentQuestion()` — see [Auto-mark answered on award](#auto-mark-answered-on-award).

Parameters: `Phase`, `CurrentQuestion`, `Teams`, `EnableTimer`, `TimeRemaining`, `EnableBuzzer`, `BuzzerConfigured`, `BuzzerLocked`, `BuzzedTeam`, `RemoteBuzzedName`, `MaxWager`
Callbacks: `OnBuzzIn`, `OnBuzzReset`, `OnScoreAdjusted(ScoreEvent)`, `OnRevealAnswer`, `OnReturnToBoard`, `OnCompleteQuestion(int)`

### `WinnerLeaderboard`

Sorts teams by score descending, renders a top-3 podium with the celebration video, and a table for 4th place and below. Medal and podium-class helpers are static methods local to this component.

Parameters: `Teams`
Callbacks: `OnPlayAgain`

## Services

| Service | Lifetime | Role |
| --- | --- | --- |
| `O11yPartyDataService` | Singleton | Loads and caches `o11yparty-board.json` and `teams.json` from `wwwroot/data/` |
| `IBuzzNotifier` / `BuzzNotifier` | Singleton | In-process pub/sub: `Hubs/BuzzHub.cs` publishes a `BuzzNotification`, `Home.razor` subscribes via `BuzzReceived`. Default (SignalR push) transport — see [Buzz transport](#buzz-transport) |
| `NewRelicBuzzService` | Transient + `HttpClient` | Legacy transport: polls New Relic's GraphQL API for remote buzz events, only used when `NewRelic:UseLegacyBuzzPolling` is `true`. `IsConfigured` is false when credentials are absent — the game runs without it |

## Models

| Type | File | Notes |
| --- | --- | --- |
| `O11yPartyBoard` | `Models/O11yPartyBoard.cs` | Root board POCO |
| `O11yPartyCategory` | `Models/O11yPartyBoard.cs` | Category with a list of questions |
| `O11yPartyQuestion` | `Models/O11yPartyBoard.cs` | Question with `Value`, `Prompt`, `Answer`, `IsDoubleDown`, `IsAnswered` |
| `Team` | `Models/Team.cs` | Runtime team state: `Name` + `Score` |
| `GamePhase` | `Models/GamePhase.cs` | Enum: `Setup`, `Board`, `Question`, `Answer`, `Winner` |
| `ScoreEvent` | `Models/ScoreEvent.cs` | Record passed from `QuestionPanel` to parent when a score button is clicked |
| `BuzzNotification` | `Services/IBuzzNotifier.cs` | Record published by `BuzzHub`: team name, server-stamped `BuzzedAtUtcMs` (authoritative — used for arbitration), and `ClientBuzzedAtUtcMs` (diagnostics only) |

## Data flow

```plain
Home.razor (state)
    │  parameters (read-only view of state)
    ▼
Child components (render)
    │  EventCallbacks (user actions)
    ▼
Home.razor (mutates state → Blazor re-renders)
```

Child components never mutate shared state directly. They fire callbacks; `Home.razor` applies the mutation and Blazor propagates the updated parameters back down on the next render cycle.

## Buzz transport

Two independent ways for the companion buzzer app to report who buzzed in, selected once at startup via `NewRelic:UseLegacyBuzzPolling` (default `false`):

| | SignalR push (default) | Legacy New Relic polling |
| --- | --- | --- |
| Path | `Hubs/BuzzHub.cs` → `IBuzzNotifier` → `Home.OnBuzzNotification` | `NewRelicBuzzService` polling loop, gated by `_enableBuzzer` |
| Latency | Push, sub-second | Poll interval — higher latency |
| Auth | `BuzzHub:SharedSecret` (see below) | New Relic User API key |

**`BuzzHub`** (`/hubs/buzz`) authenticates each connection in `OnConnectedAsync` via a shared secret (`BuzzHub:SharedSecret`, sent by the buzzer as `?access_token=` or `Authorization: Bearer`): a blank secret fails **open** in Development only, and fails **closed** (`Context.Abort()`) everywhere else; a mismatched token always fails closed. Its `Buzz(teamName, buzzedAtUtcMs)` method stamps its **own** server-side timestamp rather than trusting the client's — a client-supplied `buzzedAtUtcMs` would let a misbehaving buzzer always claim to have won — and publishes a `BuzzNotification` through `IBuzzNotifier`. The client-supplied timestamp is kept on the notification only for latency diagnostics.

**Arbitration window** (`Home.CollectBuzzAsync`): SignalR delivers concurrent buzzes in arrival order, which network jitter can reorder. The first buzz for a question opens a `BuzzWindowMs` (400 ms) collection window; every buzz that lands within it is gathered into `_buzzCandidates`, then the **lowest server-stamped `BuzzedAtUtcMs`** wins — i.e. whoever the server actually heard from first, not whoever the network delivered first. The legacy polling path doesn't need this: NRQL's `earliest()` already resolves the winner server-side.

## Auto-mark answered on award

Setup toggle `AutoMarkAnsweredOnAward` (`_autoMarkAnsweredOnAward`, on by default). `AdjustScore` and `HandleCompleteQuestion` both call a shared `CompleteCurrentQuestion()` (close buzz window, stop timer, mark `IsAnswered`, clear `_currentQuestion`, transition phase) — `CompleteCurrentQuestion()` no-ops if the question is already completed, so whichever path gets there first wins with no double-transition:

- A **positive** score adjustment for any team while a question is open is treated as "this team got it right" and calls `CompleteCurrentQuestion()` immediately, when the toggle is on.
- Penalties (delta ≤ 0) never do this — the question stays open for another team to try.
- Clicking **Mark Answered** always completes the question, toggle or no toggle; if the auto-mark path already did it, this is just a no-op.

## Chaos modes

Activated via `?chaos=mode1,mode2` query parameter. Intended for live observability demos — active modes are shown in a banner at the top of the page.

| Mode | Effect |
| --- | --- |
| `slowload` | 4-second startup delay |
| `memleak` | Allocates ~1 MB every 3 s, holds references to prevent GC |
| `errors` | 40% chance of synthetic question-load failure |
| `latency` | 1.5–4 s random delay when opening a question |
| `timerdrift` | Countdown timer runs at 2× speed |
